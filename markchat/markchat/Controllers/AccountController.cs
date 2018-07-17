using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Linq;
using System.Web;
using System.Web.Http;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OAuth;
using markchat.Models;
using markchat.Providers;
using markchat.Results;
using System.IO;
using MarkChat.DAL.Entities;
using System.Net;
using MarkChat.DAL.Repository;

namespace markchat.Controllers
{
    [Authorize]
    [RoutePrefix("api/Account")]
    public class AccountController : ApiController
    {
        private const string LocalLoginProvider = "Local";
        private ApplicationUserManager _userManager;
        private GenericUnitOfWork repository;

        #region Registry

        [HttpPost]
        [Route("GetVerificationCode")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> GetVerificationCode(GetVerificationCodeBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var checkExisting = (await repository.Repository<ApplicationUser>().
                FindAllAsync(x => x.PhoneNumber == model.PhoneNumber)).FirstOrDefault();

            if (checkExisting != null)
            {
                return BadRequest("This number already exists");
            }

            var checkStatedRegistry = (await repository.Repository<Confirmation>()
                .FindAllAsync(x => x.PhoneNumber == model.PhoneNumber)).FirstOrDefault();

            if(checkStatedRegistry != null)
            {
                repository.Repository<Confirmation>().Remove(checkStatedRegistry);
                await repository.SaveAsync();
            }

            Random rand = new Random();
            int code = rand.Next(1000, 9999);
            
            if (UserManager.SmsService != null)
            {
                var message = new IdentityMessage
                {
                    Destination = model.PhoneNumber,
                    Body = "Your security code is: " + code
                };               
                await UserManager.SmsService.SendAsync(message);
            }
         

            string token = RandomOAuthStateGenerator.Generate(64);

            Confirmation confirmationCode = new Confirmation()
            {
                PhoneNumber = model.PhoneNumber,
                Code = code.ToString(),
                Date = DateTime.Now,
                Token = token, 
                Confirmed = false,
            };

            repository.Repository<Confirmation>().Add(confirmationCode);
            
            await repository.SaveAsync();

            return Ok(new { Token = token });
        }


        [HttpPost]
        [Route("ConfirmPhone")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> ConfirmPhone(PhoneConfirnationModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Confirmation confirmationCode = (await repository.Repository<Confirmation>().FindAllAsync(item => item.Token == model.Token)).FirstOrDefault();

            if (confirmationCode == null)
            {
                return BadRequest("Invalid Token");
            }

            if(confirmationCode.Confirmed == true)
            {
                return BadRequest("Phone number already confirmed");
            }


            if (confirmationCode.TryCount == 3)
            {
                return BadRequest("Exceeded limit of attempts");
            }
           
            if (confirmationCode.TryCount > 0)
            {
                if ((DateTime.Now - confirmationCode.LastTry).TotalSeconds < 60)
                    return BadRequest($"Please wait {60 - (DateTime.Now - confirmationCode.LastTry).TotalSeconds} seconds");
            }

            if (confirmationCode.Code != model.Code)
            {
                confirmationCode.LastTry = DateTime.Now;
                confirmationCode.TryCount++;
                await repository.SaveAsync();
                return BadRequest("Code is not correct");
            }


            confirmationCode.Confirmed = true;

            await repository.SaveAsync();

            return Ok(new { Token = confirmationCode.Token });
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("DeleteUser")]
        public async Task<IHttpActionResult> DeleteUser(DeleteUserBindingModel model)
        {
            var user = (await repository.Repository<ApplicationUser>().FindAllAsync(x => x.PhoneNumber == model.PhoneNumber)).FirstOrDefault();
            if (!ModelState.IsValid || user == null)
            {
                return BadRequest("Bad Request");
            }

            await repository.Repository<ApplicationUser>().RemoveAsync(user);

            await repository.SaveAsync();

            return Ok("User Deleted");
        }



        [HttpPost]
        [AllowAnonymous]
        [Route("Register")]
        public async Task<IHttpActionResult> Register(RegisterBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Confirmation confirmedPhone = (await repository.Repository<Confirmation>()
                .FindAllAsync(item => item.Token == model.Token)).FirstOrDefault();

            if (confirmedPhone == null)
            {
                return BadRequest("Invalid Token");
            }

            var user = new ApplicationUser()
            {
                UserName = confirmedPhone.PhoneNumber,
                PhoneNumber = confirmedPhone.PhoneNumber,
                PhoneNumberConfirmed = confirmedPhone.Confirmed,
            };

            IdentityResult result = await UserManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }




            await repository.Repository<Confirmation>().RemoveAsync(confirmedPhone);


            await repository.SaveAsync();


            var request = HttpContext.Current.Request;
            var tokenServiceUrl = request.Url.GetLeftPart(UriPartial.Authority) + request.ApplicationPath + "Token";
            using (var client = new HttpClient())
            {
                var requestParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", user.UserName),
                new KeyValuePair<string, string>("password", model.Password)
            };
                var requestParamsFormUrlEncoded = new FormUrlEncodedContent(requestParams);
                var tokenServiceResponse = await client.PostAsync(tokenServiceUrl, requestParamsFormUrlEncoded);
                return this.ResponseMessage(tokenServiceResponse);

            }
        }

