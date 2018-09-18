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


    public class GetMessageByIdModel
    {
        public int MessageId { get; set; }
    }
    public class GetMessagedModel
    {
        public string FromUserId { get; set; }
        public string FromUserName { get; set; }
        public int ChatRoomId { get; set; }
        public string Body { get; set; }
        public List<AttachmentModel> Attachments { get; set; }
    }
    public class AttachmentModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string File { get; set; }
    }

    public class GetAttachmentByIdModel
    {
        public int AttachmentId { get; set; }
    }

    public class GetLast30MessagesToUserFormChatRoomModel
    {
        public int ChatRoomId { get; set; }
    }
    public class GetIdUserByIdPrivateChatRoomModel
    {
        public int ChatRoomId { get; set; }
    }
    public class GetNext30MessagesToUserFormChatRoomModel
    {
        public int ChatRoomId { get; set; }
        public int MsgId { get; set; }
    }

    public class GetChatRoomIdByUserIdModel
    {
        public string UserId { get; set; }
    }

    public class NotifyChatRoomMessageModel: ChatRoomMessageModel
    {
        public int RoomId { get; set; }
    }
    public class ChatRoomMessageModel
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Body { get; set; }
        public string UserName { get; set; }
        public DateTime? DateTime { get; set; }
        public string UserUrlPhoto { get; set; }

        //public List<string> Attachments { get; set; }
        public List<int> AttachmentsId { get; set; }
        public List<string> AttachmentsNames { get; set; }
        public List<string> AttachmentUrls { get; set; }

    }

    public class GetAllUsersWithoutPrivateChatRoomModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserUrlPhoto{ get; set; }
    }
    public class PrivateUserChatRoomModel
    {
        public int ChatRoomId { get; set; }
        public string FriendUserId { get; set; }
        public string FriendUserName { get; set; }
        public string UserUrlPhoto { get; set; }

    }
    public class CreatePrivateUserChatRoomModel
    {
        public string UserId { get; set; }
    }
    
}