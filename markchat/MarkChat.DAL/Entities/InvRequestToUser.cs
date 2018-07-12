using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.Entities
{
    [Table("InvRequestToUsers")]
    public class InvRequestToUser
    {
        [Key]
        [ForeignKey("InvRequest")]
        public int InvRequestToUserId { get; set; }
        public virtual InvRequest InvRequest { get; set; }
        public virtual TagChat TagChat { get; set; }

        public virtual ApplicationUser User { get; set; }

    }
}
