using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Mosaico.Mvc5.Extensions;
using Mosaico.Mvc5.Helpers;
using Mosaico.Mvc5.Models;

namespace Mosaico.Mvc5.Controllers
{
    [RoutePrefix("mosaico")]
    public class MosaicoController : Controller
    {
        private static readonly string FromEmail;
        private static readonly string FromName;

        static MosaicoController()
        {
            FromEmail = ConfigurationManager.AppSettings["SmtpOptions.FromEmail"];
            FromName = ConfigurationManager.AppSettings["SmtpOptions.FromName"];
        }

        [Route("")]
        public ActionResult Index()
        {
            // TODO: Obviously in a real situation we need to have paging.
            //  But for demo purposes, this will do...
            using (var context = new ApplicationDbContext())
            {
                var model = context.MosaicoEmails.ToList();
                ViewBag.Title = "Free responsive email template editor | Mosaico.io";
                return View(model);
            }
        }

        [Route("editor/{name}/{template}/{id?}")]
        public async Task<ActionResult> Editor(string name, MosaicoTemplate template, int id = 0)
        {
            MosaicoEmail model;
            if (id > 0)
            {
                using (var context = new ApplicationDbContext())
                {
                    model = await context.MosaicoEmails.FirstOrDefaultAsync(x => x.Id == id);
                }
            }
            else
            {
                model = new MosaicoEmail
                {
                    Name = name,
                    Template = template
                };
            }

            ViewBag.Title = "Mosaico Editor";

            // TODO: Add your own tokens here
            ViewBag.FieldTokens = new Dictionary<string, string>
            {
                { "Title", "Title" },
                { "FirstName", "First Name" },
                { "LastName", "Last Name" },
            };

            return View(model);
        }

        [HttpPost]
        [Route("dl")]
        public async Task<ActionResult> Download(
            string action,
            string filename,
            string rcpt,
            string subject,
            string html)
        {
            switch (action)
            {
                case "download":
                    {
                        var bytes = Encoding.UTF8.GetBytes(html);
                        return File(bytes, "text/html", filename);
                    }
                case "email":
                    {
                        var message = new MailMessage
                        {
                            Subject = subject,
                            IsBodyHtml = true,
                            Body = html
                        };

                        message.From = new MailAddress(FromEmail, FromName);
                        message.To.Add(new MailAddress(rcpt));

                        using (var smtpClient = new SmtpClient())
                        {
                            smtpClient.Send(message);
                        }

                        return new EmptyResult();
                    }
                default: throw new ArgumentException("Unsuported action type: " + action);
            }
        }

