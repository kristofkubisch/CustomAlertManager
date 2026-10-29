using System.Security.Claims;
using CustomAlertManager.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace CustomAlertManager.Web.Services;

public static class UserManagerExtensions
{
    // Identity hands back the key as a string. The database key is an int.
    public static int GetIntUserId(this UserManager<ApplicationUser> users, ClaimsPrincipal principal) =>
        int.Parse(users.GetUserId(principal) ?? throw new InvalidOperationException("No user is signed in."));
}
