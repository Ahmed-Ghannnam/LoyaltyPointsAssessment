using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LoyaltyPoints.Tests;

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task PostAward_NewAward_ReturnsCreated()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await PostAward(client, points: 25);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = await ParseJson(response);
        Assert.Equal("POS_A", document.RootElement.GetProperty("sourceId").GetString());
        Assert.Equal(25, document.RootElement.GetProperty("points").GetInt32());
        Assert.False(string.IsNullOrEmpty(document.RootElement.GetProperty("awardId").GetString()));
    }

    [Fact]
    public async Task PostAward_MatchingRetry_ReturnsOkWithSameAwardId()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var first = await PostAward(client, points: 25);
        using var firstDocument = await ParseJson(first);
        var awardId = firstDocument.RootElement.GetProperty("awardId").GetString();

        var retry = await PostAward(client, points: 25);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var retryDocument = await ParseJson(retry);
        Assert.Equal(awardId, retryDocument.RootElement.GetProperty("awardId").GetString());
    }

    [Fact]
    public async Task PostAward_ConflictingRetry_ReturnsConflict()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        await PostAward(client, points: 25);
        var response = await PostAward(client, points: 30);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        AssertJsonObjectWithError(response);
    }

    [Fact]
    public async Task PostAward_UnknownWallet_ReturnsNotFound()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await PostAward(client, walletId: "W999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertJsonObjectWithError(response);
    }

    [Fact]
    public async Task PostAward_InvalidRequest_ReturnsBadRequest()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/awards", new
        {
            sourceId = "contains space",
            externalTransactionId = "T1",
            walletId = "W100",
            points = 25
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertJsonObjectWithError(response);
    }

    [Theory]
    [InlineData("sourceId")]
    [InlineData("externalTransactionId")]
    [InlineData("walletId")]
    [InlineData("points")]
    public async Task PostAward_MissingRequiredField_ReturnsBadRequest(string fieldName)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var fields = new Dictionary<string, object>
        {
            ["sourceId"] = "POS_A",
            ["externalTransactionId"] = "T1",
            ["walletId"] = "W100",
            ["points"] = 25
        };
        fields.Remove(fieldName);

        var response = await client.PostAsJsonAsync("/awards", fields);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertJsonObjectWithError(response);
    }

    [Theory]
    [InlineData("sourceId", "contains space")]
    [InlineData("externalTransactionId", "contains.dot")]
    [InlineData("walletId", "é")]
    [InlineData("sourceId", "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task PostAward_InvalidIdentifier_ReturnsBadRequest(string fieldName, string value)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var request = new Dictionary<string, object>
        {
            ["sourceId"] = "POS_A",
            ["externalTransactionId"] = "T1",
            ["walletId"] = "W100",
            ["points"] = 25
        };
        request[fieldName] = value;

        var response = await client.PostAsJsonAsync("/awards", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertJsonObjectWithError(response);
    }

    [Fact]
    public async Task PostAward_MalformedJson_ReturnsBadRequest()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var content = new StringContent("{\"sourceId\":", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/awards", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertJsonObject(response);
    }

    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    public async Task PostAward_NonObjectJsonBody_ReturnsBadRequest(string json)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/awards", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertJsonObjectWithError(response);
    }

    [Fact]
    public async Task PostAward_DecimalPoints_ReturnsBadRequest()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var content = new StringContent(
            "{\"sourceId\":\"POS_A\",\"externalTransactionId\":\"T1\",\"walletId\":\"W100\",\"points\":25.0}",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/awards", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertJsonObjectWithError(response);
    }

    [Fact]
    public async Task PostAward_MaximumPoints_ReturnsCreatedAndUpdatesBalance()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await PostAward(client, points: 1000);
        var walletResponse = await client.GetAsync("/wallets/W100");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, walletResponse.StatusCode);
        using var walletDocument = await ParseJson(walletResponse);
        Assert.Equal(1100, walletDocument.RootElement.GetProperty("balance").GetInt32());
    }

    [Theory]
    [InlineData("\"25\"")]
    [InlineData("true")]
    [InlineData("null")]
    public async Task PostAward_NonIntegerPoints_ReturnsBadRequest(string pointsJson)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var content = new StringContent(
            $"{{\"sourceId\":\"POS_A\",\"externalTransactionId\":\"T1\",\"walletId\":\"W100\",\"points\":{pointsJson}}}",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/awards", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertJsonObjectWithError(response);
    }

    [Fact]
    public async Task PostAward_IntegerLargerThanInt32MaxValue_ReturnsBadRequest()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var content = new StringContent(
            "{\"sourceId\":\"POS_A\",\"externalTransactionId\":\"T1\",\"walletId\":\"W100\",\"points\":2147483648}",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/awards", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertJsonObjectWithError(response);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task PostAward_OutOfRangePoints_ReturnsBadRequest(int points)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await PostAward(client, points: points);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertJsonObjectWithError(response);
    }

    [Fact]
    public async Task GetWallet_ReturnsUpdatedBalance()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        await PostAward(client, points: 25);
        var response = await client.GetAsync("/wallets/W100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ParseJson(response);
        Assert.Equal("W100", document.RootElement.GetProperty("walletId").GetString());
        Assert.Equal(125, document.RootElement.GetProperty("balance").GetInt32());
    }

    [Fact]
    public async Task GetWallet_UnknownWallet_ReturnsNotFound()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/wallets/W999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertJsonObjectWithError(response);
    }

    private static Task<HttpResponseMessage> PostAward(
        HttpClient client,
        string walletId = "W100",
        int points = 25) => client.PostAsJsonAsync("/awards", new
        {
            sourceId = "POS_A",
            externalTransactionId = "T1",
            walletId,
            points
        });

    private static async Task<JsonDocument> ParseJson(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static void AssertJsonObject(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    private static void AssertJsonObjectWithError(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.True(
            document.RootElement.TryGetProperty("error", out _) ||
            document.RootElement.TryGetProperty("errors", out _) ||
            document.RootElement.TryGetProperty("title", out _));
    }
}
