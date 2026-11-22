namespace RegistrationApi.Data;

public enum RegistrationStatus { Active, Cancelled }

public class Registration
{
    public int Id { get; set; }
    public string StudentId { get; set; } = "";
    public int CourseId { get; set; }
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
