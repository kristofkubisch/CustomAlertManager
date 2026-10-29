using System.ComponentModel.DataAnnotations;

namespace CustomAlertManager.Web.Models;

public sealed record SubscriptionInput
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public int FrequencyMinutes { get; set; } = 15;

    public List<int> SourceIds { get; set; } = [];

    // Nullable on purpose: a non-nullable string is treated as required, so an empty textarea would be rejected.
    public string? IncludeKeywords { get; set; }

    public string? ExcludeKeywords { get; set; }

    public List<int> TargetIds { get; set; } = [];

}
