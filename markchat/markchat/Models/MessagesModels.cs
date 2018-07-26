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
    }
}