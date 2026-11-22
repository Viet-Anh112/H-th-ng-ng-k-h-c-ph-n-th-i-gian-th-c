namespace Contracts;

public record RegistrationEvent(
    Guid EventId,
    string EventType,       // "RegistrationCreated" hoặc "RegistrationCancelled"
    int RegistrationId,
    string StudentId,
    int CourseId,
    int RemainingSeats,
    DateTime OccurredAt
);
