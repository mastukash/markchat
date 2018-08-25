using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace markchat.Models
{

    public class CreateTagChatModel
    {
        public string TagChatName { get; set; }
    }

    public class CreateTagChatByTemplateModel
    {
        public string TagChatName { get; set; }
        public int IdTemplate { get; set; }
    }
    public class CategoryInfo
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
    }

    public class UserInfo
    {
        public string FullName { get; set; }
        //public string PhoneNumber { get; set; }
        public byte[] Photo { get; set; }
        public string PhotoName { get; set; }
    }

    public class TagChatMessageModel
    {
        public int Id { get; set; }
        public int IdUser { get; set; }

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