        [HttpPost]
        [Route("ConfigurateUserCabinet")]
        public async Task<HttpResponseMessage> ConfigurateUserCabinet(CabinetUserBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid data");
            }
            var user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            user.FullName = model.FullName;

            if (model.Email != "" || model.Email != null)
            {
                user.Email = model.Email;
                await repository.SaveAsync();
                try
                {
                    string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                    await UserManager.SendEmailAsync(user.Id, "Confirm your account", $"For confirmation please enter security code in application.</br>Security code : {code}");

                }
                catch
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid Email");
                }
            }


            if (model.File != null && model.PhotoName != null)
            {
                user.PhotoName = model.PhotoName;
                byte[] fileData = model.File;
                if (!Directory.Exists(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Id}")))
                    Directory.CreateDirectory(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Id}"));
                //Compressor c = new Compressor();
                //fileData = c.Wrap(fileData);
                System.IO.File.WriteAllBytes(Path.Combine(HttpContext.Current.Server.MapPath(
                            $"~/Images/UserPhotos/{user.Id}/"), model.PhotoName
                            ), fileData);

            }
            await repository.SaveAsync();
            return Request.CreateResponse(HttpStatusCode.OK, "Success");
        }


        [HttpPost]
        [Route("ChangeFullName")]
        public async Task<HttpResponseMessage> ChangeFullName(ChangeFullNameModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User doesn't exists");
            }

            user.FullName = model.FullName;

            await repository.SaveAsync();

