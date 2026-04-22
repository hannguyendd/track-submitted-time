# Time Tracking Submission Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a minimal API that records and queries per-user submission timestamps in Redis using a sorted set.

**Architecture:** Three endpoints (POST /submissions, GET /submissions/{userId}, GET /submissions) backed by a `RedisTimeTrackingRepository` behind an `ITimeTrackingRepository` interface. Routes are registered in `Program.cs`; Redis is a singleton. No authentication, no payload storage — only timestamps.

**Tech Stack:** .NET 10 minimal API, StackExchange.Redis 2.x, Redis sorted set (`ZADD`/`ZSCORE`/`ZRANGEBYSCORE`)

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `TrackingService/TrackingService.csproj` | Modify | Add StackExchange.Redis package |
| `TrackingService/appsettings.json` | Modify | Add Redis connection string config |
| `TrackingService/appsettings.Development.json` | Modify | Override Redis for local dev |
| `TrackingService/Models/SubmissionRequest.cs` | Create | POST request model |
| `TrackingService/Models/SubmissionResponse.cs` | Create | Response model (userId + timestamp) |
| `TrackingService/Repositories/ITimeTrackingRepository.cs` | Create | Repository interface |
| `TrackingService/Repositories/RedisTimeTrackingRepository.cs` | Create | Redis sorted-set implementation |
| `TrackingService/Program.cs` | Modify | Wire services + register routes |
| `TrackingService/TrackingService.http` | Modify | Manual test calls |

---

## Task 1: Add StackExchange.Redis package

**Files:**
- Modify: `TrackingService/TrackingService.csproj`

- [ ] **Step 1: Add the NuGet package**

```bash
cd TrackingService && dotnet add package StackExchange.Redis
```

Expected: package added, `TrackingService.csproj` now contains a `StackExchange.Redis` `PackageReference`.

- [ ] **Step 2: Verify build**

```bash
dotnet build TrackingService
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add TrackingService/TrackingService.csproj TrackingService/obj/project.assets.json
git commit -m "chore: add StackExchange.Redis package"
```

---

## Task 2: Configure Redis connection string

**Files:**
- Modify: `TrackingService/appsettings.json`
- Modify: `TrackingService/appsettings.Development.json`

- [ ] **Step 1: Add Redis config to appsettings.json**

Replace the contents of `TrackingService/appsettings.json` with:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

- [ ] **Step 2: Add Redis config to appsettings.Development.json**

Replace the contents of `TrackingService/appsettings.Development.json` with:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Debug",
      "System": "Information",
      "Microsoft": "Information"
    }
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add TrackingService/appsettings.json TrackingService/appsettings.Development.json
git commit -m "chore: add Redis connection string config"
```

---

## Task 3: Create models

**Files:**
- Create: `TrackingService/Models/SubmissionRequest.cs`
- Create: `TrackingService/Models/SubmissionResponse.cs`

- [ ] **Step 1: Create SubmissionRequest**

Create `TrackingService/Models/SubmissionRequest.cs`:

```csharp
namespace TrackingService.Models;

public record SubmissionRequest(string UserId);
```

- [ ] **Step 2: Create SubmissionResponse**

Create `TrackingService/Models/SubmissionResponse.cs`:

```csharp
namespace TrackingService.Models;

public record SubmissionResponse(string UserId, DateTime LastSubmittedAt);
```

- [ ] **Step 3: Verify build**

```bash
dotnet build TrackingService
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add TrackingService/Models/
git commit -m "feat: add submission request/response models"
```

---

## Task 4: Create repository interface

**Files:**
- Create: `TrackingService/Repositories/ITimeTrackingRepository.cs`

- [ ] **Step 1: Create the interface**

Create `TrackingService/Repositories/ITimeTrackingRepository.cs`:

```csharp
namespace TrackingService.Repositories;

public interface ITimeTrackingRepository
{
    Task RecordSubmissionAsync(string userId);
    Task<DateTime?> GetLastSubmissionAsync(string userId);
    Task<IEnumerable<(string UserId, DateTime LastSubmittedAt)>> GetSubmissionsInRangeAsync(DateTime? from, DateTime? to);
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build TrackingService
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add TrackingService/Repositories/ITimeTrackingRepository.cs
git commit -m "feat: add ITimeTrackingRepository interface"
```

---

## Task 5: Implement RedisTimeTrackingRepository

**Files:**
- Create: `TrackingService/Repositories/RedisTimeTrackingRepository.cs`

Redis sorted set key: `last_submitted`
Score: Unix timestamp in seconds (UTC)
Member: userId string

- [ ] **Step 1: Create the implementation**

Create `TrackingService/Repositories/RedisTimeTrackingRepository.cs`:

```csharp
using StackExchange.Redis;

namespace TrackingService.Repositories;

public class RedisTimeTrackingRepository(IConnectionMultiplexer redis) : ITimeTrackingRepository
{
    private const string Key = "last_submitted";
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task RecordSubmissionAsync(string userId)
    {
        var score = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _db.SortedSetAddAsync(Key, userId, score);
    }

    public async Task<DateTime?> GetLastSubmissionAsync(string userId)
    {
        var score = await _db.SortedSetScoreAsync(Key, userId);
        if (score is null) return null;
        return DateTimeOffset.FromUnixTimeSeconds((long)score.Value).UtcDateTime;
    }

