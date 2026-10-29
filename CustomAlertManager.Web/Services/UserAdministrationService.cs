using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Data.Services;
using CustomAlertManager.Scheduler.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Web.Services;

public sealed class UserAdministrationService(
    AlertDbContext db,
    UserManager<ApplicationUser> users,
    ILogger<UserAdministrationService> logger)
{
    // Returns an error message, or null when the change was made.
    public async Task<string?> SetActiveAsync(int userId, int actingAdminId, bool isActive, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return "That user no longer exists.";
        }

        if (!isActive)
        {
            if (userId == actingAdminId)
            {
                return "You cannot deactivate your own account.";
            }

            if (await IsLastActiveAdminAsync(userId, cancellationToken))
            {
                return "The last active administrator cannot be deactivated.";
            }
        }

        user.IsActive = isActive;
        logger.LogInformation("Admin {Admin} {Action} user {UserName}", actingAdminId, isActive ? "activated" : "deactivated", user.UserName);
        await users.UpdateAsync(user);

        // Changing the security stamp signs the user out everywhere, so existing sessions are checked again.
        await users.UpdateSecurityStampAsync(user);
        return null;
    }

    // Returns an error message, or null when the user was deleted.
    public async Task<string?> DeleteAsync(int userId, int actingAdminId, CancellationToken cancellationToken)
    {
        if (userId == actingAdminId)
        {
            return "You cannot delete your own account.";
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return "That user no longer exists.";
        }

        if (await IsLastActiveAdminAsync(userId, cancellationToken))
        {
            return "The last active administrator cannot be deleted.";
        }

        // Targets are protected by a foreign key, so they are removed first. Delivery history stays, with the target cleared.
        var targetIds = await db.NotificationTargets
            .Where(target => target.OwnerId == userId)
            .Select(target => target.Id)
            .ToListAsync(cancellationToken);
        await db.SubscriptionTargets
            .Where(link => targetIds.Contains(link.NotificationTargetId))
            .ExecuteDeleteAsync(cancellationToken);
        await db.AlertDeliveries
            .Where(delivery => delivery.NotificationTargetId != null
                && targetIds.Contains(delivery.NotificationTargetId.Value)
                && delivery.Status == DeliveryStatus.Pending)
            .ExecuteDeleteAsync(cancellationToken);
        await db.NotificationTargets
            .Where(target => targetIds.Contains(target.Id))
            .ExecuteDeleteAsync(cancellationToken);

        // The user's subscriptions, their alerts and their roles are removed by the database cascade.
        var result = await users.DeleteAsync(user);
        logger.LogInformation("Admin {Admin} deleted user {UserName} and their subscriptions and targets", actingAdminId, user.UserName);
        return result.Succeeded ? null : string.Join("; ", result.Errors.Select(error => error.Description));
    }

    private async Task<bool> IsLastActiveAdminAsync(int userId, CancellationToken cancellationToken)
    {
        var adminRoleId = await db.Roles
            .Where(role => role.Name == IdentitySeeder.AdminRole)
            .Select(role => role.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var otherActiveAdmins = await db.UserRoles
            .Where(link => link.RoleId == adminRoleId && link.UserId != userId)
            .Join(db.Users, link => link.UserId, user => user.Id, (link, user) => user)
            .CountAsync(user => user.IsActive, cancellationToken);
        return otherActiveAdmins == 0;
    }
}
