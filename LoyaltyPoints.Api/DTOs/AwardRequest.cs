using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoyaltyPoints.Api.DTOs;

public class AwardRequest
{
    [Required(ErrorMessage = "SourceId is required.")]
    [RegularExpression("^[A-Za-z0-9_-]{1,64}$", ErrorMessage = "SourceId must contain 1 to 64 ASCII letters, digits, underscores, or hyphens.")]
    public string SourceId { get; set; } = string.Empty;

    [Required(ErrorMessage = "ExternalTransactionId is required.")]
    [RegularExpression("^[A-Za-z0-9_-]{1,64}$", ErrorMessage = "ExternalTransactionId must contain 1 to 64 ASCII letters, digits, underscores, or hyphens.")]
    public string ExternalTransactionId { get; set; } = string.Empty;

    [Required(ErrorMessage = "WalletId is required.")]
    [RegularExpression("^[A-Za-z0-9_-]{1,64}$", ErrorMessage = "WalletId must contain 1 to 64 ASCII letters, digits, underscores, or hyphens.")]
    public string WalletId { get; set; } = string.Empty;

    [Range(1, 1000, ErrorMessage = "Points must be an integer between 1 and 1000.")]
    public int Points { get; set; }
}

