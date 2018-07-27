using MarkChat.DAL.ChatEntities;
using MarkChat.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace markchat.Models
{
    public class SendMessageModel
    {
        public string Body { get; set; }
        public int ChatRoomId { get; set; }
        public List<string> Attachments { get; set; }
        public List<string> AttachmentsNames { get; set; }
    }
    public class GetAllMessagesToUserFormChatRoomModel
    {
        public int ChatRoomId { get; set; }
    }
    public class ChatRoomMessageModel
    {
        public string Body { get; set; }
        public string UserName { get; set; }
        public List<string> Attachments { get; set; }
        public List<string> AttachmentsNames { get; set; }
    }
    public class PrivateUserChatRoomModel
    {
        public int ChatRoomId { get; set; }
        public string FriendUserId { get; set; }
        public string FriendUserName { get; set; }
    }
    public class CreatePrivateUserChatRoomModel
    {
        public string UserId { get; set; }
    }
    
}