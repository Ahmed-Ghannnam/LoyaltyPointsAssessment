# Handoff

## Implementation Summary

LoyaltyPointsAssessment is a .NET 9 ASP.NET Core Web API with two endpoints: `POST /awards` and `GET /wallets/{walletId}`. It uses singleton in-memory storage initialized with `W100 = 100` and `W200 = 50`.

Award identity is `(sourceId, externalTransactionId)`. Matching retries return the original award without updating the balance. Conflicting retries return `409` semantics, and unknown wallets do not consume the identity. The complete award operation is protected by `InMemoryStorage.SyncRoot`.

The API uses a strongly typed `AwardRequest` DTO with Data Annotations for request validation. ASP.NET Core model binding handles malformed JSON and invalid JSON types, while service-level validation enforces the business rules independently.

## Commands

From `C:\Work\LoyaltyPointsAssessment`:

```powershell
dotnet restore .\LoyaltyPointsAssessment.sln
dotnet build .\LoyaltyPointsAssessment.sln
dotnet run --project .\LoyaltyPoints.Api\LoyaltyPoints.Api.csproj --launch-profile http
dotnet test .\LoyaltyPointsAssessment.sln
```

The HTTP profile uses `http://localhost:5169`. Restarting the application resets the in-memory state.

## Verification

The final checks executed were:

- `dotnet restore`: passed.
- `dotnet build --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test --no-build --no-restore`: passed, 47 tests, 0 failures, 0 skipped.

The tests cover award creation, retries, conflicts, unknown wallets, validation, malformed and non-object JSON, numeric token validation, wallet lookup, and concurrent service operations.

## Limitations

State is in memory and is lost on restart. The design supports a single process only; it does not coordinate multiple instances. Authentication and database persistence are intentionally outside the assessment scope.

## Time Spent

The core assignment was completed within the allocated 120-minute time limit.

After completing the core implementation, I made a few additional improvements to simplify request validation and add Swagger/OpenAPI documentation. These changes were made after the allocated time.
