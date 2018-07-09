using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.Entities
{
    class TemplateCategory
    {
        [Key]
        public int IdTemplateCategory { get; set; }

        public virtual Template Template { get; set; }
        public virtual Category Root { get; set; }

    }
}
