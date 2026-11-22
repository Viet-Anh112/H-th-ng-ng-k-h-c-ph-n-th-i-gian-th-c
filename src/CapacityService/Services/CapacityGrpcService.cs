using CapacityService.Data;
using CapacityService.Grpc;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace CapacityService.Services;

public class CapacityGrpcService : Capacity.CapacityBase
{
    private readonly CapacityDbContext _db;
    public CapacityGrpcService(CapacityDbContext db) => _db = db;

    private static CourseReply Map(Course c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        Capacity = c.Capacity,
        Registered = c.Registered,
        Remaining = c.Capacity - c.Registered
    };

    private async Task<Course> GetOrThrow(int id)
    {
        var c = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return c ?? throw new RpcException(new Status(StatusCode.NotFound, "Không tìm thấy lớp học phần"));
    }

    public override async Task<CourseReply> GetCourse(GetCourseRequest request, ServerCallContext context)
        => Map(await GetOrThrow(request.CourseId));

    public override async Task<ListCoursesReply> ListCourses(ListCoursesRequest request, ServerCallContext context)
    {
        var list = await _db.Courses.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var reply = new ListCoursesReply();
        reply.Courses.AddRange(list.Select(Map));
        return reply;
    }

    public override async Task<CheckReply> CheckAvailability(CheckRequest request, ServerCallContext context)
    {
        var c = await GetOrThrow(request.CourseId);
        var remaining = c.Capacity - c.Registered;
        return new CheckReply { Available = remaining > 0, Remaining = remaining };
    }

    public override async Task<SeatReply> ReserveSeat(SeatRequest request, ServerCallContext context)
    {
        // UPDATE nguyên tử: chỉ tăng khi còn chỗ
        var affected = await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Courses""
               SET ""Registered"" = ""Registered"" + 1
               WHERE ""Id"" = {request.CourseId} AND ""Registered"" < ""Capacity""");

        var c = await GetOrThrow(request.CourseId);
        var remaining = c.Capacity - c.Registered;

        return affected == 1
            ? new SeatReply { Success = true,  Message = "OK",            Remaining = remaining }
            : new SeatReply { Success = false, Message = "Lớp đã đủ chỗ", Remaining = remaining };
    }

    public override async Task<SeatReply> ReleaseSeat(SeatRequest request, ServerCallContext context)
    {
        var affected = await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Courses""
               SET ""Registered"" = ""Registered"" - 1
               WHERE ""Id"" = {request.CourseId} AND ""Registered"" > 0");

        var c = await GetOrThrow(request.CourseId);
        var remaining = c.Capacity - c.Registered;

        return new SeatReply
        {
            Success = affected == 1,
            Message = affected == 1 ? "OK" : "Không có chỗ nào để trả",
            Remaining = remaining
        };
    }
}
