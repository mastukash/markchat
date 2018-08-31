using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace markchat.Controllers
{
    public class ClientController : Controller
    {
        // GET: Client
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult Login()
        {
            return View();
        }
        public ActionResult Messagess()
        {
            return View();
        }
        [HttpGet]
        public System.Web.Mvc.FileResult GetFile()
        {
            string file_path = Server.MapPath(@"..\App_Data\Images\UserPhotos\6b620b49-29da-4f49-bd41-255e93441320\profileImage.jpg");
            string file_type = "application/jpeg";
            string file_name = "profileImage.jpg";
            return File(file_path, file_type,file_name);
        }
    }
}