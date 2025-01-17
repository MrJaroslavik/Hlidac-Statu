using System.Threading.Tasks;
using HlidacStatu.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HlidacStatu.Ceny.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;

        public LogoutModel(SignInManager<ApplicationUser> signInManager)
        {
            _signInManager = signInManager;
        }

        public async Task<IActionResult> OnGet(string? returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            Util.Consts.Logger.Info("User logged out.");
            if(string.IsNullOrEmpty(returnUrl))
                return RedirectToAction("Index", "Home");
            else
                return LocalRedirect(returnUrl);
        }

        public async Task<IActionResult> OnPost(string? returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            Util.Consts.Logger.Info("User logged out.");
            if(string.IsNullOrEmpty(returnUrl))
                return RedirectToAction("Index", "Home");
            else
                return LocalRedirect(returnUrl);
        }
    }
}
