using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.ChatEntities
{
    public class ChatRoom
    {
        public int Id { get; set; }
        public DateTime? CreationTime { get; set; }
        public virtual TypeChat TypeChat { get; set; }
        public virtual List<Message> Messages { get; set; }
        public virtual List<ChatRoomMember> ChatRoomMembers { get; set; }
    }
}
