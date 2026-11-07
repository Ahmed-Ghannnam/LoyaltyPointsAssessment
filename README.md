# Loyalty Points Assessment

## Project Overview

This project is a small ASP.NET Core Web API that awards loyalty points to customer wallets. It handles retry-safe award requests, prevents duplicate balance updates, detects conflicting retries, and provides wallet balance lookup.

The API targets .NET 9 (`net9.0`).

## Prerequisites

- .NET 9 SDK. The project was verified with SDK `9.0.302`.
- No database, cache, external service, or other runtime dependency is required.

## Run the Application

Run these commands from the solution root:

```powershell
dotnet restore .\LoyaltyPointsAssessment.sln
dotnet build .\LoyaltyPointsAssessment.sln
dotnet run --project .\LoyaltyPoints.Api\LoyaltyPoints.Api.csproj --launch-profile http
```

The HTTP launch profile listens on `http://localhost:5169`. The HTTPS profile is also configured for `https://localhost:7076` and `http://localhost:5169`.

## Run Tests

```powershell
dotnet test .\LoyaltyPointsAssessment.sln
```

## Initial Data

The in-memory store starts with:

- `W100` = `100` points
- `W200` = `50` points

There are no existing awards at startup. State is held in process memory and resets when the application restarts.

## API Documentation

### POST /awards

Awards points to a wallet.

Request:

```json
{
  "sourceId": "POS_A",
  "externalTransactionId": "T1",
  "walletId": "W100",
  "points": 25
}
```

New award response (`201 Created`):

```json
{
  "awardId": "f47ac10b58cc4372a5670e02b2c3d479",
  "sourceId": "POS_A",
  "externalTransactionId": "T1",
  "walletId": "W100",
  "points": 25
}
```

A matching retry returns `200 OK` with the original award record and `awardId`.

Possible statuses:

- `201 Created` — new award accepted.
- `200 OK` — matching retry; no second balance update.
- `400 Bad Request` — malformed JSON, non-object JSON, invalid field types, or validation failure.
- `404 Not Found` — the wallet does not exist for a new award.
- `409 Conflict` — the same identity was previously accepted with a different wallet or points value.

Validation rules:

- `sourceId`, `externalTransactionId`, and `walletId` are required strings of 1–64 ASCII letters, digits, underscores, or hyphens.
- Identifiers are case-sensitive.
- `points` must be a JSON integer from 1 through 1000. Decimal, string, boolean, null, and out-of-range values are rejected.

### GET /wallets/{walletId}

Example request:

```text
GET http://localhost:5169/wallets/W100
```

Successful response (`200 OK`):

```json
{
  "walletId": "W100",
  "balance": 125
}
```

An unknown wallet returns `404 Not Found` with a JSON error object.

## Design Decisions

- Award identity is the case-sensitive composite pair `(sourceId, externalTransactionId)`.
- Matching retries return the original award without changing the balance.
- A retry with a different wallet or points value returns a conflict and leaves existing state unchanged.
- The complete award operation uses the shared `InMemoryStorage.SyncRoot` lock, covering identity lookup, wallet lookup, award creation/storage, and balance update.
- `JsonElement` is used at the controller boundary so the API can require a JSON object and distinguish JSON token types, including rejecting decimal points such as `25.0`.
- In-memory storage is proportionate to the single-process assessment scope and keeps the implementation free of database or infrastructure dependencies.

## Error Precedence

For a POST request, precedence is:

1. Malformed JSON or a non-object body is rejected by the HTTP layer with `400`.
2. Invalid JSON field types, including non-string identifiers or non-integer points, are rejected with `400`.
3. Missing or invalid values are validated before awards or wallets are inspected, returning `400`.
4. For a valid request, an existing award identity is checked before wallet existence. A matching identity returns `200`; a conflicting identity returns `409`.
5. Only a new identity checks the wallet. An unknown wallet returns `404`; otherwise the award is created and the balance is updated.

## Limitations

- In-memory state is lost when the application restarts.
- The implementation is intended for one process and does not coordinate across multiple instances.
- Authentication and persistent storage are not implemented because they are outside the assessment scope.
