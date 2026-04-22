namespace TrackingService.Repositories;

public interface ITimeTrackingRepository
{
    Task RecordSubmissionAsync(string userId);
    Task<DateTimeOffset?> GetLastSubmissionAsync(string userId);
    Task<IEnumerable<(string UserId, DateTimeOffset LastSubmittedAt)>> GetSubmissionsInRangeAsync(DateTimeOffset? from, DateTimeOffset? to);
}
