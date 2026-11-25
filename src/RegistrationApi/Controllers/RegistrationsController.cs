using CapacityService.Grpc;
using Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegistrationApi.Data;
using RegistrationApi.Dtos;
using RegistrationApi.Messaging;

namespace RegistrationApi.Controllers;

[ApiController]
[Route("registrations")]
public class RegistrationsController : ControllerBase
{
    private readonly RegistrationDbContext _db;
    private readonly Capacity.CapacityClient _capacity;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<RegistrationsController> _logger;

    public RegistrationsController(RegistrationDbContext db, Capacity.CapacityClient capacity,
        IEventPublisher publisher, ILogger<RegistrationsController> logger)
    {
        _db = db;
        _capacity = capacity;
        _publisher = publisher;
        _logger = logger;
    }

    // Phát sự kiện, lỗi RabbitMQ không được làm hỏng yêu cầu của sinh viên
    private void PublishSafe(string eventType, Registration reg, int remainingSeats)
    {
        try
        {
            _publisher.Publish(new RegistrationEvent(
                EventId: Guid.NewGuid(),
                EventType: eventType,
                RegistrationId: reg.Id,
                StudentId: reg.StudentId,
                CourseId: reg.CourseId,
                RemainingSeats: remainingSeats,
                OccurredAt: DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không phát được sự kiện {Type} cho đăng ký {Id}", eventType, reg.Id);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RegistrationDto>> GetById(int id, CancellationToken ct)
    {
        var reg = await _db.Registrations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return reg is null ? NotFound() : Ok(RegistrationDto.From(reg));
    }

    [HttpPost]
    public async Task<ActionResult<RegistrationDto>> Create(CreateRegistrationRequest req, CancellationToken ct)
    {
        // 1. Sinh viên có tồn tại không?
        if (!await _db.Students.AnyAsync(s => s.Id == req.StudentId, ct))
            return NotFound(new ProblemDetails { Title = "Không tìm thấy sinh viên", Status = 404 });

        // 2. Đăng ký trùng?
        var duplicated = await _db.Registrations.AnyAsync(r =>
            r.StudentId == req.StudentId && r.CourseId == req.CourseId &&
            r.Status == RegistrationStatus.Active, ct);
        if (duplicated)
            return Conflict(new ProblemDetails { Title = "Sinh viên đã đăng ký lớp này", Status = 409 });

        // 3. Kiểm tra còn chỗ (gRPC). Lớp không tồn tại -> RpcException NotFound -> 404
        var check = await _capacity.CheckAvailabilityAsync(
            new CheckRequest { CourseId = req.CourseId }, cancellationToken: ct);
        if (!check.Available)
            return Conflict(new ProblemDetails { Title = "Lớp đã đủ chỗ", Status = 409 });

        // 4. Giữ chỗ (gRPC). Đây mới là bước quyết định, vì giữa bước 3 và bước 4
        //    người khác có thể đã lấy mất chỗ cuối.
        var seat = await _capacity.ReserveSeatAsync(
            new SeatRequest { CourseId = req.CourseId }, cancellationToken: ct);
        if (!seat.Success)
            return Conflict(new ProblemDetails { Title = seat.Message, Status = 409 });

        // 5. Lưu đăng ký. Nếu lưu lỗi thì phải trả chỗ lại (bù trừ).
        var reg = new Registration
        {
            StudentId = req.StudentId,
            CourseId = req.CourseId,
            Status = RegistrationStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        _db.Registrations.Add(reg);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Lưu đăng ký thất bại, trả lại chỗ cho lớp {CourseId}", req.CourseId);
            await _capacity.ReleaseSeatAsync(new SeatRequest { CourseId = req.CourseId });
            return Conflict(new ProblemDetails { Title = "Sinh viên đã đăng ký lớp này", Status = 409 });
        }

        PublishSafe("RegistrationCreated", reg, seat.Remaining);

        var dto = RegistrationDto.From(reg);
        return CreatedAtAction(nameof(GetById), new { id = reg.Id }, dto);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<RegistrationDto>> Cancel(int id, CancellationToken ct)
    {
        var reg = await _db.Registrations.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (reg is null)
            return NotFound(new ProblemDetails { Title = "Không tìm thấy đăng ký", Status = 404 });

        if (reg.Status == RegistrationStatus.Cancelled)
            return Conflict(new ProblemDetails { Title = "Đăng ký này đã được hủy trước đó", Status = 409 });

        reg.Status = RegistrationStatus.Cancelled;
        reg.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Trả chỗ về cho lớp
        var seat = await _capacity.ReleaseSeatAsync(
            new SeatRequest { CourseId = reg.CourseId }, cancellationToken: ct);
        if (!seat.Success)
            _logger.LogError("Hủy đăng ký {Id} nhưng không trả được chỗ cho lớp {CourseId}: {Msg}",
                reg.Id, reg.CourseId, seat.Message);

        PublishSafe("RegistrationCancelled", reg, seat.Remaining);

        return Ok(RegistrationDto.From(reg));
    }
}
