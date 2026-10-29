using System.ComponentModel.DataAnnotations;
using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.Web.Models;

public sealed class TargetInput
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public ChannelKind Kind { get; set; } = ChannelKind.Email;

    [Required]
    [StringLength(2048)]
    public string Destination { get; set; } = string.Empty;
}
