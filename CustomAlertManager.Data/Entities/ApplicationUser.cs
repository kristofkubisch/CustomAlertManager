using Microsoft.AspNetCore.Identity;

namespace CustomAlertManager.Data.Entities;

public sealed class ApplicationUser : IdentityUser<int>
{
    // Deactivated users cannot log in, and their subscriptions are not run.
    public bool IsActive { get; set; } = true;

    // Filled in by the database default (SYSDATETIMEOFFSET()) when the row is inserted.
    public DateTimeOffset CreatedAt { get; set; }
}
