namespace LoyaltyPoints.Api.DTOs;

public class AwardResponse
{
    public string AwardId { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string ExternalTransactionId { get; set; } = string.Empty;

    public string WalletId { get; set; } = string.Empty;

    public int Points { get; set; }
}
