using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.Entities
{
    public class Confirmation
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; }
        public string Code { get; set; }
        public DateTime Date { get; set; }
        public string Token { get; set; }
        public bool Confirmed { get; set; } = false;
        public int TryCount { get; set; } = 0;
        public DateTime LastTry { get; set; } = DateTime.Now;
    }
}
