using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace markchat.Models
{
    public class TagChatMessageModel
    {
        public int Id { get; set; }

        //можливо варто зробити double
        public int Price { get; set; }
        
        public DateTime PublicationDate { get; set; }

        public string Description { get; set; }

        public string Currency { get; set; }

        public Stack<string> Tags { get; set; }

        public string AuthorPhone { get; set; }

        public string AuthorFullName { get; set; }

        public string AuthorPhoto { get; set; }

    }
}