        [HttpGet]
        [Route("upload")]
        public ActionResult GetUploads()
        {
            string path = Server.MapPath("~/Media/Uploads");

            var files = (new DirectoryInfo(path)).GetFiles().Select(x => new MosaicoFileInfo
            {
                name = x.Name,
                size = x.Length,
                type = MimeMapping.GetMimeMapping(x.Name),
                url = Url.AbsoluteContent(string.Concat("/Media/Uploads/", x.Name)),
                thumbnailUrl = Url.AbsoluteContent(string.Concat("/Media/Thumbs/", x.Name)),
                deleteUrl = string.Concat("/mosaico/img-delete/", x.Name),
                deleteType = "DELETE"
            });

            return Json(new { files = files }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [Route("upload")]
        public async Task<ActionResult> Upload()
        {
            var returnList = new List<MosaicoFileInfo>();

            for (int i = 0; i < Request.Files.Count; i++)
            {
                var file = Request.Files[i];

                if (file.ContentLength > 0)
                {
                    string fileName = file.FileName;
                    string filePath = Server.MapPath(Path.Combine("~/Media/Uploads", fileName));
                    string thumbPath = Server.MapPath(Path.Combine("~/Media/Thumbs", fileName));
                    file.SaveAs(filePath);

                    var image = System.Drawing.Image.FromFile(filePath);
                    var thumbnail = ImageHelper.Resize(image, 120, 90);
                    thumbnail.Save(thumbPath);

                    returnList.Add(new MosaicoFileInfo
                    {
                        name = fileName,
                        size = file.ContentLength,
                        type = MimeMapping.GetMimeMapping(fileName),
                        url = Url.AbsoluteContent(string.Concat("/Media/Uploads/", fileName)),
                        thumbnailUrl = Url.AbsoluteContent(string.Concat("/Media/Thumbs/", fileName)),
                        deleteUrl = string.Concat("/mosaico/img-delete/", fileName),
                        deleteType = "DELETE"
                    });
                }
            }

            return Json(new { files = returnList });
        }

        [Route("img")]
        public async Task<ActionResult> Image(string src, string method, string @params)
        {
            var split = @params.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            switch (method)
            {
                case "cover":
                case "resize":
                    {
                        string filePath = Server.MapPath(Path.Combine("~/Media/Uploads", src.RightOfLastIndexOf('/')));
                        byte[] bytes = System.IO.File.ReadAllBytes(filePath);

                        Image result;
                        using (var stream = new MemoryStream(bytes))
                        {
                            var image = Bitmap.FromStream(stream);

                            int? destinationWidth = split[0] == "null" ? null : (int?)int.Parse(split[0]);
                            int? destinationHeight = split[1] == "null" ? null : (int?)int.Parse(split[1]);

                            if (destinationWidth.HasValue && destinationHeight.HasValue)
                            {
                                if (method == "cover")
                                {
                                    result = ImageHelper.Crop(image, destinationWidth.Value, destinationHeight.Value, AnchorPosition.Center);
                                }
                                else
                                {
                                    result = ImageHelper.Resize(image, destinationWidth.Value, destinationHeight.Value);
                                }
                            }
                            else if (destinationWidth.HasValue)
                            {
                                var newHeight = destinationWidth.Value * image.Height / image.Width;
                                result = ImageHelper.Resize(image, destinationWidth.Value, newHeight);
                            }
                            else if (destinationHeight.HasValue)
                            {
                                var newWidth = destinationHeight.Value * image.Width / image.Height;
                                result = ImageHelper.Resize(image, newWidth, destinationHeight.Value);
                            }
                            else
                            {
                                throw new ArgumentException("A destination width and/or height must be specified.");
                            }
                        }

                        using (var memoryStream = new MemoryStream())
                        {
                            result.Save(memoryStream, ImageFormat.Jpeg);
                            byte[] newBytes = memoryStream.ToArray();
                            return File(newBytes, "image/jpg");
                        }
                    }
                case "placeholder":
                default:
                    {
                        string width = split[0];
                        string height = split[1];

                        using (var client = new HttpClient())
                        {
                            string url = string.Format("http://via.placeholder.com/{0}x{1}", width, height);
                            byte[] bytes = await client.GetByteArrayAsync(new Uri(url));
                            return File(bytes, "image/jpg");
                        }
                    }
            }
        }

        [HttpDelete]
        [Route("img-delete/{fileName}")]
        public ActionResult Delete(string fileName)
        {
            string filePath = Server.MapPath(Path.Combine("~/Media/Uploads", fileName));

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            return new EmptyResult();
        }

        [ValidateInput(false)]
        [HttpPost]
        [Route("save")]
        public async Task<ActionResult> Save(
            int id,
            string name,
            MosaicoTemplate template,
            string metadata,
            string content,
            string html)
        {
            try
            {
                using (var context = new ApplicationDbContext())
                {
                    var record = await context.MosaicoEmails.FindAsync(id);

                    bool isNew = (record == null);

                    if (isNew)
                    {
                        record = new MosaicoEmail();
                    }

                    record.Name = name;
                    record.Template = template;
                    record.Metadata = metadata;
                    record.Content = content;
                    // Save the HTML so we can use it for mass emailing. Example: User will input tokens like {FirstName}, {LastName}, etc into the template,
                    //  then we can do a search and replace with regex when sending emails (Your own logic, somewhere in your app).
                    record.Html = html;

                    if (isNew)
                    {
                        context.MosaicoEmails.Add(record);
                    }
                    else
                    {
                        context.MosaicoEmails.Attach(record);
                    }

                    await context.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Sucessfully saved email." });
            }
            catch (Exception x)
            {
                return Json(new { success = false, message = x.GetBaseException().Message });
            }
        }

        public class MosaicoFileInfo
        {
            public string name { get; set; }

            public long size { get; set; }

            public string type { get; set; }

            public string url { get; set; }

            public string thumbnailUrl { get; set; }

            public string deleteUrl { get; set; }

            public string deleteType { get; set; }
        }
    }
}