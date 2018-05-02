using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Formatting;

namespace ConsoleApp31
{
    class Program
    {
        static void Main(string[] args)
        {
            F();
            //cl.PostAsJsonAsync("api/Account/Register", model);
        }
        public static async void F()
        {
            //List<KeyValuePair<string, string>> dict = new List<KeyValuePair<string, string>>();

            //dict.Add(new KeyValuePair<string, string>("FirstName", "Олег"));
            //dict.Add(new KeyValuePair<string, string>("MiddleName", "qwerty"));
            //dict.Add(new KeyValuePair<string, string>("LastName", "фівапр"));
            //dict.Add(new KeyValuePair<string, string>("SecurityCode", "1"));
            //dict.Add(new KeyValuePair<string, string>("Email", "mastykash@itstep.org"));
            //dict.Add(new KeyValuePair<string, string>("Password", "1qaz!QAZ"));
            //dict.Add(new KeyValuePair<string, string>("ConfirmPassword ", "1qaz!QAZ"));
            //dict.Add(new KeyValuePair<string, string>("PhoneNumber", "0637873195"));
            //dict.Add(new KeyValuePair<string, string>("PhotoName", "1.jpg"));

            //MultipartFormDataContent data = new MultipartFormDataContent();
            //foreach(var i in dict)
            //{
            //    data.Add(new StringContent(i.Value),i.Key);
            //}
            //data.Add(new ByteArrayContent(System.IO.File.ReadAllBytes("1.jpg")), "File");

            HttpClient cl = new HttpClient();

            RegisterBindingModel model = new RegisterBindingModel()
            {
                FirstName = "ooooooo",
                MiddleName = "oooooo",
                LastName = "sssssss",
                SecurityCode = "1",
                Email = "ooooooooo@ahgsd.asd",
                Password = "1qaz!QAZ",
                ConfirmPassword = "1qaz!QAZ",
                PhoneNumber = "+380637873195",
                PhotoName = "1.jpg",
                File = null,

            };

            model.File = System.IO.File.ReadAllBytes("1.jpg");
            //model.PhotoName = "1.jpg";

            // model.File.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
            cl.BaseAddress = new Uri("http://localhost:2101/");
            cl.DefaultRequestHeaders.Accept.Clear();
            cl.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/bson"));

            var response = cl.PostAsync("api/Account/Register", model, new BsonMediaTypeFormatter()).Result;
            response.EnsureSuccessStatusCode();

        }
    }

    public class RegisterBindingModel
    {
        public string FirstName { get; set; }
        public string MiddleName { get; set; }

        public string LastName { get; set; }
        public byte[] File { get; set; }
        public string PhotoName { get; set; }
        //public string PhotoName { get; set; }
        public string SecurityCode { get; set; }

        public string Email { get; set; }

        public string Password { get; set; }

        public string ConfirmPassword { get; set; }
        public string PhoneNumber { get; set; }
    }
}
