using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.Entities
{
    public class InvRequestToChat : InvRequest
    {
        public virtual TagChat TagChat { get; set; }

        public virtual ApplicationUser User { get; set; }

    }
}
