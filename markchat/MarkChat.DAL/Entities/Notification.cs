using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.Entities
{
    public class Notification
    {
        public int Id { get; set; }

        //можливо варто зробити double
        public int Price { get; set; }

        [Required]
        public DateTime PublicationDate { get; set; }

        [Required]
        [MaxLength(90)]
        public string Description { get; set; }

        public virtual Currency Currency { get; set; }

        public virtual TagChat TagChat { get; set; }
        
        public virtual Category Category { get; set; }

        public virtual ApplicationUser Author { get; set; }
    }
    
}
