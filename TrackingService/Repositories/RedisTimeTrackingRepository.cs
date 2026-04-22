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

    public async Task<DateTimeOffset?> GetLastSubmissionAsync(string userId)
    {
        var score = await _db.SortedSetScoreAsync(Key, userId);
        if (score is null) return null;
        return DateTimeOffset.FromUnixTimeSeconds((long)score.Value);
    }

    public async Task<IEnumerable<(string UserId, DateTimeOffset LastSubmittedAt)>> GetSubmissionsInRangeAsync(
        DateTimeOffset? from, DateTimeOffset? to)
    {
        var min = from.HasValue
            ? (double)from.Value.ToUnixTimeSeconds()
            : double.NegativeInfinity;

        var max = to.HasValue
            ? (double)to.Value.ToUnixTimeSeconds()
            : double.PositiveInfinity;

        var entries = await _db.SortedSetRangeByScoreWithScoresAsync(Key, min, max);

        return entries.Select(e => (
            UserId: (string)e.Element!,
            LastSubmittedAt: DateTimeOffset.FromUnixTimeSeconds((long)e.Score)
        ));
    }
}
