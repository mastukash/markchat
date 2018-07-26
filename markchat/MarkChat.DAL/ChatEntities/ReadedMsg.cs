using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.ChatEntities
{
    public class ReadedMsg
    {
        public int Id { get; set; }
        public DateTime? DateTime { get; set; }
        public bool? Readed { get; set; }
        public virtual ChatRoomMember ChatRoomMember { get; set; }
        public virtual Message Message { get; set; }
    }
}