            return Request.CreateResponse(HttpStatusCode.OK,"FullName changed");
        }

        [HttpPost]
        [Route("ChangeEmail")]
        public async Task<HttpResponseMessage> ChangeEmail(ChangeEmailModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User doesn't exists");
            }

            user.Email = model.Email;
            user.EmailConfirmed = false;

            if (model.Email != "" || model.Email != null)
            {
                user.Email = model.Email;
                await repository.SaveAsync();
                try
                {
                    string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                     await UserManager.SendEmailAsync(user.Id, "Confirm your account", $"For confirmation please enter security code in application.</br>Security code : {code}");

                }
                catch
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest,"Invalid Email");
                }   
            }


            await repository.SaveAsync();

            return Request.CreateResponse(HttpStatusCode.OK, "Email changed. Please confirm it.");
        }
        
        [HttpPost]
        [Route("ConfirmEmail")]
        public async Task<HttpResponseMessage> ConfirmEmail([FromBody]ConfirmEmailModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if(user.EmailConfirmed)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest,"Your email already confirmed");
            }

            if (model.Code == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest,"Bad Request");
            }
            var result = await UserManager.ConfirmEmailAsync(user.Id, model.Code);
            return Request.CreateResponse(HttpStatusCode.OK,result.Succeeded ? "ConfirmEmail" : "Error");
        }

        [HttpPost]
        [Route("ChangePhoto")]
        public async Task<HttpResponseMessage> ChangePhoto(ChangePhotoModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User doesn't exists");
            }


            if (model.File != null && model.PhotoName != null)
            {
                user.PhotoName = model.PhotoName;
                byte[] fileData = model.File;
                if (!Directory.Exists(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Id}")))
                    Directory.CreateDirectory(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Id}"));
                //Compressor c = new Compressor();
                //fileData = c.Wrap(fileData);
                System.IO.File.WriteAllBytes(Path.Combine(HttpContext.Current.Server.MapPath(
                            $"~/Images/UserPhotos/{user.Id}/"), model.PhotoName
                            ), fileData);

                await repository.SaveAsync();

                return Request.CreateResponse(HttpStatusCode.OK, "Photo changed.");

            }


            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalide data");

        }


        #endregion


        public AccountController()
        {
            repository = new GenericUnitOfWork();
        }

        public AccountController(ApplicationUserManager userManager,
            ISecureDataFormat<AuthenticationTicket> accessTokenFormat)
        {
            UserManager = userManager;
            AccessTokenFormat = accessTokenFormat;
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        public ISecureDataFormat<AuthenticationTicket> AccessTokenFormat { get; private set; }

        // GET api/Account/UserInfo
        [HostAuthentication(DefaultAuthenticationTypes.ExternalBearer)]
        [Route("UserInfo")]
        public UserInfoViewModel GetUserInfo()
        {
            ExternalLoginData externalLogin = ExternalLoginData.FromIdentity(User.Identity as ClaimsIdentity);

            return new UserInfoViewModel
            {
                Email = User.Identity.GetUserName(),
                HasRegistered = externalLogin == null,
                LoginProvider = externalLogin != null ? externalLogin.LoginProvider : null
            };
        }

        // POST api/Account/Logout
        [Route("Logout")]
        public IHttpActionResult Logout()
        {
            Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType);
            return Ok();
        }

        // GET api/Account/ManageInfo?returnUrl=%2F&generateState=true
        [Route("ManageInfo")]
        public async Task<ManageInfoViewModel> GetManageInfo(string returnUrl, bool generateState = false)
        {
            IdentityUser user = await UserManager.FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return null;
            }

            List<UserLoginInfoViewModel> logins = new List<UserLoginInfoViewModel>();

            foreach (IdentityUserLogin linkedAccount in user.Logins)
            {
                logins.Add(new UserLoginInfoViewModel
                {
                    LoginProvider = linkedAccount.LoginProvider,
                    ProviderKey = linkedAccount.ProviderKey
                });
            }

            if (user.PasswordHash != null)
            {
                logins.Add(new UserLoginInfoViewModel
                {
                    LoginProvider = LocalLoginProvider,
                    ProviderKey = user.UserName,
                });
            }

            return new ManageInfoViewModel
            {
                LocalLoginProvider = LocalLoginProvider,
                Email = user.UserName,
                Logins = logins,
                ExternalLoginProviders = GetExternalLogins(returnUrl, generateState)
            };
        }

        // POST api/Account/ChangePassword
        [Route("ChangePassword")]
        public async Task<IHttpActionResult> ChangePassword(ChangePasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword,
                model.NewPassword);
            
            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        // POST api/Account/SetPassword
        [Route("SetPassword")]
        public async Task<IHttpActionResult> SetPassword(SetPasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result = await UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        // POST api/Account/AddExternalLogin
        [Route("AddExternalLogin")]
        public async Task<IHttpActionResult> AddExternalLogin(AddExternalLoginBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);

            AuthenticationTicket ticket = AccessTokenFormat.Unprotect(model.ExternalAccessToken);

            if (ticket == null || ticket.Identity == null || (ticket.Properties != null
                && ticket.Properties.ExpiresUtc.HasValue
                && ticket.Properties.ExpiresUtc.Value < DateTimeOffset.UtcNow))
            {
                return BadRequest("External login failure.");
            }

            ExternalLoginData externalData = ExternalLoginData.FromIdentity(ticket.Identity);

            if (externalData == null)
            {
                return BadRequest("The external login is already associated with an account.");
            }

            IdentityResult result = await UserManager.AddLoginAsync(User.Identity.GetUserId(),
                new UserLoginInfo(externalData.LoginProvider, externalData.ProviderKey));

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        // POST api/Account/RemoveLogin
        [Route("RemoveLogin")]
        public async Task<IHttpActionResult> RemoveLogin(RemoveLoginBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result;

            if (model.LoginProvider == LocalLoginProvider)
            {
                result = await UserManager.RemovePasswordAsync(User.Identity.GetUserId());
            }
            else
            {
                result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId(),
                    new UserLoginInfo(model.LoginProvider, model.ProviderKey));
            }

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        // GET api/Account/ExternalLogin
        [OverrideAuthentication]
        [HostAuthentication(DefaultAuthenticationTypes.ExternalCookie)]
        [AllowAnonymous]
        [Route("ExternalLogin", Name = "ExternalLogin")]
        public async Task<IHttpActionResult> GetExternalLogin(string provider, string error = null)
        {
            if (error != null)
            {
                return Redirect(Url.Content("~/") + "#error=" + Uri.EscapeDataString(error));
            }

            if (!User.Identity.IsAuthenticated)
            {
                return new ChallengeResult(provider, this);
            }

            ExternalLoginData externalLogin = ExternalLoginData.FromIdentity(User.Identity as ClaimsIdentity);

            if (externalLogin == null)
            {
                return InternalServerError();
            }

            if (externalLogin.LoginProvider != provider)
            {
                Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);
                return new ChallengeResult(provider, this);
            }

            ApplicationUser user = await UserManager.FindAsync(new UserLoginInfo(externalLogin.LoginProvider,
                externalLogin.ProviderKey));

            bool hasRegistered = user != null;

            if (hasRegistered)
            {
                Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);
                
                 ClaimsIdentity oAuthIdentity = await user.GenerateUserIdentityAsync(UserManager,
                    OAuthDefaults.AuthenticationType);
                ClaimsIdentity cookieIdentity = await user.GenerateUserIdentityAsync(UserManager,
                    CookieAuthenticationDefaults.AuthenticationType);

                AuthenticationProperties properties = ApplicationOAuthProvider.CreateProperties(user.UserName);
                Authentication.SignIn(properties, oAuthIdentity, cookieIdentity);
            }
            else
            {
                IEnumerable<Claim> claims = externalLogin.GetClaims();
                ClaimsIdentity identity = new ClaimsIdentity(claims, OAuthDefaults.AuthenticationType);
                Authentication.SignIn(identity);
            }

            return Ok();
        }

        // GET api/Account/ExternalLogins?returnUrl=%2F&generateState=true
        [AllowAnonymous]
        [Route("ExternalLogins")]
        public IEnumerable<ExternalLoginViewModel> GetExternalLogins(string returnUrl, bool generateState = false)
        {
            IEnumerable<AuthenticationDescription> descriptions = Authentication.GetExternalAuthenticationTypes();
            List<ExternalLoginViewModel> logins = new List<ExternalLoginViewModel>();

            string state;

            if (generateState)
            {
                const int strengthInBits = 256;
                state = RandomOAuthStateGenerator.Generate(strengthInBits);
            }
            else
            {
                state = null;
            }

            foreach (AuthenticationDescription description in descriptions)
            {
                ExternalLoginViewModel login = new ExternalLoginViewModel
                {
                    Name = description.Caption,
                    Url = Url.Route("ExternalLogin", new
                    {
                        provider = description.AuthenticationType,
                        response_type = "token",
                        client_id = Startup.PublicClientId,
                        redirect_uri = new Uri(Request.RequestUri, returnUrl).AbsoluteUri,
                        state = state
                    }),
                    State = state
                };
                logins.Add(login);
            }

            return logins;
        }

        // POST api/user/login
        [HttpPost]
        [AllowAnonymous]
        [Route("login")]
        public async Task<IHttpActionResult> LoginUser(UserAccountBindingModel model)
        {
            if (model == null)
            {
                return this.BadRequest("Invalid user data");
            }

            var user = UserManager.FindAsync(model.Username, model.Password).Result;
            var dbUser = (await repository.Repository<ApplicationUser>()
                .FindAllAsync(x => x.UserName == model.Username)).FirstOrDefault();
            if (user == null )
            {
               
                if (dbUser!= null && dbUser?.AccessFailedCount == 3)
                {
                    return BadRequest("Exceeded limit of attempts");
                }

                if (dbUser.LastSecurityCodeSendDate == null)
                    dbUser.LastSecurityCodeSendDate = DateTime.Now.AddDays(-1);


                if ((DateTime.Now - dbUser.LastSecurityCodeSendDate.Value).TotalSeconds < 60)
                        return BadRequest($"Please wait {60 - (DateTime.Now - dbUser.LastSecurityCodeSendDate.Value).TotalSeconds} seconds to next attempt.");


                
                dbUser.AccessFailedCount++;
                dbUser.LastSecurityCodeSendDate = DateTime.Now;
                await repository.SaveAsync();

                return BadRequest("The user name or password is incorrect");

            }

            var request = HttpContext.Current.Request;
            var tokenServiceUrl = request.Url.GetLeftPart(UriPartial.Authority) + request.ApplicationPath + "Token";
            using (var client = new HttpClient())
            {
                var requestParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", model.Username),
                new KeyValuePair<string, string>("password", model.Password)
            };
                var requestParamsFormUrlEncoded = new FormUrlEncodedContent(requestParams);
                var tokenServiceResponse = await client.PostAsync(tokenServiceUrl, requestParamsFormUrlEncoded);
                return this.ResponseMessage(tokenServiceResponse);
            }
        }

        //// POST api/Account/Register
        //[AllowAnonymous]
        //[Route("Register")]
        //public async Task<IHttpActionResult> Register(RegisterBindingModel model)
        //{
 
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest(ModelState);
        //    }


        //    var user = new ApplicationUser() {
        //        UserName = model.Email,
        //        Email = model.Email,
        //        FirstName = model.FirstName,
        //        MiddleName = model.MiddleName,
        //        LastName = model.LastName,
        //        PhoneNumber = model.PhoneNumber,
        //        PhotoName = model.PhotoName
                
        //    };

        //    if (model.File != null && model.PhotoName != null)
        //    {
        //        user.PhotoName = model.PhotoName;
        //        byte[] fileData = model.File;
        //        if (!Directory.Exists(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Email}")))
        //            Directory.CreateDirectory(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Email}"));
        //        //Compressor c = new Compressor();
        //        //fileData = c.Wrap(fileData);
        //        System.IO.File.WriteAllBytes(Path.Combine(HttpContext.Current.Server.MapPath(
        //                    $"~/Images/UserPhotos/{user.Email}/"), model.PhotoName
        //                    ), fileData);

        //    }

           

        //    IdentityResult result = await UserManager.CreateAsync(user, model.Password);

        //    if (!result.Succeeded)
        //    {
        //        return GetErrorResult(result);
        //    }
        //    var code = await UserManager.GenerateChangePhoneNumberTokenAsync(user.Id, model.PhoneNumber);
        //    if (UserManager.SmsService != null)
        //    {
        //        var message = new IdentityMessage
        //        {
        //            Destination = model.PhoneNumber,
        //            Body = "Your security code is: " + code
        //        };
        //        // Send token
        //        await UserManager.SmsService.SendAsync(message);
        //    }

        //    GenericUnitOfWork unit = new GenericUnitOfWork();
        //    var ruser = await unit.Repository<ApplicationUser>().FindByIdAsync(user.Id);
        //    ruser.SecurityCode = code;
        //    ruser.PhoneNumberConfirmed = false;
        //    user.LastSecurityCodeSendDate = DateTime.Now;

        //    await unit.SaveAsync();

        //    // await UserManager.AddToRoleAsync(user.Id, "Realtor");

        //    return Ok();
        //}

        // POST api/Account/RegisterExternal
        [OverrideAuthentication]
        [HostAuthentication(DefaultAuthenticationTypes.ExternalBearer)]
        [Route("RegisterExternal")]
        public async Task<IHttpActionResult> RegisterExternal(RegisterExternalBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var info = await Authentication.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return InternalServerError();
            }

            var user = new ApplicationUser() { UserName = model.Email, Email = model.Email };

            IdentityResult result = await UserManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            result = await UserManager.AddLoginAsync(user.Id, info.Login);
            if (!result.Succeeded)
            {
                return GetErrorResult(result); 
            }
         
            return Ok();
        }


        // GET: /Account/AddPhoneNumber
        public IHttpActionResult AddPhoneNumber()
        {
            return Ok();// чи щось інше повертати????
        }

        // POST: /Account/AddPhoneNumber
        //??????????????
        [HttpPost]
        public async Task<IHttpActionResult> AddPhoneNumber(AddPhoneNumberBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Generate the token 
            var code = await UserManager.GenerateChangePhoneNumberTokenAsync(
                                       User.Identity.GetUserId(), model.Number);
            if (UserManager.SmsService != null)
            {
                var message = new IdentityMessage
                {
                    Destination = model.Number,
                    Body = "Your security code is: " + code
                };
                // Send token
                await UserManager.SmsService.SendAsync(message);
            }
            return  Ok(new { PhoneNumber = model.Number });
        }

        //return ???????????
        public async Task<IHttpActionResult> VerifyPhoneNumber(string phoneNumber)
        {
            var code = await UserManager.GenerateChangePhoneNumberTokenAsync(User.Identity.GetUserId(), phoneNumber);
            if (phoneNumber == null)
                return BadRequest(ModelState);
            else
                return Ok(new VerifyPhoneNumberBindingModel { PhoneNumber = phoneNumber });
        }

        //return ???????????
        [HttpPost]
        public async Task<IHttpActionResult> VerifyPhoneNumber(VerifyPhoneNumberBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var result = await UserManager.ChangePhoneNumberAsync(User.Identity.GetUserId(), model.PhoneNumber, model.Code);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await HttpContext.Current.GetOwinContext().Get<ApplicationSignInManager>().SignInAsync(user, true, false);
                }
                return Ok(new { Message = "AddPhoneSuccess" });
            }
            ModelState.AddModelError("", "Failed to verify phone");
            return Ok(model);     
        }
       

        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                _userManager = null;
            }

            base.Dispose(disposing);
        }

        #region Helpers

        private IAuthenticationManager Authentication
        {
            get { return Request.GetOwinContext().Authentication; }
        }

        private IHttpActionResult GetErrorResult(IdentityResult result)
        {
            if (result == null)
            {
                return InternalServerError();
            }

            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (string error in result.Errors)
                    {
                        ModelState.AddModelError("", error);
                    }
                }

                if (ModelState.IsValid)
                {
                    // No ModelState errors are available to send, so just return an empty BadRequest.
                    return BadRequest();
                }

                return BadRequest(ModelState);
            }

            return null;
        }

        private class ExternalLoginData
        {
            public string LoginProvider { get; set; }
            public string ProviderKey { get; set; }
            public string UserName { get; set; }

            public IList<Claim> GetClaims()
            {
                IList<Claim> claims = new List<Claim>();
                claims.Add(new Claim(ClaimTypes.NameIdentifier, ProviderKey, null, LoginProvider));

                if (UserName != null)
                {
                    claims.Add(new Claim(ClaimTypes.Name, UserName, null, LoginProvider));
                }

                return claims;
            }

            public static ExternalLoginData FromIdentity(ClaimsIdentity identity)
            {
                if (identity == null)
                {
                    return null;
                }

                Claim providerKeyClaim = identity.FindFirst(ClaimTypes.NameIdentifier);

                if (providerKeyClaim == null || String.IsNullOrEmpty(providerKeyClaim.Issuer)
                    || String.IsNullOrEmpty(providerKeyClaim.Value))
                {
                    return null;
                }

                if (providerKeyClaim.Issuer == ClaimsIdentity.DefaultIssuer)
                {
                    return null;
                }

                return new ExternalLoginData
                {
                    LoginProvider = providerKeyClaim.Issuer,
                    ProviderKey = providerKeyClaim.Value,
                    UserName = identity.FindFirstValue(ClaimTypes.Name)
                };
            }
        }

        private static class RandomOAuthStateGenerator
        {
            private static RandomNumberGenerator _random = new RNGCryptoServiceProvider();

            public static string Generate(int strengthInBits)
            {
                const int bitsPerByte = 8;

                if (strengthInBits % bitsPerByte != 0)
                {
                    throw new ArgumentException("strengthInBits must be evenly divisible by 8.", "strengthInBits");
                }

                int strengthInBytes = strengthInBits / bitsPerByte;

                byte[] data = new byte[strengthInBytes];
                _random.GetBytes(data);
                return HttpServerUtility.UrlTokenEncode(data);
            }
        }

        #endregion
    }

    public class ChangeEmailModel
    {
        public string Email { get; set; }
    }
}
