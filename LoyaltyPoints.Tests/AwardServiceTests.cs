using LoyaltyPoints.Api.DTOs;
using LoyaltyPoints.Api.Services;

namespace LoyaltyPoints.Tests;

public class AwardServiceTests
{
    [Fact]
    public void NewAward_CreatesAwardAndIncreasesBalance()
    {
        var (service, storage) = CreateService();

        var result = service.Award(Request(points: 25));

        Assert.Equal(AwardResultStatus.Created, result.Status);
        Assert.NotNull(result.Award);
        Assert.NotEmpty(result.Award.AwardId);
        Assert.Equal("POS_A", result.Award.SourceId);
        Assert.Equal("T1", result.Award.ExternalTransactionId);
        Assert.Equal("W100", result.Award.WalletId);
        Assert.Equal(25, result.Award.Points);
        Assert.Equal(125, storage.Wallets["W100"].Balance);
        Assert.Single(storage.Awards);
    }

    [Fact]
    public void MatchingRetry_ReturnsOriginalAwardWithoutChangingBalance()
    {
        var (service, storage) = CreateService();
        var request = Request(points: 25);

        var created = service.Award(request);
        var retry = service.Award(request);

        Assert.Equal(AwardResultStatus.AlreadyExists, retry.Status);
        Assert.Same(created.Award, retry.Award);
        Assert.Equal(125, storage.Wallets["W100"].Balance);
        Assert.Single(storage.Awards);
    }

    [Fact]
    public void ConflictingRetryWithDifferentPoints_DoesNotModifyOriginalAwardOrBalance()
    {
        var (service, storage) = CreateService();
        var original = service.Award(Request(points: 25)).Award!;

        var result = service.Award(Request(points: 30));

        Assert.Equal(AwardResultStatus.Conflict, result.Status);
        Assert.Equal(original, storage.Awards[("POS_A", "T1")]);
        Assert.Equal(25, storage.Awards[("POS_A", "T1")].Points);
        Assert.Equal(125, storage.Wallets["W100"].Balance);
    }

    [Fact]
    public void ConflictingRetryWithDifferentWallet_DoesNotModifyOriginalAwardOrAnyWallet()
    {
        var (service, storage) = CreateService();
        var original = service.Award(Request(points: 25)).Award!;

        var result = service.Award(Request(walletId: "W200", points: 25));

        Assert.Equal(AwardResultStatus.Conflict, result.Status);
        Assert.Equal(original, storage.Awards[("POS_A", "T1")]);
        Assert.Equal(125, storage.Wallets["W100"].Balance);
        Assert.Equal(50, storage.Wallets["W200"].Balance);
    }

    [Fact]
    public void UnknownWallet_DoesNotCreateAwardOrConsumeIdentity()
    {
        var (service, storage) = CreateService();

        var result = service.Award(Request(walletId: "W999", points: 25));

        Assert.Equal(AwardResultStatus.WalletNotFound, result.Status);
        Assert.Empty(storage.Awards);
        Assert.Equal(100, storage.Wallets["W100"].Balance);
        Assert.Equal(50, storage.Wallets["W200"].Balance);
    }

