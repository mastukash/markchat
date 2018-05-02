using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.Entities
{
    //public class Category
    //{
    //    public int Id { get; set; }
    //    [Required]
    //    public string Name { get; set; }



    //    public virtual List<CategoryTag> CategoryTags { get; set; }
    //}
    public class Category
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }

        public string Title { get; set; }

        //public int? ParentCategoryId { get; set; }    

        public virtual Category ParentCategory { get; set; }

        public virtual List<Category> ChildCategories { get; set; }

        public virtual List<Category> ParentCategories { get; set; }
        




        public virtual List<TagChat> ChatRoots { get; set; }

        public virtual List<Notification> Notifications { get; set; }
    }
}
