using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Http.ModelBinding;
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
using ZstdNet;
using MarkChat.DAL;
using MarkChat.DAL.Entities;
using System.Net;
using MarkChat.DAL.Repository;
using Microsoft.Owin.Testing;
using System.Text;

namespace markchat.Controllers
{
    [Authorize]
    [RoutePrefix("api/Account")]
    public class AccountController : ApiController
    {
        private const string LocalLoginProvider = "Local";
        private ApplicationUserManager _userManager;
        private GenericUnitOfWork repository;

        #region ChatTagAPI

        [HttpPost]
        [Route("GetLastMessages")]
        //returns last 20 chat messages 
        public async Task<HttpResponseMessage> GetLastMessages([FromBody]int TagChatId)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User doesn't exists");
            }

            var tagChat = await repository.Repository<TagChat>().FindByIdAsync(TagChatId);

            if(!tagChat.Users.Contains(user))
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "You are not chat member");
            }

            var dbNotifications = tagChat.Messages.OrderByDescending(x => x.PublicationDate).Take(20);

            var responceModel = dbNotifications.Select(x =>
            {
                var tmp = new TagChatMessageModel()
                {
                    Id = x.Id,
                    AuthorFullName = x.Author?.FullName,
                    //AuthorFullName = x.Author?.FirstName + " " + x.Author?.LastName,
                    AuthorPhoto = x.Author?.PhotoName,
                    Currency = x.Currency?.Symbol.ToString(),
                    AuthorPhone = x.Author?.PhoneNumber,
                    Description = x.Description,
                    Price = x.Price,
                    PublicationDate = x.PublicationDate,
                    Tags = new Stack<string>()
                };
                var messagecategory = x.Category;

                while (messagecategory != null)
                {
                    tmp.Tags.Push(messagecategory.Name);
                    messagecategory = messagecategory.ParentCategory;
                } 
                return tmp;
            }
            ).ToList();

            

            var responce = Request.CreateResponse(HttpStatusCode.OK, responceModel);

            return responce;
        }

        [HttpGet]
        [Route("GetUserTagChats")]
        public async Task<HttpResponseMessage> GetUserTagChats()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());    
            
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound,"User doesn't exists");
            }

            Dictionary<int, string> chats = new Dictionary<int, string>();

            (await repository.Repository<TagChat>().FindAllAsync(x => x.Users.Contains(user))).ToList()
                .ForEach(x=> chats.Add(x.Id, x.Name));

            var responce = Request.CreateResponse<Dictionary<int, string>>(HttpStatusCode.OK, chats);

            return responce;
        }

        [HttpPost]
        [Route("AddNotification")]
        public async Task<HttpResponseMessage> AddNotification(AddNotificationBindingModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null || model == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User doesn't exists");
            }

            TagChat chat = await repository.Repository<TagChat>().FindByIdAsync(model.IdChat);
            Category category = await repository.Repository<Category>().FindByIdAsync(model.IdCategory);
            Currency currency = await repository.Repository<Currency>().FindByIdAsync(model.IdCurrency);

            if (chat == null || category == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Category doesn't exists");
            }

            repository.Repository<Notification>().Add(new Notification()
            {
                Author= user,
                PublicationDate = DateTime.Now,
                Category = category,
                Price = model.Price,
                Description = model.Description,
                Currency = currency,
                TagChat = chat
            });

            await repository.SaveAsync();

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [HttpPost]
        [Route("FindNotifications")]
        public async Task<HttpResponseMessage> FindNotifications(FindNotificationBindingModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null || model == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User doesn't exists");
            }

            Category category = await repository.Repository<Category>().FindByIdAsync(model.LastCategoryId);

            if (category == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Category doesn't exists");
            }

            List<Notification> messages = new List<Notification>();
                        
            Action<Category> search = null;
            search = delegate (Category c)
            {
                c.Notifications.ForEach(y => messages.Add(y));

                if (c.ChildCategories.Count > 0)
                    c.ChildCategories.ForEach(x => search(x));
            };

            search(category);

            var responceModel = messages.Select(x =>
            {
                var tmp = new TagChatMessageModel()
                {
                    Id = x.Id,
                    AuthorFullName = x.Author?.FullName,
                    //AuthorFullName = x.Author?.FirstName + " " + x.Author?.LastName,
                    AuthorPhoto = x.Author?.PhotoName,
                    Currency = x.Currency?.Symbol.ToString(),
                    AuthorPhone = x.Author?.PhoneNumber,
                    Description = x.Description,
                    Price = x.Price,
                    PublicationDate = x.PublicationDate,
                    Tags = new Stack<string>()
                };
                var messagecategory = x.Category;

                while (messagecategory != null)
                {
                    tmp.Tags.Push(messagecategory.Name);
                    messagecategory = messagecategory.ParentCategory;
                }
                return tmp;
            }
           ).ToList();

            return Request.CreateResponse(HttpStatusCode.OK, responceModel);
        }

        [HttpPost]
        [Route("GetRootCategories")]
        public async Task<HttpResponseMessage> GetRootCategories([FromBody] int TagChatId)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null || TagChatId == 0)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User doesn't exists");
            }

            TagChat chat = await repository.Repository<TagChat>().FindByIdAsync(TagChatId);

            if (chat == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat doesn't exists");
            }


            Dictionary<int, string> childCategories = new Dictionary<int, string>();
            chat.RootCategory.ChildCategories.ForEach(x => childCategories.Add(x.Id, x.Name));


            var responce = Request.CreateResponse<Dictionary<int, string>>(HttpStatusCode.OK, childCategories);

            return responce;
        }

        [HttpPost]
        [Route("GetChildCategoriesById")]
        public async Task<HttpResponseMessage> GetChildCategoriesById([FromBody] int ParentCategoryId)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null || ParentCategoryId == 0)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User doesn't exists");
            }

            Category category = await repository.Repository<Category>().FindByIdAsync(ParentCategoryId);

            if (category == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Category doesn't exists");
            }


            Dictionary<int, string> childCategories = new Dictionary<int, string>();
            category.ChildCategories.ForEach(x => childCategories.Add(x.Id, x.Name));


            var responce = Request.CreateResponse<Dictionary<int, string>>(HttpStatusCode.OK, childCategories);

            return responce;
        }

        #endregion  


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
                Confirmed = false
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
                Email = confirmedPhone.PhoneNumber+"@qwer.com",
                //FirstName = model.FirstName,
                //MiddleName = model.MiddleName,
                //LastName = model.LastName,
                PhoneNumber = confirmedPhone.PhoneNumber,
                PhoneNumberConfirmed = confirmedPhone.Confirmed,
                //PhotoName = model.PhotoName

            };


            

            //if (model.File != null && model.PhotoName != null)
            //{
            //    user.PhotoName = model.PhotoName;
            //    byte[] fileData = model.File;
            //    if (!Directory.Exists(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Email}")))
            //        Directory.CreateDirectory(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Email}"));
            //    //Compressor c = new Compressor();
            //    //fileData = c.Wrap(fileData);
            //    System.IO.File.WriteAllBytes(Path.Combine(HttpContext.Current.Server.MapPath(
            //                $"~/Images/UserPhotos/{user.Email}/"), model.PhotoName
            //                ), fileData);

            //}



            IdentityResult result = await UserManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            await repository.SaveAsync();

            return Ok(new { Token = confirmedPhone.Token});
        }


        [HttpPost]
        [AllowAnonymous]
        [Route("ConfigurateUserCabinet")]
        public async Task<IHttpActionResult> ConfigurateUserCabinet(CabinetUserBindingModel model)
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

            var user = (await repository.Repository<ApplicationUser>().FindAllAsync(x => x.PhoneNumber == confirmedPhone.PhoneNumber)).FirstOrDefault();

            user.FullName = model.FullName;

            user.Email = model.Email;
            
            if (model.File != null && model.PhotoName != null)
            {
                user.PhotoName = model.PhotoName;
                byte[] fileData = model.File;
                if (!Directory.Exists(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Email}")))
                    Directory.CreateDirectory(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Email}"));
                //Compressor c = new Compressor();
                //fileData = c.Wrap(fileData);
                System.IO.File.WriteAllBytes(Path.Combine(HttpContext.Current.Server.MapPath(
                            $"~/Images/UserPhotos/{user.Email}/"), model.PhotoName
                            ), fileData);

            }

            await repository.Repository<Confirmation>().RemoveAsync(confirmedPhone);
          

            await repository.SaveAsync();

            return Ok(new { Token = confirmedPhone.Token });
        }


        [HttpPost]
        [Route("ChangeFullName")]
        public async Task<HttpResponseMessage> ChangeFullName([FromBody] string FullName)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User doesn't exists");
            }

            user.FullName = FullName;

            await repository.SaveAsync();

            return Request.CreateResponse(HttpStatusCode.OK,"FullName changed");
        }

        [HttpPost]
        [Route("ChangeEmail")]
        public async Task<HttpResponseMessage> ChangeEmail([FromBody] string Email)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User doesn't exists");
            }

            user.Email = Email;
            user.EmailConfirmed = false;

            string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
            var callbackUrl = Url.Link("ConfirmEmail", new { userId = user.Id, code = code });
            await UserManager.SendEmailAsync(user.Id, "Confirm your account", "Please confirm your account by clicking <a href=\"" + callbackUrl + "\">here</a>");
            



            await repository.SaveAsync();

            return Request.CreateResponse(HttpStatusCode.OK, "Email changed. Please confirm it.");
        }

        [HttpPost]
        [Route("ChangePhoto")]
        public async Task<HttpResponseMessage> ChangePhoto([FromBody] string PhotoName,[FromBody] byte[] File)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User doesn't exists");
            }


            if (File != null && PhotoName != null)
            {
                user.PhotoName = PhotoName;
                byte[] fileData = File;
                if (!Directory.Exists(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Id}")))
                    Directory.CreateDirectory(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Id}"));
                //Compressor c = new Compressor();
                //fileData = c.Wrap(fileData);
                System.IO.File.WriteAllBytes(Path.Combine(HttpContext.Current.Server.MapPath(
                            $"~/Images/UserPhotos/{user.Id}/"), PhotoName
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



            // Invoke the "token" OWIN service to perform the login: /api/token
            // Ugly hack: I use a server-side HTTP POST because I cannot directly invoke the service (it is deeply hidden in the OAuthAuthorizationServerHandler class)
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
                //var responseString = await tokenServiceResponse.Content.ReadAsStringAsync();
                //var responseCode = tokenServiceResponse.StatusCode;
                //var responseMsg = new HttpResponseMessage(responseCode)
                //{
                //    Content = new StringContent(responseString, Encoding.UTF8, "application/json")
                //};
                return this.ResponseMessage(tokenServiceResponse);
            }


            // Invoke the "token" OWIN service to perform the login (POST /token)
            //    var testServer = TestServer.Create<Startup>();
            //    var requestParams = new List<KeyValuePair<string, string>>
            //    {
            //new KeyValuePair<string, string>("grant_type", "password"),
            //new KeyValuePair<string, string>("username", model.Username),
            //new KeyValuePair<string, string>("password", model.Password)
            //    };
            //    var requestParamsFormUrlEncoded = new FormUrlEncodedContent(requestParams);
            //    var tokenServiceResponse = await testServer.HttpClient.PostAsync(
            //        "/Token", requestParamsFormUrlEncoded);

            //    return this.ResponseMessage(tokenServiceResponse);
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

    
}
