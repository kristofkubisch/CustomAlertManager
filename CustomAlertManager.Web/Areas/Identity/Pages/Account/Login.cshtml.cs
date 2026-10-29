using System.ComponentModel.DataAnnotations;
using CustomAlertManager.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomAlertManager.Web.Areas.Identity.Pages.Account;

// Replaces the default Identity login page so that users log in with their username rather than their email.
[AllowAnonymous]
public sealed class LoginModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; private set; }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var username = Input.Username.Trim();

        // A deactivated account gets its own message only when the password is also right, so the page
        // does not reveal which usernames exist. Nothing is signed in, so there is nothing to undo.
        var user = await signInManager.UserManager.FindByNameAsync(username);
        if (user is { IsActive: false }
            && await signInManager.UserManager.CheckPasswordAsync(user, Input.Password))
        {
            ModelState.AddModelError(string.Empty, "This account has been deactivated. Contact an administrator.");
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(
            username,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : Redirect("/");
        }

        ModelState.AddModelError(
            string.Empty,
            result.IsLockedOut
                ? "This account is locked after too many failed attempts. Try again later."
                : "Invalid username or password.");
        return Page();
    }

    public sealed class InputModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}
