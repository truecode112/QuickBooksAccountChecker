using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using System.Collections.Generic;
using System.IdentityModel.Tokens;
using Owin;
using log4net;

[assembly: OwinStartup(typeof(QBOAccountChecker.Startup))]

namespace QBOAccountChecker
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseCookieAuthentication(new CookieAuthenticationOptions
                {
                    AuthenticationType = "Cookies"
                });

            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = "TempState",
                AuthenticationMode = AuthenticationMode.Passive
            });

            log4net.Config.XmlConfigurator.Configure();
        }


    }
}