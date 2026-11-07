using LoyaltyPoints.Api.Models;

namespace LoyaltyPoints.Api.Services;

public class InMemoryStorage
{
    public InMemoryStorage()
    {
        Wallets = new Dictionary<string, Wallet>(StringComparer.Ordinal)
        {
            ["W100"] = new Wallet { WalletId = "W100", Balance = 100 },
            ["W200"] = new Wallet { WalletId = "W200", Balance = 50 }
        };

        Awards = new Dictionary<(string SourceId, string ExternalTransactionId), Award>();
    }

    public Dictionary<string, Wallet> Wallets { get; }

    public Dictionary<(string SourceId, string ExternalTransactionId), Award> Awards { get; }

    public object SyncRoot { get; } = new();
}
