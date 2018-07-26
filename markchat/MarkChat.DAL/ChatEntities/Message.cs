using MarkChat.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.ChatEntities
{
    public class Message
    {
        public int Id { get; set; }
        public string Body { get; set; }
        public DateTime? DateTime { get; set; }
        public virtual ChatRoomMember ChatRoomMember { get; set; }
        public virtual ChatRoom ChatRoom { get; set; }
        public virtual List<AttachmentMsg> Attachments { get; set; }
        public virtual List<ReadedMsg> Readeds { get; set; }
    }
}
