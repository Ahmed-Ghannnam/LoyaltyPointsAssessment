using System.Text.RegularExpressions;
using LoyaltyPoints.Api.DTOs;
using LoyaltyPoints.Api.Models;

namespace LoyaltyPoints.Api.Services;

public sealed class AwardService
{
    private static readonly Regex IdentifierPattern =
        new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly InMemoryStorage _storage;

    public AwardService(InMemoryStorage storage)
    {
        _storage = storage;
    }

    public AwardResult Award(AwardRequest? request)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return AwardResult.ValidationFailed(validationErrors);
        }

        lock (_storage.SyncRoot)
        {
            var identity = (request!.SourceId, request.ExternalTransactionId);
            if (_storage.Awards.TryGetValue(identity, out var existingAward))
            {
                if (existingAward.WalletId == request.WalletId && existingAward.Points == request.Points)
                {
                    return AwardResult.AlreadyExists(existingAward);
                }

                return AwardResult.Conflict();
            }

            if (!_storage.Wallets.TryGetValue(request.WalletId, out var wallet))
            {
                return AwardResult.WalletNotFound();
            }

            var award = new Award
            {
                AwardId = CreateUniqueAwardId(),
                SourceId = request.SourceId,
                ExternalTransactionId = request.ExternalTransactionId,
                WalletId = request.WalletId,
                Points = request.Points
            };

            _storage.Awards.Add(identity, award);
            wallet.Balance += request.Points;

            return AwardResult.Created(award);
        }
    }

    public WalletResponse? GetWallet(string walletId)
    {
        lock (_storage.SyncRoot)
        {
            if (!_storage.Wallets.TryGetValue(walletId, out var wallet))
            {
                return null;
            }

            return new WalletResponse
            {
                WalletId = wallet.WalletId,
                Balance = wallet.Balance
            };
        }
    }

    private string CreateUniqueAwardId()
    {
        string awardId;
        do
        {
            awardId = Guid.NewGuid().ToString("N");
        }
        while (_storage.Awards.Values.Any(award => award.AwardId == awardId));

        return awardId;
    }

    private static List<string> Validate(AwardRequest? request)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Request is required.");
            return errors;
        }

        ValidateIdentifier(request.SourceId, nameof(request.SourceId), errors);
        ValidateIdentifier(request.ExternalTransactionId, nameof(request.ExternalTransactionId), errors);
        ValidateIdentifier(request.WalletId, nameof(request.WalletId), errors);

        if (request.Points is < 1 or > 1000)
        {
            errors.Add("Points must be an integer between 1 and 1000.");
        }

        return errors;
    }

    private static void ValidateIdentifier(string? value, string fieldName, ICollection<string> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            errors.Add($"{fieldName} is required.");
        }
        else if (!IdentifierPattern.IsMatch(value))
        {
            errors.Add($"{fieldName} must contain 1 to 64 ASCII letters, digits, underscores, or hyphens.");
        }
    }
}
