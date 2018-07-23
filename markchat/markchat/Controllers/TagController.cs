using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Linq;
using System.Web;
using System.Web.Http;
using Microsoft.AspNet.Identity;
using markchat.Models;
using System.IO;
using MarkChat.DAL;
using MarkChat.DAL.Entities;
using System.Net;
using MarkChat.DAL.Repository;

namespace markchat.Controllers
{
    [Authorize]
    [RoutePrefix("api/Tag")]
    public class TagController : ApiController
    {
        private const string LocalLoginProvider = "Local";
        private ApplicationUserManager _userManager;
        private GenericUnitOfWork repository;

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
            if (tagChat == null)
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

            var dbNotifications = tagChat.Messages.OrderByDescending(x => x.PublicationDate).SkipWhile(x => x.Id != model.LastNotificationId).Take(10);

            var responceModel = dbNotifications.Select(x =>
            {
                var tmp = new TagChatMessageModel()
                {
                    Id = x.Id,
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
        public async Task<HttpResponseMessage> MakeInvitationRequestFromUserToTagChat(InvitationRequestFromUserModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var chat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);
            if (chat == null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "Chat doesn't exists");
            if (chat.Users.Contains(user))
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already exists in chat");
            if ((await repository.Repository<InvRequestToChat>().FindAllAsync(x => x.User.Id == user.Id && x.TagChat.Id == model.TagChatId && x.InvRequest.Confirmed == false && x.InvRequest.Denied == false)).FirstOrDefault() != null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "Request already exists in chat");
            await repository.Repository<InvRequestToChat>().AddAsync(new InvRequestToChat()
            {
                User = user,
                TagChat = chat,
                InvRequest = new InvRequest() { IsWatched = false, Confirmed = false, Denied = false, RequestDateTime = DateTime.Now }
            });

            await repository.SaveAsync();
            return Request.CreateResponse(HttpStatusCode.OK, "Your request sent");
        }

        [HttpPost]
        [Route("MakeInvitationRequestFromTagChatToUser")]
        public async Task<HttpResponseMessage> MakeInvitationRequestFromTagChatToUser(InvitationRequestFromTagChatModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var chat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);
            if (chat == null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "Chat doesn't exists");
            if (chat.OwnerUser.Id != user.Id)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "You dont have premission");
            if (chat.Users.Where(x => x.Id == model.UserId).FirstOrDefault() != null)
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
            return Request.CreateResponse(HttpStatusCode.OK, "Your request sent");
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
            if (user.Id != invReq.User.Id)
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Wrong request");
            if (chat == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat doesn't exists");
            if (chat.Users.Where(x => x.Id == user.Id).FirstOrDefault() != null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already exists in chat");
            if (invReq.InvRequest.Confirmed == true)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already confirmed");
            if (invReq.InvRequest.Denied == true)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already denied");
            var invUser = (await repository.Repository<InvRequestToChat>().FindAllAsync(x => x.TagChat.Id == invReq.TagChat.Id && x.User.Id == user.Id)).FirstOrDefault();
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
            return Request.CreateResponse(HttpStatusCode.OK);
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
            if (chat.OwnerUser.Id != user.Id)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "You don't have permission");
            if (chat.Users.Where(x => x.Id == invReq.User.Id).FirstOrDefault() != null)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already exists in chat");
            if (invReq.InvRequest.Confirmed == true)
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User already confirmed");
            if (invReq.InvRequest.Denied == true)
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
            return Request.CreateResponse(HttpStatusCode.OK);
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
            if (invReq.TagChat == null || invReq.TagChat.OwnerUser.Id != user.Id)
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
            return Request.CreateResponse(HttpStatusCode.OK);
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
            return Request.CreateResponse(HttpStatusCode.OK);
        }


        [HttpPost]
        [Route("GetAllNewRequestsFromChatsToUser")]
        public async Task<HttpResponseMessage> GetAllNewRequestsFromChatsToUser()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var newRequests = await repository.Repository<InvRequestToUser>().FindAllAsync(x => x.User.Id == user.Id && x.InvRequest.IsWatched == false && x.InvRequest.Confirmed == false && x.InvRequest.Denied == false);
            if (newRequests.Count() == 0)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "New requests not found");
            List<NewRequestsFromChatsToUserModel> model = new List<NewRequestsFromChatsToUserModel>();
            newRequests.ToList().ForEach(item => model.Add(
                new NewRequestsFromChatsToUserModel()
                {
                    TagChatId = item.TagChat.Id,
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
        public async Task<HttpResponseMessage> GetAllNewRequestsFromUsersToChat()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var newRequests = await repository.Repository<InvRequestToChat>().FindAllAsync(x => x.TagChat.OwnerUser.Id == user.Id && x.InvRequest.IsWatched == false && x.InvRequest.Confirmed == false && x.InvRequest.Denied == false);
            if (newRequests.Count() == 0)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "New requests not found");
            List<NewRequestsFromUsersToChatModel> model = new List<NewRequestsFromUsersToChatModel>();
            newRequests.ToList().ForEach(item => model.Add(
                new NewRequestsFromUsersToChatModel()
                {
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

            return Request.CreateResponse(HttpStatusCode.OK, new { Id = rootCategory.Id, Title = rootCategory.Title, Name = rootCategory.Name });
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
            if (parentCat.ChildCategories != null && parentCat.ChildCategories.FirstOrDefault(x => x.Name == model.NameNewCat) != null)
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
        [HttpGet]
        [Route("GetChatUsers")]
        public async Task<HttpResponseMessage> GetChatUsers(GetChatUsersModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var tagChat = await repository.Repository<TagChat>().FindByIdAsync(model.TagChatId);
            if (tagChat == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Tag Chat not found");
            }
            if (!tagChat.Users.Contains(user))
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
            return Request.CreateResponse(HttpStatusCode.OK, returnModel);
        }

        [HttpGet]
        [Route("GetUserTagChats")]
        public async Task<HttpResponseMessage> GetUserTagChats()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }

            Dictionary<int, string> chats = new Dictionary<int, string>();

            (await repository.Repository<TagChat>().FindAllAsync(x => x.Users.Select(y => y.Id).Contains(user.Id))).ToList()
                .ForEach(x => chats.Add(x.Id, x.Name));
            if (chats.Count() == 0)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "You dont have any chats yet");
            var responce = Request.CreateResponse<Dictionary<int, string>>(HttpStatusCode.OK, chats);

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
            List<TagChatModel> model = new List<TagChatModel>();
            allTagChats.ToList().ForEach(x => model.Add(
                new TagChatModel()
                {
                    Id = x.Id,
                    Name = x.Name,
                    OwnerUserId = x.OwnerUser.Id,
                    OwnerUserName = x.OwnerUser.FullName,
                    RootCategoryId = x.RootCategory.Id,
                    RootCategoryName = x.RootCategory.Name,
                }));
            return Request.CreateResponse(HttpStatusCode.OK, model);
        }

        [HttpGet]
        [Route("GetAllCurrencies")]
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
            Category category = await repository.Repository<Category>().FindByIdAsync(model.IdCategory);
            Currency currency = await repository.Repository<Currency>().FindByIdAsync(model.IdCurrency);

            //TODO - перевірки чи користувач чату
            //if (chat.)
            //{

            //}
            if (chat == null || category == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Category doesn't exists");
            }

            repository.Repository<Notification>().Add(new Notification()
            {
                Author = user,
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
                c.Notifications.Where(y => y.Price >= model.MinPrice && y.Price <= model.MaxPrice).ToList().ForEach(y => messages.Add(y));

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


            return Request.CreateResponse(HttpStatusCode.OK, "Chat created");
        }

        [HttpGet]
        [Route("GetMemberList")]
        public async Task<HttpResponseMessage> GetMemberList()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }


            Dictionary<string, UserInfo> userList = new Dictionary<string, UserInfo>();

            var users = await repository.Repository<ApplicationUser>().FindAllAsync(x => x.Id != user.Id);

            users.ToList().ForEach(x =>
            {
                var userInfo = new UserInfo
                {
                    FullName = x.FullName == "" ? x.FullName : x.PhoneNumber,
                };
                if (x.PhotoName != null)
                {
                    userInfo.PhotoName = x.PhotoName;
                    userInfo.Photo = File.ReadAllBytes(Path.Combine(HttpContext.Current.Server.MapPath(
                            $"~/Images/UserPhotos/{x.Id}/"), x.PhotoName));
                }
                else
                {
                    userInfo.PhotoName = "userPhoto.jpg";
                    userInfo.Photo = File.ReadAllBytes(HttpContext.Current.Server.MapPath(
                            $"~/Images/UserPhotos/userPhoto.png"));
                }
                userList.Add(x.Id, userInfo);
            });

            var responce = Request.CreateResponse<Dictionary<string, UserInfo>>(HttpStatusCode.OK, userList);

            return responce;
        }
    }
}