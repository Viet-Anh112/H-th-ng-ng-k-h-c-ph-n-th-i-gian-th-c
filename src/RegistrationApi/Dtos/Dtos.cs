using System.ComponentModel.DataAnnotations;
using RegistrationApi.Data;
using CapacityService.Grpc;

namespace RegistrationApi.Dtos;

public record CourseDto(int Id, string Code, string Name, int Capacity, int Registered, int Remaining)
{
    public static CourseDto From(CourseReply c) =>
        new(c.Id, c.Code, c.Name, c.Capacity, c.Registered, c.Remaining);
}

public record StudentDto(string Id, string FullName)
{
    public static StudentDto From(Student s) => new(s.Id, s.FullName);
}

public class CreateRegistrationRequest
{
    [Required, StringLength(20)]
    public string StudentId { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int CourseId { get; set; }
}

public record RegistrationDto(
    int Id,
    string StudentId,
    int CourseId,
    string? CourseCode,
    string? CourseName,
    RegistrationStatus Status,
    DateTime CreatedAt,
    DateTime? CancelledAt)
{
    public static RegistrationDto From(Registration r, CourseReply? c = null) =>
        new(r.Id, r.StudentId, r.CourseId, c?.Code, c?.Name, r.Status, r.CreatedAt, r.CancelledAt);
}
