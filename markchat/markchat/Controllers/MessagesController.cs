using markchat.Hubs;
using markchat.Models;
using MarkChat.DAL.ChatEntities;
using MarkChat.DAL.Entities;
using MarkChat.DAL.Repository;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace markchat.Controllers
{
    [Authorize]
    [RoutePrefix("api/Messages")]
    public class MessagesController : ApiController
    {
        private GenericUnitOfWork repository;

        public MessagesController()
        {
            repository = new GenericUnitOfWork();
        }

        private string GetUrlUserPhoto(ApplicationUser user)
        {
            return File.Exists(Path.Combine(HttpContext.Current.Server.MapPath(
                            $"~/Images/UserPhotos/{user.PhotoName}/")))
                            ? Request.RequestUri.GetLeftPart(UriPartial.Authority) + $"/Images/UserPhotos/{user.Id}/{user.PhotoName}"
                            : Request.RequestUri.GetLeftPart(UriPartial.Authority) + $"/Images/UserPhotos/userPhoto.png";
        }

        private string GetUrlAttachment(AttachmentMsg attachment)
        {
            return Request.RequestUri.GetLeftPart(UriPartial.Authority) + $"/api/Messages/GetMsgAttachment?ChatRoomId={attachment.Message.ChatRoom.Id}&AttachmentName={attachment.FileName}";
        }

        [HttpPost]
        [Route("SendMessage")]
        //TODO!!!
        //заточений для роботи 2 користувачів в чаті, якщо буде більше то будуть проблеми!!!!
        //add SignalR
        public async Task<HttpResponseMessage> SendMessage(SendMessageModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var chatRoom = await repository.Repository<ChatRoom>().FindByIdAsync(model.ChatRoomId);
            if (chatRoom == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat Room not found");
            var chatRoomMembers = (await repository.Repository<ChatRoomMember>().FindAllAsync(x => x.ChatRoom.Id == chatRoom.Id));
            if (chatRoomMembers == null || chatRoomMembers.FirstOrDefault(x => x.User.Id == user.Id) == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "You are not a member of this chat room");
            var msg = new Message { Body = model.Body, DateTime = DateTime.Now };
            msg.DateTime = DateTime.Now;
            msg.ChatRoom = chatRoom;
            //? шось тут не то. навіщо посилання в повідомлення на ChatRoomMember і на кімнату , чи все так...
            msg.ChatRoomMember = chatRoomMembers.FirstOrDefault(x => x.User.Id == user.Id);
            //chatRoom.Messages.Add(msg);
            //той хто відправив повідомлення - автоматично його прочитав
            foreach (var item in chatRoomMembers)
            {
                await repository.Repository<ReadedMsg>().AddAsync(new ReadedMsg
                {
                    ChatRoomMember = item,
                    Message = msg,
                    Readed = item.User.Id == user.Id ? true : false,
                    RDateTime = item.User.Id == user.Id ? DateTime.Now : new DateTime(2000, 01, 01),
                });
            }

            //TODO to AttachmentModel
            msg.Attachments = new List<AttachmentMsg>();
            if (model.Attachments != null && model.Attachments.Count > 0 && model.Attachments[0] != null)
            {
                //поставити обмеження на розмір атачментів
                // і тримати тільки останні 30 повідомлень кімнати, під час написання 30-го усі попередні терти з бд
                for (int i = 0; i < model.Attachments.Count; i++)
                {
                    var pathToFile = Path.Combine(HttpContext.Current.Server.MapPath(
                        $"~/App_Data/FilesChatRooms/{chatRoom.Id}/"),
                        model.AttachmentsNames[i]);
                    byte[] fileData = Convert.FromBase64String(model.Attachments[i]);
                    System.IO.File.WriteAllBytes(pathToFile, fileData);
                    msg.Attachments.Add(new AttachmentMsg() { FileName = model.AttachmentsNames[i] });
                }
            }
            await repository.SaveAsync();

            var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
            foreach (var item in chatRoom.ChatRoomMembers)
            {
                if (item.User.Id == user.Id)
                    continue;
                if (ChatHub.Users.ContainsKey(item.User.Id))
                    //hubContext.Clients.Client(ChatHub.Users[item.User.Id]).SendMsg(msg.Id, msg.ChatRoom.Id,msg.ChatRoomMember.User.Id, msg.ChatRoomMember.User.UserName, msg.Body);
                    hubContext.Clients.Client(ChatHub.Users[item.User.Id]).sendMsg(msg.Id, msg.ChatRoom.Id, msg.ChatRoomMember.User.Id, msg.ChatRoomMember.User.UserName, msg.Body);
            }

            var responce = Request.CreateResponse(HttpStatusCode.OK, "Success");
            return responce;
        }

        [HttpGet]
        [Route("GetAttachmentById")]
        public async Task<HttpResponseMessage> DownloadAttachmentById(GetAttachmentByIdModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var att = await repository.Repository<AttachmentMsg>().FindByIdAsync(model.AttachmentId);
            if (att == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Attachment not found");
            if (user.ChatRoomsMember.FirstOrDefault(x => x.ChatRoom.Id == att.Message.ChatRoom.Id) == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "You do not have permissions");
            }
            var returnModel = new
            {
                Id = att.Id,
                Name = att.FileName,
                File = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(HttpContext.Current.Server.MapPath(
                            $"~/App_Data/FilesChatRooms/{att.Message.ChatRoom.Id}/"), att.FileName)))
            };
            var responce = Request.CreateResponse(HttpStatusCode.OK, returnModel);
            return responce;
        }

        [HttpPost]
        [Route("GetMessageById")]
        public async Task<HttpResponseMessage> GetMessageById(GetMessageByIdModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            var msg = await repository.Repository<Message>().FindByIdAsync(model.MessageId);
            if (msg == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Message not found");
            var member = user.ChatRoomsMember.FirstOrDefault(x => x.ChatRoom.Id == msg.ChatRoom.Id);
            if (member == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "You do not have permissions");
            }
            var returnModel = new GetMessagedModel
            {
                FromUserId = msg.ChatRoomMember.User.Id,
                FromUserName = msg.ChatRoomMember.User.FullName != "" ? msg.ChatRoomMember.User.FullName : msg.ChatRoomMember.User.PhoneNumber,
                Body = msg.Body,
                ChatRoomId = msg.ChatRoom.Id,
            };
            returnModel.Attachments = new List<AttachmentModel>();
            if (msg.Attachments.Count > 0)
            {
                for (int i = 0; i < msg.Attachments.Count; i++)
                {
                    var att = new AttachmentModel
                    {
                        Id = msg.Attachments[i].Id,
                        Name = msg.Attachments[i].FileName,
                        File = GetUrlAttachment (msg.Attachments[i])
                        //File = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(HttpContext.Current.Server.MapPath(
                        //    $"~/App_Data/FilesChatRooms/{msg.ChatRoom.Id}/"), msg.Attachments[i].FileName)))
                    };

                    returnModel.Attachments.Add(att);
                }
            }
            var r = msg.Readeds.Where(x => x.ChatRoomMember.Id == member.Id)?.FirstOrDefault();
            if (r != null)
                r.Readed = true;
            await repository.SaveAsync();
            var responce = Request.CreateResponse(HttpStatusCode.OK, returnModel);
            return responce;
        }
        //TODO!!!
        [HttpPost]
        [Route("GetLast30MessagesToUserFormChatRoom")]
        public async Task<HttpResponseMessage> GetLast30MessagesToUserFormChatRoom(GetLast30MessagesToUserFormChatRoomModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var chatRoom = await repository.Repository<ChatRoom>().FindByIdAsync(model.ChatRoomId);
            if (chatRoom == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Chat Room not found");
            }
            var listMsgs = new List<ChatRoomMessageModel>();
            string pathToDir = Path.Combine(HttpContext.Current.Server.MapPath($"~/App_Data/FilesChatRooms/{chatRoom.Id}/"));
            //TODO здійснити перевірку чи правильно написано!!!
            if (chatRoom.Messages == null || chatRoom.Messages.Count == 0)
                return Request.CreateResponse(HttpStatusCode.OK, listMsgs);
            var msgs = chatRoom.Messages.OrderByDescending(x => x.Id).Take(30).ToList();
            for (int i = 0; i < msgs.Count; i++)
            {
                var returnModel = new ChatRoomMessageModel
                {
                    Id = msgs[i].Id,
                    Body = msgs[i].Body,
                    UserId = msgs[i].ChatRoomMember.User.Id,
                    UserName = msgs[i].ChatRoomMember.User.UserName,
                    DateTime = msgs[i].DateTime
                };
                listMsgs.Add(returnModel);
                //чи потрібно передавати усі файли??
                //чи краще зробити якусь кнопку, щоб він міг окремим методом той файл викачати
                if (msgs[i].Attachments != null && msgs[i].Attachments.Count > 0)
                {
                    //returnModel.Attachments = new List<string>(msgs[i].Attachments.Select(x => Convert.ToBase64String(File.ReadAllBytes($"{pathToDir}{x.FileName}"))));// перевірити чи працює!!!!
                    returnModel.AttachmentsId = new List<int>(msgs[i].Attachments.Select(x => x.Id) as IEnumerable<int>);
                    returnModel.AttachmentsNames = new List<string>(msgs[i].Attachments.Select(x => x.FileName) as IEnumerable<string>);// перевірити чи працює!!!!
                    returnModel.AttachmentUrls = new List<string>(msgs[i].Attachments.Select(x => GetUrlAttachment(x)) as IEnumerable<string>);
                }
                var r = msgs[i].Readeds.FirstOrDefault(x => x.ChatRoomMember.User.Id == user.Id);
                if (r != null && r.Readed != true)
                {
                    r.Readed = true;
                    r.RDateTime = DateTime.Now;
                }
            }
            //var rmsgs = msgs.ForEach(item=>item.Readeds.Select(r=>r.ChatRoomMember.User.Id).FirstOrDefault()==user.Id.ToList().ForEach(x=>x.re)
            //    Select(x => x.Readeds.FirstOrDefault(item => item.ChatRoomMember.User.Id == user.Id && item.Readed == false)).Take(30);
            //foreach(var item in rmsgs)
            //{

            //}
            await repository.SaveAsync();
            return Request.CreateResponse(HttpStatusCode.OK, listMsgs);
        }

        //TODO через SignalR написати метод для отримання 1 повідомлення!!!
        [HttpPost]
        [Route("GetNext30MessagesToUserFormChatRoom")]
        public async Task<HttpResponseMessage> GetNext30MessagesToUserFormChatRoom(GetNext30MessagesToUserFormChatRoomModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var chatRoom = await repository.Repository<ChatRoom>().FindByIdAsync(model.ChatRoomId);
            if (chatRoom == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat Room not found");
            }
            var listMsgs = new List<ChatRoomMessageModel>();
            string pathToDir = Path.Combine(HttpContext.Current.Server.MapPath($"~/App_Data/FilesChatRooms/{chatRoom.Id}/"));
            //TODO здійснити перевірку чи правильно написано!!!
            if (chatRoom.Messages == null || chatRoom.Messages.Count == 0)
                return Request.CreateResponse(HttpStatusCode.OK, listMsgs);
            var msgs = chatRoom.Messages.Where(x => x.Id < model.MsgId)?.OrderByDescending(x => x.Id)?.Take(30)?.ToList();
            if (msgs == null || msgs.Count == 0)
                return Request.CreateResponse(HttpStatusCode.OK, listMsgs);
            for (int i = 0; i < msgs.Count; i++)
            {
                var returnModel = new ChatRoomMessageModel
                {
                    Id = msgs[i].Id,
                    Body = msgs[i].Body,
                    UserId = msgs[i].ChatRoomMember.User.Id,
                    UserName = msgs[i].ChatRoomMember.User.UserName,
                    DateTime = msgs[i].DateTime
                };
                listMsgs.Add(returnModel);
                //чи потрібно передавати усі файли??
                //чи краще зробити якусь кнопку, щоб він міг окремим методом той файл викачати
                if (msgs[i].Attachments != null && msgs[i].Attachments.Count > 0)
                {
                    //returnModel.Attachments = new List<string>(msgs[i].Attachments.Select(x => Convert.ToBase64String(File.ReadAllBytes($"{pathToDir}{x.FileName}"))));// перевірити чи працює!!!!
                    returnModel.AttachmentsId = new List<int>(msgs[i].Attachments.Select(x => x.Id) as IEnumerable<int>);
                    returnModel.AttachmentsNames = new List<string>(msgs[i].Attachments.Select(x => x.FileName) as IEnumerable<string>);// перевірити чи працює!!!!
                    returnModel.AttachmentUrls = new List<string>(msgs[i].Attachments.Select(x => GetUrlAttachment(x)) as IEnumerable<string>);
                }
                var r = msgs[i].Readeds.FirstOrDefault(x => x.ChatRoomMember.User.Id == user.Id);
                if (r != null && r.Readed != true)
                {
                    r.Readed = true;
                    r.RDateTime = DateTime.Now;
                }
            }

            await repository.SaveAsync();
            return Request.CreateResponse(HttpStatusCode.OK, listMsgs);
        }
        [HttpGet]
        [Route("GetAllPrivateUserChatRooms")]
        public async Task<HttpResponseMessage> GetAllPrivateUserChatRooms()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var chatRoomsMember = await repository.Repository<ChatRoomMember>().FindAllAsync(x => x.User.Id == user.Id);
            var privateChatRoomsMember = chatRoomsMember?.Where(item => item.ChatRoom.ChatRoomMembers.Count == 2);
            if (privateChatRoomsMember == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Not found private chat Rooms");
            }
            var model = new List<PrivateUserChatRoomModel>();
            privateChatRoomsMember.OrderByDescending(x => x.Id).ToList().ForEach(item => model.Add(
                new PrivateUserChatRoomModel
                {
                    ChatRoomId = item.ChatRoom.Id,
                    FriendUserId = item.ChatRoom.ChatRoomMembers.FirstOrDefault(x => x.User.Id != user.Id)?.User.Id,
                    FriendUserName = item.ChatRoom.ChatRoomMembers.FirstOrDefault(x => x.User.Id != user.Id)?.User.UserName
                }));
            return Request.CreateResponse(HttpStatusCode.OK, model);
        }

        [HttpGet]
        [Route("GetAllUsersWithoutPrivateChatRoom")]
        public async Task<HttpResponseMessage> GetAllUsersWithoutPrivateChatRoom()
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var chatRoomsMember = await repository.Repository<ChatRoomMember>().FindAllAsync(x => x.User.Id == user.Id);
            var privateChatRoomsMember = chatRoomsMember?.Where(item => item.ChatRoom.ChatRoomMembers.Count == 2);
            var allUsers = await repository.Repository<ApplicationUser>().GetAllAsync();
            var contain = false;
            var model = new List<GetAllUsersWithoutPrivateChatRoomModel>();
            //var results = new List<ApplicationUser>();
            //(await repository.Repository<ChatRoom>().FindAllAsync(x=> x.ChatRoomMembers.Select(member=> member.User.Id).Contains(user.Id)))
            //    .Select(x=>x.ChatRoomMembers)
            //    .Select(x=>x.Select(u=>u.User)).Distinct().ToList().ForEach(x=> x.ToList().ForEach(u=> results.Add(u)));

            //results.Distinct().ToList().ForEach(item => model.Add(new GetAllUsersWithoutPrivateChatRoomModel
            //{
            //    UserId = item.Id,
            //    UserName = item.FullName == "" ? item.FullName : item.PhoneNumber
            //}));


            foreach (var u in allUsers)
            {
                contain = false;
                if (u.Id == user.Id)
                    continue;
                foreach (var member in privateChatRoomsMember)
                {
                    if (member.ChatRoom.ChatRoomMembers.Select(x => x.User.Id).Contains(u.Id))
                    {
                        contain = true;
                        break;
                    }
                }
                if (!contain)
                {
                    model.Add(new GetAllUsersWithoutPrivateChatRoomModel
                    {
                        UserId = u.Id,
                        UserName = u.FullName == "" ? u.FullName : u.PhoneNumber
                    });
                }
            }

            return Request.CreateResponse(HttpStatusCode.OK, model);
        }


        [HttpPost]
        [Route("GetIdUserByIdPrivateChatRoom")]
        public async Task<HttpResponseMessage> GetIdUserByIdPrivateChatRoom(GetIdUserByIdPrivateChatRoomModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var chatRoom = await repository.Repository<ChatRoom>().FindByIdAsync(model.ChatRoomId);
            if (chatRoom == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Chat Room not found");
            }
            var chatRoomMembers = (await repository.Repository<ChatRoomMember>().FindAllAsync(x => x.ChatRoom.Id == chatRoom.Id));
            if (chatRoomMembers == null || chatRoomMembers.FirstOrDefault(x => x.User.Id == user.Id) == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "You are not a member of this chat room");
            if (chatRoomMembers.Count() != 2)
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "This is not a private chat");
            var friend = chatRoomMembers.FirstOrDefault(item => item.User.Id != user.Id)?.User;

            //string photoName = "";
            //string photo = "";
            //if (friend.PhotoName != null)
            //{
            //    photoName = friend.PhotoName;
            //    if (File.Exists(Path.Combine(HttpContext.Current.Server.MapPath(
            //            $"~/Images/UserPhotos/{friend.Id}/"), friend.PhotoName)))
            //        photo = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(HttpContext.Current.Server.MapPath(
            //                $"~/Images/UserPhotos/{friend.Id}/"), friend.PhotoName)));
            //    else
            //    {
            //        photoName = "userPhoto.jpg";
            //        photo = Convert.ToBase64String(File.ReadAllBytes(HttpContext.Current.Server.MapPath(
            //                $"~/Images/UserPhotos/userPhoto.png")));
            //    }

            //}
            //else
            //{
            //    photoName = "userPhoto.jpg";
            //    photo = Convert.ToBase64String(File.ReadAllBytes(HttpContext.Current.Server.MapPath(
            //            $"~/Images/UserPhotos/userPhoto.png")));
            //}

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                UserId = friend.Id,
                UserName = friend.FullName == "" ? friend.FullName : friend.PhoneNumber,
                PhotoName = GetUrlUserPhoto(friend) == Request.RequestUri.GetLeftPart(UriPartial.Authority) + $"/Images/UserPhotos/userPhoto.png"? "userPhone.png":friend.PhoneNumber,
                Photo = GetUrlUserPhoto(friend)
            });
        }

        [HttpPost]
        [Route("GetChatRoomIdByUserId")]
        public async Task<HttpResponseMessage> GetChatRoomIdByUserId(GetChatRoomIdByUserIdModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            ApplicationUser friend = await repository.Repository<ApplicationUser>().FindByIdAsync(model.UserId);
            if (user == null || friend == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var chatRoomsMember = await repository.Repository<ChatRoomMember>().FindAllAsync(x => x.User.Id == user.Id);
            //ой хз хз
            var privateChatRoomWithUser = chatRoomsMember?.FirstOrDefault(item => item.ChatRoom.ChatRoomMembers.Count == 2 && item.ChatRoom.ChatRoomMembers.FirstOrDefault(x => x.User.Id == model.UserId) != null);
            if (privateChatRoomWithUser != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { ChatRoomId = privateChatRoomWithUser.ChatRoom.Id });
            }

            var chatRoom = new ChatRoom { CreationTime = DateTime.Now };
            chatRoom.Messages = new List<Message>();
            chatRoom.TypeChat = (await repository.Repository<TypeChat>().FindAllAsync(x => x.Name == "Private"))?.FirstOrDefault();
            chatRoom.ChatRoomMembers = new List<ChatRoomMember>
                {
                    new ChatRoomMember { DateTimeConnected = DateTime.Now, User = user },
                    new ChatRoomMember { DateTimeConnected = DateTime.Now, User = friend }
                };
            await repository.Repository<ChatRoom>().AddAsync(chatRoom);
            await repository.SaveAsync();
            Directory.CreateDirectory(HttpContext.Current.Server.MapPath($"~/App_Data/FilesChatRooms/{chatRoom.Id}/"));
            return Request.CreateResponse(HttpStatusCode.Created, new
            {
                chatRoomId = chatRoom.Id,
                userId = user.Id,
                friendUserId = friend.Id
            });
        }

        [HttpPost]
        [Route("CreatePrivateUserChatRoom")]
        public async Task<HttpResponseMessage> CreatePrivateUserChatRoom(CreatePrivateUserChatRoomModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            ApplicationUser friendUser = await repository.Repository<ApplicationUser>().FindByIdAsync(model.UserId);
            if (friendUser == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Friend user doesn't exists");
            }
            var chatRoomsMember = await repository.Repository<ChatRoomMember>().FindAllAsync(x => x.User.Id == user.Id);
            var privateChatRoomsMember = chatRoomsMember?.Where(item => item.ChatRoom.ChatRoomMembers.Count == 2);
            foreach (var item in privateChatRoomsMember)
            {
                foreach (var m in item.ChatRoom.ChatRoomMembers)
                    if (m.User.Id == friendUser.Id)
                        return Request.CreateErrorResponse(HttpStatusCode.Conflict, "Chat room with this user already created");
            }

            var chatRoom = new ChatRoom { CreationTime = DateTime.Now };
            chatRoom.Messages = new List<Message>();
            chatRoom.TypeChat = (await repository.Repository<TypeChat>().FindAllAsync(x => x.Name == "Private"))?.FirstOrDefault();
            chatRoom.ChatRoomMembers = new List<ChatRoomMember>
                {
                    new ChatRoomMember { DateTimeConnected = DateTime.Now, User = user },
                    new ChatRoomMember { DateTimeConnected = DateTime.Now, User = friendUser }
                };
            await repository.Repository<ChatRoom>().AddAsync(chatRoom);
            await repository.SaveAsync();
            Directory.CreateDirectory(HttpContext.Current.Server.MapPath($"~/App_Data/FilesChatRooms/{chatRoom.Id}/"));

            return Request.CreateResponse(HttpStatusCode.OK, new PrivateUserChatRoomModel
            {
                ChatRoomId = chatRoom.Id,
                FriendUserId = friendUser.Id,
                FriendUserName = friendUser.FullName == "" ? friendUser.FullName : friendUser.PhoneNumber
            });
        }

        //компресування і декомпресування!!!
        [AllowAnonymous]
        [HttpGet]
        [Route("GetMsgAttachment")]
        public IHttpActionResult GetMsgAttachment([FromUri]string ChatRoomId, [FromUri]string AttachmentName)
        {
            string file_path = HttpContext.Current.Server.MapPath($@"..\..\App_Data\FilesChatRooms\{ChatRoomId}\{AttachmentName}");
            if (!File.Exists(file_path))
                return null;
            var dataBytes = File.ReadAllBytes(file_path);
            var dataStream = new MemoryStream(dataBytes);
            return new CustomFileResult(dataStream, Request, AttachmentName);
        }

        //[AllowAnonymous]
        //[ActionName("File1")]
        //[HttpGet]
        //public HttpResponseMessage File1()
        //{
        //    var response = new HttpResponseMessage(HttpStatusCode.OK);
        //    string file_path = HttpContext.Current.Server.MapPath(@"..\..\Images\UserPhotos\6b620b49-29da-4f49-bd41-255e93441320\profileImage.jpg");
        //    var stream = new System.IO.FileStream(file_path, System.IO.FileMode.Open);
        //    response.Content = new StreamContent(stream);
        //    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        //    return response;
        //}
    }
}
