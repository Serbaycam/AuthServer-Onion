using AuthServer.Identity.WebPanel.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;

namespace AuthServer.Identity.WebPanel.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {

        public async Task<IActionResult> Dashboard()
        {
            return View();
        }
    }
}