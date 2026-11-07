using LoyaltyPoints.Api.Models;

namespace LoyaltyPoints.Api.Services;

public enum AwardResultStatus
{
    Created,
    AlreadyExists,
    Conflict,
    WalletNotFound,
    ValidationFailed
}

public sealed class AwardResult
{
    private AwardResult(AwardResultStatus status, Award? award, IReadOnlyList<string> errors)
    {
        Status = status;
        Award = award;
        Errors = errors;
    }

    public AwardResultStatus Status { get; }

    public Award? Award { get; }

    public IReadOnlyList<string> Errors { get; }

    public static AwardResult Created(Award award) => new(AwardResultStatus.Created, award, []);

    public static AwardResult AlreadyExists(Award award) => new(AwardResultStatus.AlreadyExists, award, []);

    public static AwardResult Conflict() => new(AwardResultStatus.Conflict, null, []);

    public static AwardResult WalletNotFound() => new(AwardResultStatus.WalletNotFound, null, []);

    public static AwardResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(AwardResultStatus.ValidationFailed, null, errors);
}
