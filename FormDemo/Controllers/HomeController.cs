using System.Diagnostics;
using FormDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.Identity.Web;

namespace FormDemo.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly GraphServiceClient _graphServiceClient;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, GraphServiceClient graphServiceClient)
        {
            _logger = logger;
            _graphServiceClient = graphServiceClient;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var model = new UserFormModel();
            
            if (User.Identity?.IsAuthenticated == true)
            {
                try
                {
                    var user = await _graphServiceClient.Me.Request().GetAsync();
                    model.Name = user.DisplayName ?? string.Empty;
                    model.Email = user.Mail ?? user.UserPrincipalName ?? string.Empty;
                    ViewData["GraphApiResult"] = user.DisplayName;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving user data from Graph API");
                }
            }

            return View(model);
        }
        
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Index(UserFormModel model)
        {
            if (!ModelState.IsValid)
            {
                if (User.Identity?.IsAuthenticated == true)
                {
                    try
                    {
                        var user = await _graphServiceClient.Me.Request().GetAsync();
                        model.Name = user.DisplayName ?? string.Empty;
                        model.Email = user.Mail ?? user.UserPrincipalName ?? string.Empty;
                        ViewData["GraphApiResult"] = user.DisplayName;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error retrieving user data from Graph API");
                    }
                }
                return View(model);
            }
            
            // Process the form data
            // For demonstration purposes, we're just returning the same view with a success message
            ViewData["SubmissionSuccess"] = true;
            ViewData["SubmissionMessage"] = $"Thank you, {model.Name}! Your information has been received.";
            
            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
