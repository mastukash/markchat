using MarkChat.DAL.ChatEntities;
using MarkChat.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL
{
    class ContextInitializer : CreateDatabaseIfNotExists<ApplicationDbContext>
    {
        protected override void Seed(ApplicationDbContext db)
        {
            TypeChat t1 = new TypeChat { Name = "Private" };
            TypeChat t2 = new TypeChat { Name = "Public" };
            TypeChat t3 = new TypeChat { Name = "Group" };

            db.TypesChats.Add(t1);
            db.TypesChats.Add(t2);
            db.TypesChats.Add(t3);

            db.SaveChanges();
        }
    }
}
