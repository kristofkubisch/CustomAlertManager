using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Data.Services;
using CustomAlertManager.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Web.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public sealed class IndexModel(
    AlertDbContext db,
    UserManager<ApplicationUser> users,
    UserAdministrationService administration) : PageModel
{
    public List<UserRow> Rows { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Rows = await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostToggleAsync(int id, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var error = await administration.SetActiveAsync(id, users.GetIntUserId(User), !user.IsActive, cancellationToken);
        if (error is null)
        {
            TempData["Status"] = $"'{user.UserName}' {(user.IsActive ? "deactivated" : "activated")}.";
        }
        else
        {
            TempData["Error"] = error;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var name = user.UserName;
        var error = await administration.DeleteAsync(id, users.GetIntUserId(User), cancellationToken);
        if (error is null)
        {
            TempData["Status"] = $"User '{name}' and their subscriptions and targets were deleted.";
        }
        else
        {
            TempData["Error"] = error;
        }

        return RedirectToPage();
    }

    private Task<List<UserRow>> LoadAsync(CancellationToken cancellationToken)
    {
        var adminRoleId = db.Roles.Where(role => role.Name == IdentitySeeder.AdminRole).Select(role => role.Id).FirstOrDefault();
        return db.Users
            .OrderBy(user => user.UserName)
            .Select(user => new UserRow(
                user.Id,
                user.UserName!,
                user.Email,
                user.IsActive,
                db.UserRoles.Any(link => link.UserId == user.Id && link.RoleId == adminRoleId),
                db.AlertSubscriptions.Count(subscription => subscription.OwnerId == user.Id),
                user.CreatedAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public sealed record UserRow(
        int Id,
        string UserName,
        string? Email,
        bool IsActive,
        bool IsAdmin,
        int SubscriptionCount,
        DateTimeOffset CreatedAt);
}
