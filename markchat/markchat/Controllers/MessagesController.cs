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
        //Добавити при створенні БД тип чату "приватний"
        //add SignalR
        //чи в повідомленні все таки мати посилання на користувача, а не на мембера???
        //два користувача можуть бути в декількох чатах, тому і такі параметри, чи можливо у вхідних параметрах відштовхуватися від типу чату?
        //переробити на create chatroom i змінити модель, яка буде заточена під chatroom
        public async Task<HttpResponseMessage> SendMessage(SendMessageModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var chatRoom = await repository.Repository<ChatRoom>().FindByIdAsync(model.ChatRoomId);
            
            var msg = new Message { Body = model.Body, DateTime = DateTime.Now };
            msg.Attachments = new List<AttachmentMsg>();
            // відмітити шо цей користувач прочитав то повідомлення!
            chatRoom.Messages.Add(msg);

            if (model.Attachments.Count > 0)
            {
                //поставити обмеження на розмір атачментів
                // і тримати тільки останні 30 повідомлень кімнати, під час написання 30-го усі попередні терти з бд
                for (int i = 0; i < model.Attachments.Count; i++)
                {
                    var pathToDir = Path.Combine(HttpContext.Current.Server.MapPath(
                        $"~/FilesChatRooms/{chatRoom.Id}/"),
                        model.AttachmentsNames[i]);
                    byte[] fileData = Convert.FromBase64String(model.Attachments[i]);
                    System.IO.File.WriteAllBytes(pathToDir, fileData);
                    msg.Attachments.Add(new AttachmentMsg() { FileName = model.AttachmentsNames[i] });
                }
            }
            msg.DateTime = DateTime.Now;
            var ChatRoomMembers = (await repository.Repository<ChatRoomMember>().FindAllAsync(x => x.ChatRoom.Id == chatRoom.Id));
            if (ChatRoomMembers == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "Chat Room Members not found");
            }
            msg.ChatRoomMember = ChatRoomMembers.FirstOrDefault(x => x.User.Id == user.Id);
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
            //TODO відмітити усі повідомлення чат кімнати як прочитані
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
