using MarkChat.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace markchat.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Home Page";

            ApplicationDbContext context = new ApplicationDbContext();
            context.Users.Select(x => x);

            return View();
        }
    }
}
