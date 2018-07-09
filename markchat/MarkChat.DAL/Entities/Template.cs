using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.Entities
{
    class Template
    {
        [Key]
        public int IdTemplate { get; set; }
        public string Name { get; set; }

        public virtual Category Root { get; set; }
    }
}
