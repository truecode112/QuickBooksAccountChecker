using Intuit.Ipp.OAuth2PlatformClient;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using System.Net;
using Intuit.Ipp.Core;
using Intuit.Ipp.Data;
using Intuit.Ipp.QueryFilter;
using Intuit.Ipp.Security;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using log4net;
using System.Collections.ObjectModel;

namespace QBOAccountChecker.Controllers
{
    public class AppController : Controller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AppController));
        public static string clientid = ConfigurationManager.AppSettings["clientid"];
        public static string clientsecret = ConfigurationManager.AppSettings["clientsecret"];
        public static string redirectUrl = ConfigurationManager.AppSettings["redirectUrl"];
        public static string environment = ConfigurationManager.AppSettings["appEnvironment"];

        public static OAuth2Client auth2Client = new OAuth2Client(clientid, clientsecret, redirectUrl, environment);

        /// <summary>
        /// Use the Index page of App controller to get all endpoints from discovery url
        /// </summary>
        public ActionResult Index()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Session.Clear();
            Session.Abandon();
            Request.GetOwinContext().Authentication.SignOut("Cookies");
            return View();
        }

        /// <summary>
        /// Start Auth flow
        /// </summary>
        public ActionResult InitiateAuth(string submitButton)
        {
            switch (submitButton)
            {
                case "Connect to QuickBooks":
                    List<OidcScopes> scopes = new List<OidcScopes>();
                    scopes.Add(OidcScopes.Accounting);
                    scopes.Add(OidcScopes.OpenId);
                    scopes.Add(OidcScopes.Email);
                    scopes.Add(OidcScopes.Profile);
                    string authorizeUrl = auth2Client.GetAuthorizationURL(scopes);
                    return Redirect(authorizeUrl);
                default:
                    return (View());
            }
        }

        /// <summary>
        /// QBO API Request
        /// </summary>
        public async Task<ActionResult> ApiCallService()
        {
            if (Session["realmId"] != null)
            {
                string realmId = Session["realmId"].ToString();
                try
                {
                    var principal = User as ClaimsPrincipal;
                    string access_token = principal.FindFirst("access_token").Value;
                    Log.Debug(string.Format("Access Token = {0}", access_token));
                    string refresh_token = principal.FindFirst("refresh_token").Value;
                    Log.Debug(string.Format("Refresh Token = {0}", refresh_token));
                    string identity_token = principal.FindFirst("identity_token").Value;
                    Log.Debug(string.Format("Identity Token = {0}", identity_token));

                    Log.Debug("Start Validate ID Token");
                    var isTokenValid = await auth2Client.ValidateIDTokenAsync(identity_token);
                    Log.Debug("End Validate ID Token");

                    if (!isTokenValid)
                    {
                        return View("ApiCallService", (object)("QBO API call Failed!" + " Error message: " + "Token invalid"));
                    }

                    Log.Debug("Start Get User Info");
                    UserInfoResponse userInfoResp = await auth2Client.GetUserInfoAsync(access_token);
                    Log.Debug("End Get User Info");
                    if (userInfoResp == null)
                    {
                        return View("ApiCallService", (object)("QBO API call Failed!" + " Error message: " + "Invalid User Information"));
                    }
                    Log.Debug(string.Format("UserInfo = {0}", userInfoResp.Raw));
                    string userEmail = (string)userInfoResp.Json["email"];
                    Log.Debug(string.Format("User Email = {0}", userEmail));
                    string givenName = (string)userInfoResp.Json["givenName"];
                    Log.Debug(string.Format("Given Name = {0}", givenName));

                    Boolean isEmailVerified = (Boolean)userInfoResp.Json["emailVerified"];
                    if (!isEmailVerified)
                    {
                        return View("ApiCallService", (object)("QBO API call Response!" + " Sorry, email is not verified yet!"));
                    }

                    OAuth2RequestValidator oauthValidator = new OAuth2RequestValidator(principal.FindFirst("access_token").Value);

                    // Create a ServiceContext with Auth tokens and realmId
                    ServiceContext serviceContext = new ServiceContext(realmId, IntuitServicesType.QBO, oauthValidator);
                    serviceContext.IppConfiguration.MinorVersion.Qbo = "23";

                    // Create a QuickBooks QueryService using ServiceContext
                    //QueryService<CompanyInfo> querySvc = new QueryService<CompanyInfo>(serviceContext);
                    //CompanyInfo companyInfo = querySvc.ExecuteIdsQuery("SELECT * FROM CompanyInfo").FirstOrDefault();
                    QueryService<Account> querySvc = new QueryService<Account>(serviceContext);
                    //Account accountInfo = querySvc.ExecuteIdsQuery(string.Format("SELECT * FROM Account where Name like '%{0}%'", givenName)).FirstOrDefault();
                    ReadOnlyCollection<Account> accountInfo = querySvc.ExecuteIdsQuery(string.Format("SELECT * FROM Account"));

                    foreach (Account acc in accountInfo)
                    {
                        Log.Debug((Newtonsoft.Json.JsonConvert.SerializeObject(acc)));
                    }

                    //string output = "Company Name: " + companyInfo.CompanyName + " Company Address: " + companyInfo.CompanyAddr.Line1 + ", " + companyInfo.CompanyAddr.City + ", " + companyInfo.CompanyAddr.Country + " " + companyInfo.CompanyAddr.PostalCode;
                    if (accountInfo == null)
                    {
                        return View("ApiCallService", (object)("QBO API call Failed!" + " Error message: Can not find user"));
                    }
                    //string output = accountInfo.Id.ToString();
                    return View("ApiCallService", (object)("QBO API call Successful!! Response: " + givenName));
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    return View("ApiCallService", (object)("QBO API call Failed!" + " Error message: " + ex.Message));
                }
            }
            else
                return View("ApiCallService", (object)"QBO API call Failed!");
        }

        /// <summary>
        /// Use the Index page of App controller to get all endpoints from discovery url
        /// </summary>
        public ActionResult Error()
        {
            return View("Error");
        }

        /// <summary>
        /// Action that takes redirection from Callback URL
        /// </summary>
        public ActionResult Tokens()
        {
            return View("Tokens");
        }
    }
}