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
    DateTimeOffset? fromDate = null;
    DateTimeOffset? toDate = null;

    if (from is not null && !DateTimeOffset.TryParse(from, out var parsedFrom))
        return Results.BadRequest("Invalid 'from' date format. Use ISO 8601.");
    else if (from is not null)
        fromDate = DateTimeOffset.Parse(from).ToUniversalTime();

    if (to is not null && !DateTimeOffset.TryParse(to, out var parsedTo))
        return Results.BadRequest("Invalid 'to' date format. Use ISO 8601.");
    else if (to is not null)
        toDate = DateTimeOffset.Parse(to).ToUniversalTime();

    var results = await repo.GetSubmissionsInRangeAsync(fromDate, toDate);
    return Results.Ok(results.Select(r => new SubmissionResponse(r.UserId, r.LastSubmittedAt)));
})
.WithName("GetSubmissions");

app.Run();
