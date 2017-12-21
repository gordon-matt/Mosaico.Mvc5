using System.IO;
using System.Web.Mvc;

namespace Mosaico.Mvc5.Controllers
{
    [RoutePrefix("templates")]
    public class TemplateController : Controller
    {
        // NOTE: Ignore {name1}. It's actually just the same as {name}, but MVC routing won't allow us to use {name} twice
        [Route("{name}/template-{name1}")]
        public ActionResult Index(string name)
        {
            string filePath = Server.MapPath(string.Format("~/Content/templates/{0}/template-{0}.html", name));
            string content = System.IO.File.ReadAllText(filePath);
            return Content(content);
        }
    }
}