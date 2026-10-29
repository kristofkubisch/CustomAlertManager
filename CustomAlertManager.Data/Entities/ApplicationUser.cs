using Microsoft.AspNetCore.Identity;

namespace CustomAlertManager.Data.Entities;

public sealed class ApplicationUser : IdentityUser<int>
{
    // Filled in by the database default (SYSDATETIMEOFFSET()) when the row is inserted.
    public DateTimeOffset CreatedAt { get; set; }
}
