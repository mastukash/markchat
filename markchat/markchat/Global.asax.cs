using markchat.Hubs;
using MarkChat.DAL.Entities;
using MarkChat.DAL.Repository;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace markchat
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
        protected void Session_Start(object sender, EventArgs e)
        {
            Session.Timeout = 15;
        }

        protected async System.Threading.Tasks.Task Session_EndAsync(object sender, EventArgs e)
        {
            GenericUnitOfWork repository = new GenericUnitOfWork();
            ApplicationUser user = await repository.Repository<ApplicationUser>().FindByIdAsync(User.Identity.GetUserId());
            if (user != null && ChatHub.Users.ContainsKey(user.Id))
                ChatHub.Users.Remove(user.Id);
        }
    }
}
