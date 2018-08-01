using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.SignalR;
    
namespace markchat.Hubs
{
    //mb додати user name
    public class ChatHub : Hub
    {
        //key - ApplicationUserId
        //value - hub context user id
        static Dictionary<string,string> Users = new Dictionary<string, string>();

        public void SendMsg(string idRoom, string idUser, string message)
        {
            if (Users.ContainsKey(idUser))
                Clients.User(Users[idUser]).Send(message);
            else
                Clients.Caller.Notification("User is offline");
        }

        public void Connect(string idUser)
        {
            var id = Context.ConnectionId;
            if (!Users.ContainsKey(idUser))
            {
                Users.Add(idUser, id);
                //Clients.Caller.onConnected(idUser,id, Users);
            }
            else
            {
                Users[idUser] = id;
            }
        }

        public override System.Threading.Tasks.Task OnDisconnected(bool stopCalled)
        {
            var id = Context.ConnectionId;
            if (Users.ContainsValue(id))
            {
                var item = Users.FirstOrDefault(x => x.Value == id);
                Users.Remove(item.Key);
                Clients.All.onUserDisconnected(id, item.Key);
            }
            return base.OnDisconnected(stopCalled);
        }
    }
}