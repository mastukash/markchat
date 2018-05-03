using MarkChat.DAL.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL
{
    public class TagChat
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string InvitationCode { get; set; }

        public TagChat()
        {
            Users = new List<ApplicationUser>();
        }


        public virtual Category RootCategory { get; set; }
        public virtual ApplicationUser OwnerUser { get; set; }
        
        public virtual ICollection<ApplicationUser> Users { get; set; }
    }
}
