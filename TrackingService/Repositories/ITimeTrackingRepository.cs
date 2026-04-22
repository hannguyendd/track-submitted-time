namespace TrackingService.Repositories;

public interface ITimeTrackingRepository
{
    Task RecordSubmissionAsync(string userId);
    Task<DateTime?> GetLastSubmissionAsync(string userId);
    Task<IEnumerable<(string UserId, DateTime LastSubmittedAt)>> GetSubmissionsInRangeAsync(DateTime? from, DateTime? to);
}
