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
            if(chatRoom==null)
                return Request.CreateResponse(HttpStatusCode.NotFound, "Chat Room not found");
            var chatRoomMembers = (await repository.Repository<ChatRoomMember>().FindAllAsync(x => x.ChatRoom.Id == chatRoom.Id));
            if (chatRoomMembers == null || chatRoomMembers.FirstOrDefault(x=>x.User.Id==user.Id)==null)
                return Request.CreateResponse(HttpStatusCode.NotFound, "You are not a member of this chat room");
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
            msg.Attachments = new List<AttachmentMsg>();
            if (model.Attachments.Count > 0)
            {
                //поставити обмеження на розмір атачментів
                // і тримати тільки останні 30 повідомлень кімнати, під час написання 30-го усі попередні терти з бд
                for (int i = 0; i < model.Attachments.Count; i++)
                {
                    var pathToFile = Path.Combine(HttpContext.Current.Server.MapPath(
                        $"~/FilesChatRooms/{chatRoom.Id}/"),
                        model.AttachmentsNames[i]);
                    byte[] fileData = Convert.FromBase64String(model.Attachments[i]);
                    System.IO.File.WriteAllBytes(pathToFile, fileData);
                    msg.Attachments.Add(new AttachmentMsg() { FileName = model.AttachmentsNames[i] });
                }
            }
            await repository.SaveAsync();
            var responce = Request.CreateResponse(HttpStatusCode.OK, "Success");
            return responce;
        }

        //TODO!!!
        [HttpGet]
        [Route("GetAllMessagesToUserFormChatRoom")]
        public async Task<HttpResponseMessage> GetAllMessagesToUserFormChatRoom(GetAllMessagesToUserFormChatRoomModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var chatRoom = await repository.Repository<ChatRoom>().FindByIdAsync(model.ChatRoomId);
            if (chatRoom == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "Chat Room not found");
            }
            var listMsgs = new List<ChatRoomMessageModel>();
            string pathToDir = Path.Combine(HttpContext.Current.Server.MapPath($"~/FilesChatRooms/{chatRoom.Id}/"));
            //TODO здійснити перевірку чи правильно написано!!!
            if (chatRoom.Messages == null || chatRoom.Messages.Count == 0)
                return Request.CreateResponse(HttpStatusCode.OK, "List is empty");
            var msgs = chatRoom.Messages.OrderByDescending(x => x.Id).Take(30).ToList();
            for (int i = 0; i < msgs.Count; i++)
            {
                var returnModel = new ChatRoomMessageModel
                {
                    Body = msgs[i].Body,
                    UserName = msgs[i].ChatRoomMember.User.UserName,
                };
                listMsgs.Add(returnModel);
                //чи потрібно передавати усі файли??
                //чи краще зробити якусь кнопку, щоб він міг окремим методом той файл викачати
                if (msgs[i].Attachments != null && msgs[i].Attachments.Count > 0)
                {
                    returnModel.Attachments = new List<string>(msgs[i].Attachments.Select(x => Convert.ToBase64String(File.ReadAllBytes($"{pathToDir}{x.FileName}"))));// перевірити чи працює!!!!
                    returnModel.AttachmentsNames = new List<string>(msgs[i].Attachments.Select(x => x.FileName) as IEnumerable<string>);// перевірити чи працює!!!!
                }
            }
            var rmsgs = msgs.ToList().Select(x => x.Readeds.FirstOrDefault(item => item.ChatRoomMember.User.Id == user.Id));
            foreach(var item in rmsgs)
            {
                item.Readed = true;
                item.RDateTime = DateTime.Now;
            }
            return Request.CreateResponse(HttpStatusCode.OK, listMsgs);
        }
        //TODO через SignalR написати метод для отримання 1 повідомлення!!!

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
                new PrivateUserChatRoomModel {
                    ChatRoomId = item.ChatRoom.Id,
                    FriendUserId = item.ChatRoom.ChatRoomMembers.FirstOrDefault(x=>x.User.Id!=user.Id)?.User.Id,
                    FriendUserName = item.ChatRoom.ChatRoomMembers.FirstOrDefault(x => x.User.Id != user.Id)?.User.UserName
                }));
            return Request.CreateResponse(HttpStatusCode.OK, model);
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
                if(item.User.Id == model.UserId)
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
            await repository.SaveAsync();
            Directory.CreateDirectory(HttpContext.Current.Server.MapPath($"~/FilesChatRooms/{chatRoom.Id}/"));
            return Request.CreateResponse(HttpStatusCode.OK, new {
                chatRoomId = chatRoom,
                userId = user.Id,
                friendUserId = friendUser.Id
            });
        }
    }
}