    [Fact]
    public void UnknownWalletIdentityCanBeReusedAfterCorrection()
    {
        var (service, storage) = CreateService();
        var request = Request(walletId: "W999", points: 25);

        Assert.Equal(AwardResultStatus.WalletNotFound, service.Award(request).Status);

        request.WalletId = "W100";
        var corrected = service.Award(request);

        Assert.Equal(AwardResultStatus.Created, corrected.Status);
        Assert.Single(storage.Awards);
        Assert.Equal(125, storage.Wallets["W100"].Balance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("contains.dot")]
    [InlineData("é")]
    public void InvalidSourceId_ReturnsValidationFailed(string? sourceId)
    {
        var (service, storage) = CreateService();
        var request = Request();
        request.SourceId = sourceId!;

        var result = service.Award(request);

        Assert.Equal(AwardResultStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("SourceId"));
        Assert.Empty(storage.Awards);
    }

    [Fact]
    public void IdentifierLongerThan64Characters_ReturnsValidationFailed()
    {
        var (service, storage) = CreateService();
        var request = Request();
        request.ExternalTransactionId = new string('x', 65);

        var result = service.Award(request);

        Assert.Equal(AwardResultStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("ExternalTransactionId"));
        Assert.Empty(storage.Awards);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("contains.dot")]
    [InlineData("é")]
    public void InvalidWalletId_ReturnsValidationFailed(string? walletId)
    {
        var (service, storage) = CreateService();

        var result = service.Award(Request(walletId: walletId!));

        Assert.Equal(AwardResultStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("WalletId"));
        Assert.Empty(storage.Awards);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("contains.dot")]
    [InlineData("é")]
    public void InvalidExternalTransactionId_ReturnsValidationFailed(string? externalTransactionId)
    {
        var (service, storage) = CreateService();

        var result = service.Award(Request(externalTransactionId: externalTransactionId!));

        Assert.Equal(AwardResultStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("ExternalTransactionId"));
        Assert.Empty(storage.Awards);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void InvalidPoints_ReturnsValidationFailed(int points)
    {
        var (service, storage) = CreateService();

        var result = service.Award(Request(points: points));

        Assert.Equal(AwardResultStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("Points"));
        Assert.Empty(storage.Awards);
    }

    [Fact]
    public async Task ConcurrentMatchingRequests_CreateExactlyOneAward()
    {
        var (service, storage) = CreateService();
        var requests = Enumerable.Range(0, 32).Select(_ => Request()).ToArray();

        var results = await RunConcurrently(service, requests);

        Assert.Equal(1, results.Count(result => result.Status == AwardResultStatus.Created));
        Assert.Equal(31, results.Count(result => result.Status == AwardResultStatus.AlreadyExists));
        Assert.All(results, result => Assert.NotNull(result.Award));
        Assert.Single(storage.Awards);
        Assert.Equal(125, storage.Wallets["W100"].Balance);
    }

    [Fact]
    public async Task ConcurrentDistinctAwardsToSameWallet_AddAllPoints()
    {
        var (service, storage) = CreateService();
        var requests = Enumerable.Range(1, 20)
            .Select(index => Request(externalTransactionId: $"T{index}", points: index))
            .ToArray();

        var results = await RunConcurrently(service, requests);

        Assert.All(results, result => Assert.Equal(AwardResultStatus.Created, result.Status));
        Assert.Equal(20, storage.Awards.Count);
        Assert.Equal(100 + Enumerable.Range(1, 20).Sum(), storage.Wallets["W100"].Balance);
    }

    [Fact]
    public async Task ConcurrentConflictingRequests_AcceptOneAndConflictTheRest()
    {
        var (service, storage) = CreateService();
        var requests = Enumerable.Range(1, 32)
            .Select(points => Request(points: points))
            .ToArray();

        var results = await RunConcurrently(service, requests);

        Assert.Equal(1, results.Count(result => result.Status == AwardResultStatus.Created));
        Assert.Equal(31, results.Count(result => result.Status == AwardResultStatus.Conflict));
        Assert.Single(storage.Awards);
        Assert.Equal(100 + results.Single(result => result.Status == AwardResultStatus.Created).Award!.Points,
            storage.Wallets["W100"].Balance);
    }

    private static (AwardService Service, InMemoryStorage Storage) CreateService()
    {
        var storage = new InMemoryStorage();
        return (new AwardService(storage), storage);
    }

    private static AwardRequest Request(
        string sourceId = "POS_A",
        string externalTransactionId = "T1",
        string walletId = "W100",
        int points = 25) => new()
        {
            SourceId = sourceId,
            ExternalTransactionId = externalTransactionId,
            WalletId = walletId,
            Points = points
        };

    private static async Task<AwardResult[]> RunConcurrently(AwardService service, AwardRequest[] requests)
    {
        using var barrier = new Barrier(requests.Length);
        var tasks = requests.Select(request => Task.Run(() =>
        {
            barrier.SignalAndWait();
            return service.Award(request);
        }));

        return await Task.WhenAll(tasks);
    }
}
