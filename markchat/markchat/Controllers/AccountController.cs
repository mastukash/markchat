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
        [Route("GetTagChatsByName")]
        public async Task<HttpResponseMessage> GetTagChatsByName(GetTagChatsByNameModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var tagChats = await repository.Repository<TagChat>().FindAllAsync(x => x.Name.Contains(model.TagChatName));
            if (tagChats==null || tagChats?.Count() == 0)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Tag Chats not found");
            }
            var returnModel = tagChats.Select(item => new {
                TagChatId = item.Id,
                TagChatName = item.Name,
                OwnerName = item.OwnerUser.FullName != "" ? item.OwnerUser.FullName : item.OwnerUser.PhoneNumber,
                RootId = item.RootCategory.Id

            });
            return Request.CreateResponse(HttpStatusCode.OK, returnModel);
        }

        [HttpPost]
        [Route("GetMinMaxPriceFromTagChat")]
        public async Task<HttpResponseMessage> GetMinMaxPriceFromTagChat(GetMinMaxPriceFromTagChatModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }

            var tagChat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);

            if (tagChat == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat doesn't exists");

            if (!tagChat.Users.Contains(user))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "You are not chat member");
            }
            var min = tagChat.Messages?.Min(x => x.Price);
            var max = tagChat.Messages?.Max(x => x.Price);
            return Request.CreateResponse(HttpStatusCode.OK, new { MinPrice = min, MaxPrice = max});
        }

        [HttpPost]
        [Route("GetLastMessages")]
        //returns last 10 chat messages 
        public async Task<HttpResponseMessage> GetLastMessages(GetLastMessagesModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");

            }

            var tagChat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);

            if(tagChat == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat doesn't exists");

            if (!tagChat.Users.Contains(user))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "You are not chat member");
            }

            var dbNotifications = tagChat.Messages.OrderByDescending(x => x.PublicationDate).Take(10);

            var responceModel = dbNotifications.Select(x =>
            {
                var tmp = new TagChatMessageModel()
                {
                    Id = x.Id,
                    IdAuthor = x.Author.Id,
                    AuthorFullName = x.Author?.FullName,
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
        [Route("GetOwnTagChats")]
        public async Task<HttpResponseMessage> GetOwnTagChats()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }

            var tagChats = await repository.Repository<TagChat>().FindAllAsync(x => x.OwnerUser.Id == user.Id);

            return Request.CreateResponse(HttpStatusCode.OK, tagChats.Select(x => new
            {
                x.Id,
                x.Name
            }));
        }

        [HttpGet]
        [Route("GetTemplates")]
        public async Task<HttpResponseMessage> GetTemplates()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }

            var templates = await repository.Repository<Template>().GetAllAsync();

            return Request.CreateResponse(HttpStatusCode.OK, templates.Select(x => new
            {
                Id = x.IdTemplate,
                x.Name
            }).ToList());
        }

        [HttpPost]
        [Route("CreateTagChatByTemplate")]
        public async Task<HttpResponseMessage> CreateTagChatByTemplate(CreateTagChatByTemplateModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            if (model.TagChatName == "")
            {
                return Request.CreateErrorResponse(HttpStatusCode.LengthRequired, "Chat Name cannot be empty");
            }
            var template = await repository.Repository<Template>().FindByIdAsync(model.IdTemplate);
            if (template == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Template doesn't exists");
            }


            var rootCategory = await repository.Repository<Category>().AddAsync(new Category() { Name = "Root", Title = model.TagChatName, ParentCategory = null });
            await repository.SaveAsync();

            var chat = await repository.Repository<TagChat>().AddAsync(new TagChat
            {
                Name = model.TagChatName,
                OwnerUser = user,
                RootCategory = rootCategory
            });

            chat.Users.Add(user);


            await repository.SaveAsync();

            AddCategories(rootCategory, template.Root.ChildCategories);

            return Request.CreateResponse(HttpStatusCode.OK, new { TagChatName = chat.Name, chat.Id});
        }

        private  void AddCategories(Category root , List<Category> childs)
        {
            foreach (var item in childs)
            {
                var tmp = new Category { Name = item.Name, Title = item.Title, ParentCategory = root };
                    repository.Repository<Category>().Add(tmp);
                repository.SaveChanges();
                if (item.ChildCategories.Count > 0)
                    AddCategories(tmp, item.ChildCategories);
            }

        }

        [HttpPost]
        [Route("IsChatOwner")]
        public async Task<HttpResponseMessage> IsChatOwner(IsChatOwnerModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }

            var tagChat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);

            if (tagChat == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat doesn't exists");

            return Request.CreateResponse(HttpStatusCode.OK, tagChat.OwnerUser.Id == user.Id);
        }


        [HttpPost]
        [Route("GetNextMessages")]
        //returns Next 10 chat messages 
        public async Task<HttpResponseMessage> GetNextMessages(GetNextMessagesModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }

            var tagChat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);

            if (tagChat == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat doesn't exists");

            if (!tagChat.Users.Contains(user))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "You are not chat member");
            }

            var dbNotifications = tagChat.Messages.OrderByDescending(x => x.PublicationDate).SkipWhile(x=>x.Id!=model.LastNotificationId)?.Skip(1);

            var responceModel = dbNotifications?.Take(10).Select(x =>
            {
                var tmp = new TagChatMessageModel()
                {
                    Id = x.Id,
                    IdAuthor = x.Author.Id,
                    AuthorFullName = x.Author?.FullName,
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

        [HttpPost]
        [Route("MakeInvitationRequestFromUserToTagChat")]
        public async Task<HttpResponseMessage> MakeInvitationRequestFromUserToTagChat(InvitationRequestFromUserToTagChatModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var chat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);
            if (chat == null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "Chat doesn't exists");
            if (chat.Users.Contains(user))
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already exists in chat");
            if ((await repository.Repository<InvRequestToChat>().FindAllAsync(x=>x.User.Id == user.Id && x.TagChat.Id == model.TagChatId && x.InvRequest.Confirmed == false && x.InvRequest.Denied == false)).FirstOrDefault()!= null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "Request already exists in chat");
            await repository.Repository<InvRequestToChat>().AddAsync(new InvRequestToChat()
            {
                User = user,
                TagChat = chat,
                InvRequest = new InvRequest() { IsWatched = false, Confirmed = false, Denied=false, RequestDateTime = DateTime.Now }
            });

            await repository.SaveAsync();
            return Request.CreateResponse(HttpStatusCode.OK, new { Finished = true, Message = "Your request sent" });
        }
        [HttpPost]
        [Route("MakeInvitationRequestsFromTagChatToUsers")]
        public async Task<HttpResponseMessage> MakeInvitationRequestsFromTagChatToUsers(InvitationRequestsFromTagChatToUsersModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null || model==null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var chat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);
            if (chat == null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "Chat doesn't exists");
            if (chat.OwnerUser.Id != user.Id)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "You dont have premission");
            foreach (var item in model.UsersId)
            {
                if (chat.Users.Where(x => x.Id == item).FirstOrDefault() != null)
                  continue;
                if ((await repository.Repository<InvRequestToUser>().FindAllAsync(x => x.User.Id == item && x.TagChat.Id == model.TagChatId && x.InvRequest.Confirmed == false && x.InvRequest.Denied == false)).FirstOrDefault() != null)
                    continue;

                await repository.Repository<InvRequestToUser>().AddAsync(new InvRequestToUser()
                {
                    User = await repository.Repository<ApplicationUser>().FindByIdAsync(item),
                    TagChat = chat,
                    InvRequest = new InvRequest() { IsWatched = false, Confirmed = false, Denied = false, RequestDateTime = DateTime.Now }
                });
                await repository.SaveAsync();
            }
            
            return Request.CreateResponse(HttpStatusCode.OK, new { Finished = true, Message = "Your requests have been sent" });
        }

        [HttpPost]
        [Route("MakeInvitationRequestFromTagChatToUser")]
        public async Task<HttpResponseMessage> MakeInvitationRequestFromTagChatToUser(InvitationRequestFromTagChatToUserModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var chat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);
            if (chat == null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "Chat doesn't exists");
            if (chat.OwnerUser.Id !=user.Id )
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "You dont have premission");
            if (chat.Users.Where(x=>x.Id == model.UserId).FirstOrDefault() != null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User already exists in chat");
            if ((await repository.Repository<InvRequestToUser>().FindAllAsync(x => x.User.Id == model.UserId && x.TagChat.Id == model.TagChatId && x.InvRequest.Confirmed == false && x.InvRequest.Denied == false)).FirstOrDefault() != null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "Request already exists in user");

            await repository.Repository<InvRequestToUser>().AddAsync(new InvRequestToUser()
            {
                User = await repository.Repository<ApplicationUser>().FindByIdAsync(model.UserId),
                TagChat = chat,
                InvRequest = new InvRequest() { IsWatched = false, Confirmed = false, Denied = false, RequestDateTime = DateTime.Now }
            });
            await repository.SaveAsync();
            return Request.CreateResponse(HttpStatusCode.OK, new { Finished = true, Message = "Your request has been sent" });
        }

        [HttpPost]
        [Route("AcceptInvitationRequestFromTagChatToUser")]
        public async Task<HttpResponseMessage> AcceptInvitationRequestFromTagChatToUser(AcceptInvitationRequestModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var invReq = await repository.Repository<InvRequestToUser>().FindByIdAsync(model.InvRequestId);
            if (invReq == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Request doesn't exists");
            var chat = await repository.Repository<TagChat>().FindByIdAsync(invReq.TagChat.Id);
            if(user.Id != invReq.User.Id)
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Wrong request");
            if (chat == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat doesn't exists");
            if (chat.Users.Where(x => x.Id == user.Id).FirstOrDefault() != null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already exists in chat");
            if (invReq.InvRequest.Confirmed == true)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already confirmed");
            if (invReq.InvRequest.Denied == true)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already denied");
            var invUser = (await repository.Repository<InvRequestToChat>().FindAllAsync(x=>x.TagChat.Id == invReq.TagChat.Id && x.User.Id == user.Id)).FirstOrDefault();
            if (invUser != null)
            {
                invUser.InvRequest.Confirmed = true;
                invUser.InvRequest.IsWatched = true;
                invUser.InvRequest.Denied = false;

                await repository.SaveAsync();
            }
            invReq.InvRequest.Confirmed = true;
            invReq.InvRequest.IsWatched = true;
            invReq.InvRequest.Denied = false;

            chat.Users.Add(user);
            await repository.SaveAsync();
            return Request.CreateResponse(HttpStatusCode.OK, new { Finished = true, Message = "Invitation request accepted" });
        }

        [HttpPost]
        [Route("AcceptInvitationRequestFromUserToTagChat")]
        public async Task<HttpResponseMessage> AcceptInvitationRequestFromUserToTagChat(AcceptInvitationRequestModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var invReq = await repository.Repository<InvRequestToChat>().FindByIdAsync(model.InvRequestId);
            if (invReq == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Request doesn't exists");
            var chat = await repository.Repository<TagChat>().FindByIdAsync(invReq.TagChat.Id);
            if (chat == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat doesn't exists");
            if(chat.OwnerUser.Id != user.Id)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "You don't have permission");
            if (chat.Users.Where(x => x.Id == invReq.User.Id).FirstOrDefault() != null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already exists in chat");
            if (invReq.InvRequest.Confirmed == true)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already confirmed");
            if (invReq.InvRequest.Denied== true)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already denied");
            var invUser = (await repository.Repository<InvRequestToUser>().FindAllAsync(x => x.TagChat.Id == invReq.TagChat.Id && x.User.Id == user.Id)).FirstOrDefault();
            if (invUser != null)
            {
                invUser.InvRequest.Confirmed = true;
                invUser.InvRequest.IsWatched = true;
                invUser.InvRequest.Denied = false;
                await repository.SaveAsync();
            }
            invReq.InvRequest.Confirmed = true;
            invReq.InvRequest.Denied = false;
            invReq.InvRequest.IsWatched = true;
            chat.Users.Add(invReq.User);
            await repository.SaveAsync();
            return Request.CreateResponse(HttpStatusCode.OK, new { Finished = true, Message = "Invitation request accepted" });
        }

        //------------------mastykash 13.07.2018--begin
        [HttpPost]
        [Route("DenyInvitationRequestFromUserToTagChat")]
        public async Task<HttpResponseMessage> DenyInvitationRequestFromUserToTagChat(DenyInvitationRequestModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var invReq = await repository.Repository<InvRequestToChat>().FindByIdAsync(model.InvRequestId);
            if (invReq == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Request doesn't exists");
            if (invReq.TagChat==null || invReq.TagChat.OwnerUser.Id != user.Id)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "You are not chat owner");

            if (invReq.TagChat.Users.Where(x => x.Id == invReq.User.Id).FirstOrDefault() != null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already exists in chat");
            if (invReq.InvRequest.Confirmed == true)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already confirmed");
            if (invReq.InvRequest.Denied == true)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already denied");
            var invUser = (await repository.Repository<InvRequestToUser>().FindAllAsync(x => x.TagChat.Id == invReq.TagChat.Id && x.User.Id == invReq.User.Id)).FirstOrDefault();
            if (invUser != null)
            {
                invUser.InvRequest.Denied = true;
                invUser.InvRequest.Confirmed = false;
                invUser.InvRequest.IsWatched = true;
                await repository.SaveAsync();
            }
            invReq.InvRequest.Denied = true;
            invReq.InvRequest.Confirmed = false;
            invReq.InvRequest.IsWatched = true;
            await repository.SaveAsync();
            return Request.CreateResponse(HttpStatusCode.OK, new { Finished = true, Message = "invitation request has been denied" });
        }

        [HttpPost]
        [Route("DenyInvitationRequestFromTagChatToUser")]
        public async Task<HttpResponseMessage> DenyInvitationRequestFromTagChatToUser(DenyInvitationRequestModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var invReq = await repository.Repository<InvRequestToUser>().FindByIdAsync(model.InvRequestId);
            if (invReq == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Request doesn't exists");
            var chat = invReq.TagChat;
            if (user.Id != invReq.User.Id)
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Wrong request");
            if (chat == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat doesn't exists");
            if (chat.Users.Where(x => x.Id == user.Id).FirstOrDefault() != null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "You are already a chat member");
            if (invReq.InvRequest.Confirmed == true)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "You are already confirmed");
            if (invReq.InvRequest.Denied == true)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "You are already denied");
            var invUser = (await repository.Repository<InvRequestToChat>().FindAllAsync(x => x.TagChat.Id == invReq.TagChat.Id && x.User.Id == user.Id)).FirstOrDefault();
            if (invUser != null)
            {
                invUser.InvRequest.Denied = true;
                invUser.InvRequest.Confirmed = false;
                invUser.InvRequest.IsWatched = true;
                await repository.SaveAsync();
            }
            invReq.InvRequest.Denied = true;
            invReq.InvRequest.Confirmed = false;
            invReq.InvRequest.IsWatched = true;

            await repository.SaveAsync();
            return Request.CreateResponse(HttpStatusCode.OK, new { Finished = true, Message = "invitation request has been denied" });
        }


        [HttpGet]
        [Route("GetAllNewRequestsFromChatsToUser")]
        public async Task<HttpResponseMessage> GetAllNewRequestsFromChatsToUser()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var newRequests = await repository.Repository<InvRequestToUser>().FindAllAsync(x => x.User.Id == user.Id && x.InvRequest.IsWatched == false && x.InvRequest.Confirmed == false && x.InvRequest.Denied == false);
            if(newRequests.Count()==0)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "New requests not found");
            List<NewRequestsFromChatsToUserModel> model = new List<NewRequestsFromChatsToUserModel>();
            newRequests.ToList().ForEach(item=> model.Add(
                new NewRequestsFromChatsToUserModel() {
                    TagChatId = item.TagChat.Id,
                    InvRequestId = item.InvRequest.Id,
                    TagChatName = item.TagChat.Name,
                    OwnerId = item.TagChat.OwnerUser.Id,
                    OwnerName = item.TagChat.OwnerUser.FullName,
                   // OwnerPhotoName = item.TagChat.OwnerUser.PhotoName,
                    OwnerPhoneNumber = item.TagChat.OwnerUser.PhoneNumber
                }));
            return Request.CreateResponse(HttpStatusCode.OK, model);
        }

        [HttpPost]
        [Route("GetAllNewRequestsFromUsersToChat")]
        public async Task<HttpResponseMessage> GetAllNewRequestsFromUsersToChat(RequestsFromUsersToChat requestsFromUsersToChat) 
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");

            var tagChat = await repository.Repository<TagChat>().FindByIdAsync(requestsFromUsersToChat.IdTagChat);
            if(tagChat.OwnerUser.Id != user.Id)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "You are not the owner of the tag chat");
            }
            var newRequests = await repository.Repository<InvRequestToChat>().FindAllAsync(x => x.TagChat.Id == requestsFromUsersToChat.IdTagChat && x.InvRequest.IsWatched == false && x.InvRequest.Confirmed == false && x.InvRequest.Denied == false);
            if (newRequests.Count() == 0)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "New requests not found");
            List<NewRequestsFromUsersToChatModel> model = new List<NewRequestsFromUsersToChatModel>();
            newRequests.ToList().ForEach(item => model.Add(
                new NewRequestsFromUsersToChatModel()
                {
                    InvRequestId = item.InvRequest.Id,
                    TagChatId = item.TagChat.Id,
                    TagChatName = item.TagChat.Name,
                    UserId = item.User.Id,
                    UserName = item.User.UserName,
                   // UserPhotoName = item.User.PhotoName,
                    UserPhoneNumber = item.User.PhoneNumber
                }));
            return Request.CreateResponse(HttpStatusCode.OK, model);
        }


        [HttpPost]
        [Route("GetRootCategoryByTagChatId")]
        public async Task<HttpResponseMessage> GetRootCategoryByTagChatId(GetRootCategoryByTagChatIdModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }

            var tagChat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);
            if (tagChat == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Tag Chat doesn't exists");
            }
            var rootCategory = tagChat.RootCategory;

            return Request.CreateResponse(HttpStatusCode.OK, new { rootCategory.Id, rootCategory.Title, rootCategory.Name });
        }

        [HttpPost]
        [Route("CreateSubCategory")]
        public async Task<HttpResponseMessage> CreateSubCategory(CreateSubCategoryModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var parentCat = await repository.Repository<Category>().FindByIdAsync(model.ParentCatId);
            if (parentCat == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Categories not found");
            }
            Category category = parentCat;
            while (category.ParentCategory != null)
                category = category.ParentCategory;


            var tagChat = category.ChatRoot;

            if (tagChat.OwnerUser.Id != user.Id)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "You are not the owner of the tag chat");
            }
            if(parentCat.ChildCategories!=null && parentCat.ChildCategories.FirstOrDefault(x=>x.Name == model.NameNewCat)!=null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "Category with this name already exists");
            }


            var childCategory = await repository.Repository<Category>().AddAsync(new Category
            {
                Name = model.NameNewCat,
                Title = model.TitleNewCat,                
            });

            childCategory.ParentCategory = parentCat;

            //parentCat.ChildCategories.Add(new Category
            //{
            //    Name = model.NameNewCat,
            //    Title = model.TitleNewCat
            //});
            await repository.SaveAsync();
            return Request.CreateResponse(HttpStatusCode.OK, "Category added");
        }

        /// <summary>
        /// Отримати усіх користувачів чату
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("GetChatUsers")]
        public async Task<HttpResponseMessage> GetChatUsers(GetChatUsersModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var tagChat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);
            if(tagChat== null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Tag Chat not found");
            }
            if(!tagChat.Users.Contains(user))
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "You are not chat member");

            var returnModel = new List<ChatUserModel>();
            tagChat.Users.ToList().ForEach(item => returnModel.Add(
                new ChatUserModel()
                {
                    UserId = item.Id,
                    UserName = item.UserName,
                    UserPhoneNumber = item.PhoneNumber,
                    //UserPhotoName = item.PhotoName
                }));
             return  Request.CreateResponse(HttpStatusCode.OK, returnModel);
        }
        
        [HttpGet]
        [Route("GetUserTagChats")]
        public async Task<HttpResponseMessage> GetUserTagChats()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());    
            
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized,"User doesn't exists");
            }

            List<ChatInfoModel> chats =  new List<ChatInfoModel>();

            (await repository.Repository<TagChat>().FindAllAsync(x => x.Users.Select(y=>y.Id).Contains(user.Id))).ToList()
                .ForEach(x=> chats.Add(new ChatInfoModel { Id = x.Id, Name = x.Name }));
            if(chats.Count()==0)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound,"You dont have any chats yet");
            var responce = Request.CreateResponse(HttpStatusCode.OK, chats);

            return responce;
        }      

        [HttpGet]
        [Route("GetAllTagChats")]
        public async Task<HttpResponseMessage> GetAllTagChats()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var allTagChats = await repository.Repository<TagChat>().GetAllAsync();
            if (allTagChats == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No chats found");
            List<TagChatModel> model = new List<TagChatModel>();
            allTagChats.ToList().ForEach(x => model.Add(
                new TagChatModel()
                {
                    Id = x.Id,
                    Name = x.Name,
                    OwnerUserId = x.OwnerUser.Id,
                    OwnerUserName = x.OwnerUser.FullName != "" ? x.OwnerUser.FullName : x.OwnerUser.PhoneNumber,
                    RootCategoryId = x.RootCategory.Id,
                    RootCategoryName = x.RootCategory.Name,
                }));
            return Request.CreateResponse(HttpStatusCode.OK, model);
        }

        [HttpGet]
        [Route("GetAllTagChats")]
        public async Task<HttpResponseMessage> GetListTagChatsByName(ListTagChatsByNameModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var allTagChatsByName = (await repository.Repository<TagChat>().GetAllAsync())?.Where(x => x.Name == model.TagChatName); 
            if(allTagChatsByName == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No chats found");
            List<TagChatModel> returnModel = new List<TagChatModel>();
            allTagChatsByName.ToList().ForEach(x => returnModel.Add(
                new TagChatModel()
                {
                    Id = x.Id,
                    Name = x.Name,
                    OwnerUserId = x.OwnerUser.Id,
                    OwnerUserName = x.OwnerUser.FullName == "" ? x.OwnerUser.FullName : x.OwnerUser.PhoneNumber,
                    RootCategoryId = x.RootCategory.Id,
                    RootCategoryName = x.RootCategory.Name,
                }));
            return Request.CreateResponse(HttpStatusCode.OK, returnModel);
        }

        [HttpGet]
        [Route("GetAllCurrencies")]
        //[TokenValidation]
        public async Task<HttpResponseMessage> GetAllCurrencies()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var allCurrencies = await repository.Repository<Currency>().GetAllAsync();

            List<CurrencyModel> model = new List<CurrencyModel>();
            allCurrencies.ToList().ForEach(x => model.Add(
                new CurrencyModel()
                {
                    Id = x.Id,
                    Name = x.Name,
                    Symbol = x.Symbol
                }));
            return Request.CreateResponse(HttpStatusCode.OK, model);
        }


        [HttpGet]
        [Route("GetAllUsers")]
        public async Task<HttpResponseMessage> GetAllUsers()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var allUsers = await repository.Repository<ApplicationUser>().FindAllAsync(x=> x.Id != user.Id);
            
            return Request.CreateResponse(HttpStatusCode.OK, allUsers.Select(x=> new
            {
                x.Id,
                OwnerUserName = x.FullName == "" ? x.FullName : x.PhoneNumber,
            }));
        }


        [HttpPost]
        [Route("GetAllUserForTagChatInvitation")]
        public async Task<HttpResponseMessage> GetAllUserForTagChatInvitation(GetInvitationTagChatModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var tagChat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);
            var chatMembers = tagChat.Users.Select(u => u.Id);
            var allUsers = await repository.Repository<ApplicationUser>().FindAllAsync(x => x.Id != user.Id && !chatMembers.Contains(x.Id));

            return Request.CreateResponse(HttpStatusCode.OK, allUsers.Select(x => new
            {
                x.Id,
                OwnerUserName = x.FullName == "" ? x.FullName : x.PhoneNumber,
            }));
        }




        [HttpPost]
        [Route("AddNotification")]
        public async Task<HttpResponseMessage> AddNotification(AddNotificationBindingModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null || model == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            TagChat chat = await repository.Repository<TagChat>().FindByIdAsync(model.IdChat);

            if (chat == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat doesn't exists");
            }

            Category category = await repository.Repository<Category>().FindByIdAsync(model.IdCategory);

            if (category == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Category doesn't exists");
            }

            Currency currency = await repository.Repository<Currency>().FindByIdAsync(model.IdCurrency);

            if (currency == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Currency doesn't exists");
            }


            var tmpCategory = category;
            while (tmpCategory.ChatRoot == null)
                tmpCategory = tmpCategory.ParentCategory;


            if(tmpCategory.ChatRoot.Id != chat.Id)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Wrong Category");
            }


            if(chat.Users.Where(x=>x.Id == user.Id).Select(x=>x).FirstOrDefault() == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "You are not a chat member.");
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

            return Request.CreateResponse(HttpStatusCode.OK, new { Finished = true, Message = "Notification has been added" });
        }
        //добавати повернення списку валют
        [HttpPost]
        [Route("FindNotifications")]
        public async Task<HttpResponseMessage> FindNotifications(FindNotificationBindingModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null || model == null)

            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
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
                c.Notifications.Where(y=>y.Price>=model.MinPrice && y.Price<=model.MaxPrice && y.PublicationDate>= model.FromDate && y.PublicationDate<=model.ToDate)
                .ToList().ForEach(y => messages.Add(y));

                if (c.ChildCategories.Count > 0)
                    c.ChildCategories.ForEach(x => search(x));
            };

            search(category);

            var responceModel = messages.OrderBy(x=>x.PublicationDate).Select(x =>
            {
                var tmp = new TagChatMessageModel()
                {
                    Id = x.Id,
                    IdAuthor = x.Author.Id,
                    AuthorFullName = x.Author?.FullName,
                    AuthorPhoto = x.Author?.PhotoName,
                    Currency = x.Currency?.Symbol,
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
        public async Task<HttpResponseMessage> GetRootCategories(GetRootCategoriesModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null || model.TagChatId == 0)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }

            TagChat chat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);

            if (chat == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat doesn't exists");
            }



            List<CategoryInfo> childCategories = new List<CategoryInfo>();
            chat.RootCategory.ChildCategories.ForEach(x => childCategories.Add(new CategoryInfo { CategoryId = x.Id, Name = x.Name, Title = x.Title }));


            return Request.CreateResponse(HttpStatusCode.OK, childCategories);

        }

        [HttpPost]
        [Route("GetChildCategoriesById")]
        public async Task<HttpResponseMessage> GetChildCategoriesById(GetChildCategoriesById model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null || model.ParentCategoryId == 0)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }

            Category category = await repository.Repository<Category>().FindByIdAsync(model.ParentCategoryId);

            if (category == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Category doesn't exists");
            }


            List<CategoryInfo> childCategories = new List<CategoryInfo>();
            category.ChildCategories.ForEach(x => childCategories.Add(new CategoryInfo { CategoryId = x.Id, Name = x.Name, Title = x.Title }));


            var responce = Request.CreateResponse(HttpStatusCode.OK, childCategories);

            return responce;
        }

        [HttpPost]
        [Route("CreateTagChat")]
        public async Task<HttpResponseMessage> CreateTagChat(CreateTagChatModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            if (model.TagChatName == "")
            {
                return Request.CreateErrorResponse(HttpStatusCode.LengthRequired, "Chat Name cannot be empty");
            }


            var rootCategory = await repository.Repository<Category>().AddAsync(new Category() { Name = "Root", Title = model.TagChatName, ParentCategory = null });
            await repository.SaveAsync();

            var chat = await repository.Repository<TagChat>().AddAsync(new TagChat
            {
                Name = model.TagChatName,
                OwnerUser = user,
                RootCategory = rootCategory
            });

            chat.Users.Add(user);


            await repository.SaveAsync();
                 

            return Request.CreateResponse(HttpStatusCode.OK, new { TagChatName = chat.Name, chat.Id });
        }

        [HttpPost]
        [Route("GetMemberList")]
        public async Task<HttpResponseMessage> GetMemberList(ListUsersTagChatModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }

            List<GetMemberModel> returnModel = new List<GetMemberModel>();
            //Dictionary<string, UserInfo> userList = new Dictionary<string, UserInfo>();

            var users = (await repository.Repository<TagChat>().FindByIdAsync(model.IdTagChat))?.Users;
            if(users==null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Users doesn't exists");

            users?.ToList().ForEach(x =>
            {
                var userInfo = new GetMemberModel
                {
                    FullName = x.FullName == "" ? x.FullName : x.PhoneNumber,
                };
                if (x.PhotoName != null)
                {
                    userInfo.PhotoName = x.PhotoName;
                    if(File.Exists(Path.Combine(HttpContext.Current.Server.MapPath(
                            $"~/Images/UserPhotos/{x.Id}/"), x.PhotoName)))
                    userInfo.Photo = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(HttpContext.Current.Server.MapPath(
                            $"~/Images/UserPhotos/{x.Id}/"), x.PhotoName)));
                    else
                    {
                        userInfo.PhotoName = "userPhoto.jpg";
                        userInfo.Photo = Convert.ToBase64String(File.ReadAllBytes(HttpContext.Current.Server.MapPath(
                                $"~/Images/UserPhotos/userPhoto.png")));
                    }

                }
                else
                {
                    userInfo.PhotoName = "userPhoto.jpg";
                    userInfo.Photo = Convert.ToBase64String(File.ReadAllBytes(HttpContext.Current.Server.MapPath(
                            $"~/Images/UserPhotos/userPhoto.png")));
                }
                userInfo.UserId = x.Id;
                returnModel.Add(userInfo);
            });
            var responce = Request.CreateResponse(HttpStatusCode.OK, returnModel);
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

            return Ok(new { confirmationCode.Token });
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
                byte[] fileData = Convert.FromBase64String(model.File);
                if (!Directory.Exists(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Id}")))
                    Directory.CreateDirectory(Path.Combine(HttpContext.Current.Server.MapPath("~/Images/UserPhotos/"), $"{user.Id}"));
                //Compressor c = new Compressor();
                //fileData = c.Wrap(fileData);
                var pathToFile = Path.Combine(HttpContext.Current.Server.MapPath(
                            $"~/Images/UserPhotos/{user.Id}/"), model.PhotoName
                            );
                //if(Path.GetExtension(pathToFile)!="jpg")
                //{
                //    //Або інші типи (а ще краще поставити валідацію!!!)
                //}
                System.IO.File.WriteAllBytes(pathToFile, fileData);

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
            repository = new GenericUnitOfWork();

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
                LoginProvider = externalLogin?.LoginProvider
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
                        state
                    }),
                    State = state
                };
                logins.Add(login);
            }

            return logins;
        }


        [HttpGet]
        [Route("GetUserId")]
        public async Task<HttpResponseMessage> GetUserId()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            return Request.CreateResponse(HttpStatusCode.OK, new { UserId = user.Id });
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

            dbUser.AccessFailedCount = 0;
            await repository.SaveAsync();
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
                IList<Claim> claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, ProviderKey, null, LoginProvider)
                };

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
