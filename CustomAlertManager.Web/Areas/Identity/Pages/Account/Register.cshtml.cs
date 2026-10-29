using System.ComponentModel.DataAnnotations;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomAlertManager.Web.Areas.Identity.Pages.Account;

// Replaces the default Identity sign-up page so that every new account gets the User role.
[AllowAnonymous]
public sealed class RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) : PageModel
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

        var user = new ApplicationUser
        {
            UserName = Input.Username.Trim(),
            Email = Input.Email.Trim(),
        };

        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                AddError(error);
            }

            return Page();
        }

        await userManager.AddToRoleAsync(user, IdentitySeeder.UserRole);
        await signInManager.SignInAsync(user, isPersistent: false);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : Redirect("/");
    }

    private void AddError(IdentityError error)
    {
        var (key, message) = error.Code switch
        {
            "DuplicateEmail" => ("Input.Email", "An account with this email address already exists."),
            "DuplicateUserName" => ("Input.Username", "This username is already taken."),
            "InvalidUserName" => ("Input.Username", "Use letters, numbers, dots, dashes, underscores or @ and + in the username."),
            "InvalidEmail" => ("Input.Email", "Enter a valid email address."),
            _ when error.Code.StartsWith("Password", StringComparison.Ordinal) => ("Input.Password", error.Description),
            _ => (string.Empty, error.Description),
        };
        ModelState.AddModelError(key, message);
    }

    public sealed class InputModel
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[A-Za-z0-9._@+-]+$", ErrorMessage = "Use letters, numbers and . _ - @ + only.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 10)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
