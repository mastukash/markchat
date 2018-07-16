using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.Entities
{
    public class InvRequest
    {
        [Key]
        public int Id { get; set; }

        public bool IsWatched { get; set; }

        public DateTime? RequestDateTime { get; set; }

        public bool Confirmed { get; set; }
        public bool Denied { get; set; }

    }
}
