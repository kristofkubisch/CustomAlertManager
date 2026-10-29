namespace CustomAlertManager.Data.Entities;

public abstract class TimestampedEntity
{
    // Filled in by the database default (SYSDATETIMEOFFSET()) when the row is inserted.
    public DateTimeOffset CreatedAt { get; set; }
}
