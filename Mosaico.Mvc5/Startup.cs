using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Mosaico.Mvc5.Startup))]
namespace Mosaico.Mvc5
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
