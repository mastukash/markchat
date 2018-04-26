using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MarkChat.DAL.Entities
{
    public class CategoryTag
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public virtual Category ParentCategory { get; set; }

        public virtual Category ChildCategory { get; set; }

        public virtual List<Notification> Notificatons { get; set; }
    }
}