using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.Entities
{
    public class Currency
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public char Symbol { get; set; }
        public virtual List<Notification> Notification { get; set; }
    }
}
