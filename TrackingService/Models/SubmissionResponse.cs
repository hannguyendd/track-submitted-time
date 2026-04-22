namespace TrackingService.Models;

public record SubmissionResponse(string UserId, DateTimeOffset LastSubmittedAt);
