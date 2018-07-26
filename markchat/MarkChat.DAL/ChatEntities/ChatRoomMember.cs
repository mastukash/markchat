using MarkChat.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.ChatEntities
{
    public class ChatRoomMember
    {
        public int Id { get; set; }
        public DateTime? DateTimeConnected { get; set; }
        public virtual ApplicationUser User { get; set; }
        public virtual ChatRoom ChatRoom { get; set; }
        public virtual List<ReadedMsg> ReadedMsgs { get; set; }
        public virtual List<Message> Messages { get; set; }

        
    }
}
