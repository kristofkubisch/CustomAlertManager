namespace CustomAlertManager.Data.Entities;

public sealed class NewsSource : TimestampedEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