    public async Task<IEnumerable<(string UserId, DateTime LastSubmittedAt)>> GetSubmissionsInRangeAsync(
        DateTime? from, DateTime? to)
    {
        var min = from.HasValue
            ? (double)new DateTimeOffset(from.Value, TimeSpan.Zero).ToUnixTimeSeconds()
            : double.NegativeInfinity;

        var max = to.HasValue
            ? (double)new DateTimeOffset(to.Value, TimeSpan.Zero).ToUnixTimeSeconds()
            : double.PositiveInfinity;

        var entries = await _db.SortedSetRangeByScoreWithScoresAsync(Key, min, max);

        return entries.Select(e => (
            UserId: (string)e.Element!,
            LastSubmittedAt: DateTimeOffset.FromUnixTimeSeconds((long)e.Score).UtcDateTime
        ));
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build TrackingService
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add TrackingService/Repositories/RedisTimeTrackingRepository.cs
git commit -m "feat: implement RedisTimeTrackingRepository"
```

---

## Task 6: Wire up services and routes in Program.cs

**Files:**
- Modify: `TrackingService/Program.cs`

Replace the entire file content with:

- [ ] **Step 1: Rewrite Program.cs**

```csharp
using StackExchange.Redis;
using TrackingService.Models;
using TrackingService.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
    ?? throw new InvalidOperationException("Redis:ConnectionString is not configured.");

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));

builder.Services.AddSingleton<ITimeTrackingRepository, RedisTimeTrackingRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();

app.MapPost("/submissions", async (SubmissionRequest request, ITimeTrackingRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(request.UserId))
        return Results.BadRequest("UserId is required.");

    await repo.RecordSubmissionAsync(request.UserId);
    return Results.NoContent();
})
.WithName("RecordSubmission");

app.MapGet("/submissions/{userId}", async (string userId, ITimeTrackingRepository repo) =>
{
    var timestamp = await repo.GetLastSubmissionAsync(userId);
    return timestamp is null
        ? Results.NotFound()
        : Results.Ok(new SubmissionResponse(userId, timestamp.Value));
})
.WithName("GetUserSubmission");

app.MapGet("/submissions", async (
    string? from,
    string? to,
    ITimeTrackingRepository repo) =>
{
    DateTime? fromDate = null;
    DateTime? toDate = null;

    if (from is not null && !DateTime.TryParse(from, out var parsedFrom))
        return Results.BadRequest("Invalid 'from' date format. Use ISO 8601.");
    else if (from is not null)
        fromDate = DateTime.Parse(from).ToUniversalTime();

    if (to is not null && !DateTime.TryParse(to, out var parsedTo))
        return Results.BadRequest("Invalid 'to' date format. Use ISO 8601.");
    else if (to is not null)
        toDate = DateTime.Parse(to).ToUniversalTime();

    var results = await repo.GetSubmissionsInRangeAsync(fromDate, toDate);
    return Results.Ok(results.Select(r => new SubmissionResponse(r.UserId, r.LastSubmittedAt)));
})
.WithName("GetSubmissions");

app.Run();
```

- [ ] **Step 2: Verify build**

```bash
dotnet build TrackingService
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add TrackingService/Program.cs
git commit -m "feat: wire Redis services and register submission routes"
```

---

## Task 7: Update .http file for manual testing

**Files:**
- Modify: `TrackingService/TrackingService.http`

- [ ] **Step 1: Replace TrackingService.http**

```http
@host = http://localhost:5228

### Record submission for user-1
POST {{host}}/submissions
Content-Type: application/json

{
  "userId": "user-1"
}

###

### Record submission for user-2
POST {{host}}/submissions
Content-Type: application/json

{
  "userId": "user-2"
}

###

### Get last submission for user-1
GET {{host}}/submissions/user-1

###

### Get last submission for unknown user (expect 404)
GET {{host}}/submissions/unknown-user

###

### Get all submissions
GET {{host}}/submissions

###

### Get submissions in a time range (adjust dates as needed)
GET {{host}}/submissions?from=2026-01-01T00:00:00Z&to=2026-12-31T23:59:59Z

###

### Submit with missing userId (expect 400)
POST {{host}}/submissions
Content-Type: application/json

{
  "userId": ""
}
```

- [ ] **Step 2: Start Redis locally (if not running)**

```bash
docker run -d -p 6379:6379 redis
```

Expected: Redis container starts and listens on port 6379.

- [ ] **Step 3: Run the service**

```bash
dotnet run --project TrackingService
```

Expected: service starts on `http://localhost:5228`.

- [ ] **Step 4: Execute the .http requests manually and verify each response**

| Request | Expected response |
|---|---|
| POST /submissions (user-1) | 204 No Content |
| POST /submissions (user-2) | 204 No Content |
| GET /submissions/user-1 | 200 `{ "userId": "user-1", "lastSubmittedAt": "..." }` |
| GET /submissions/unknown-user | 404 Not Found |
| GET /submissions | 200 array with user-1 and user-2 |
| GET /submissions?from=...&to=... | 200 array filtered by range |
| POST with empty userId | 400 Bad Request |

- [ ] **Step 5: Commit**

```bash
git add TrackingService/TrackingService.http
git commit -m "chore: update .http file with submission endpoint tests"
```
