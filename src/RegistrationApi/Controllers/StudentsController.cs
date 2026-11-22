using CapacityService.Grpc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegistrationApi.Data;
using RegistrationApi.Dtos;

namespace RegistrationApi.Controllers;

[ApiController]
[Route("students")]
public class StudentsController : ControllerBase
{
    private readonly RegistrationDbContext _db;
    private readonly Capacity.CapacityClient _capacity;

    public StudentsController(RegistrationDbContext db, Capacity.CapacityClient capacity)
    {
        _db = db;
        _capacity = capacity;
    }

    [HttpGet("{studentId}/registrations")]
    public async Task<ActionResult<IEnumerable<RegistrationDto>>> GetRegistrations(
        string studentId, CancellationToken ct)
    {
        if (!await _db.Students.AnyAsync(s => s.Id == studentId, ct))
            return NotFound(new ProblemDetails { Title = "Không tìm thấy sinh viên", Status = 404 });

        var regs = await _db.Registrations.AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        // Lấy thông tin lớp (mã, tên) từ CapacityService bằng 1 lần gọi gRPC
        var courses = (await _capacity.ListCoursesAsync(new ListCoursesRequest(), cancellationToken: ct))
            .Courses.ToDictionary(c => c.Id);

        return Ok(regs.Select(r =>
            RegistrationDto.From(r, courses.TryGetValue(r.CourseId, out var c) ? c : null)));
    }
}
