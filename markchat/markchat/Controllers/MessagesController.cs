using markchat.Models;
using MarkChat.DAL.ChatEntities;
using MarkChat.DAL.Entities;
using MarkChat.DAL.Repository;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
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
        //Добавити при створенні БД тип чату "приватний"
        //add SignalR
        //чи в повідомленні все таки мати посилання на користувача, а не на мембера???
        //два користувача можуть бути в декількох чатах, тому і такі параметри, чи можливо у вхідних параметрах відштовхуватися від типу чату?
        public async Task<HttpResponseMessage> SendMessage(SendMessageModel model)
        {
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User doesn't exists");
            }
            var chatRoom = await repository.Repository<ChatRoom>().FindByIdAsync(model.ChatRoomId);
            //TODO створити чат кімнату і добавити до неї користувача (якому ми пишемо, мб тоді і передавати ід користувача кому пишемо)
            //під час створення чату створювати папку
            if (chatRoom == null)
            {
                chatRoom = new ChatRoom { CreationTime = DateTime.Now };
                chatRoom.Messages = new List<Message>();
                chatRoom.ChatRoomMembers = new List<ChatRoomMember>();
                chatRoom.ChatRoomMembers.Add(new ChatRoomMember {DateTimeConnected = DateTime.Now, User = user });
                if(model.ToUserId!=null)
                    chatRoom.ChatRoomMembers.Add(new ChatRoomMember {
                        DateTimeConnected = DateTime.Now,
                        User = await repository.Repository<ApplicationUser>().FindByIdAsync(model.ToUserId)
                    });
                await repository.SaveAsync();
            }
            var msg = new Message { Body = model.Body, DateTime = DateTime.Now };
            // відмітити шо цей користувач прочитав то повідомлення!
            
            chatRoom.Messages.Add(msg);
            if (model.Attachments.Count > 0)
            {
                //TODO добавити атачменти
                //поставити обмеження на розмір атачментів
                // і тримати тільки останні 50 повідомлень кімнати, під час написання 50-го усі попередні терти з бд
            }
            msg.DateTime= DateTime.Now;
            //кароче всьо сложно.... 0_о

            var responce = Request.CreateResponse(HttpStatusCode.OK, "Success");
            return responce;
        }
    }
}
