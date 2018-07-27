using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.ChatEntities
{
    public class AttachmentMsg
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public virtual Message Message { get; set; }
    }
}
