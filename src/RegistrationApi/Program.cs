using CapacityService.Grpc;
using Grpc.Core;
using Grpc.Net.Client;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5000");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cho phép kết nối gRPC unencrypted HTTP/2
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

builder.Services.AddSingleton(services =>
{
    var channel = GrpcChannel.ForAddress("http://localhost:5101");
    return new Capacity.CapacityClient(channel);
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Capacity Test API v1");
    c.RoutePrefix = "swagger";
});

// API endpoints
app.MapGet("/api/test/courses", async (Capacity.CapacityClient client) =>
{
    var reply = await client.ListCoursesAsync(new ListCoursesRequest());
    return Results.Ok(reply.Courses.Select(c => new
    {
        id = c.Id,
        code = c.Code,
        name = c.Name,
        capacity = c.Capacity,
        registered = c.Registered,
        remaining = c.Remaining
    }));
});

app.MapGet("/api/test/courses/{id:int}", async (int id, Capacity.CapacityClient client) =>
{
    try
    {
        var c = await client.GetCourseAsync(new GetCourseRequest { CourseId = id });
        return Results.Ok(new
        {
            id = c.Id,
            code = c.Code,
            name = c.Name,
            capacity = c.Capacity,
            registered = c.Registered,
            remaining = c.Remaining
        });
    }
    catch (RpcException ex)
    {
        return Results.NotFound(new { error = ex.Status.Detail, statusCode = ex.StatusCode.ToString() });
    }
});

app.MapGet("/api/test/courses/{id:int}/availability", async (int id, Capacity.CapacityClient client) =>
{
    var reply = await client.CheckAvailabilityAsync(new CheckRequest { CourseId = id });
    return Results.Ok(new { available = reply.Available, remaining = reply.Remaining });
});

app.MapPost("/api/test/courses/{id:int}/reserve", async (int id, Capacity.CapacityClient client) =>
{
    var reply = await client.ReserveSeatAsync(new SeatRequest { CourseId = id });
    return Results.Ok(new { success = reply.Success, message = reply.Message, remaining = reply.Remaining });
});

app.MapPost("/api/test/courses/{id:int}/release", async (int id, Capacity.CapacityClient client) =>
{
    var reply = await client.ReleaseSeatAsync(new SeatRequest { CourseId = id });
    return Results.Ok(new { success = reply.Success, message = reply.Message, remaining = reply.Remaining });
});

app.MapPost("/api/test/reset-course-1", async (Capacity.CapacityClient client) =>
{
    var c = await client.GetCourseAsync(new GetCourseRequest { CourseId = 1 });
    while (c.Registered > 0)
    {
        await client.ReleaseSeatAsync(new SeatRequest { CourseId = 1 });
        c = await client.GetCourseAsync(new GetCourseRequest { CourseId = 1 });
    }
    return Results.Ok(new { message = "Lớp 1 đã được đưa về registered = 0", registered = c.Registered, remaining = c.Remaining });
});

app.MapPost("/api/test/run-all-tests", async (Capacity.CapacityClient client) =>
{
    var results = new List<object>();

    // 1. ListCourses
    var list = await client.ListCoursesAsync(new ListCoursesRequest());
    var course1 = list.Courses.FirstOrDefault(x => x.Id == 1);
    results.Add(new { step = 1, test = "ListCourses", expected = "3 lớp, lớp 1 có remaining = 3", actual = $"Tìm thấy {list.Courses.Count} lớp, lớp 1 remaining = {course1?.Remaining}", pass = list.Courses.Count == 3 && course1?.Remaining == 3 });

    // 2. CheckAvailability
    var check1 = await client.CheckAvailabilityAsync(new CheckRequest { CourseId = 1 });
    results.Add(new { step = 2, test = "CheckAvailability (course_id: 1)", expected = "available = true, remaining = 3", actual = $"available = {check1.Available}, remaining = {check1.Remaining}", pass = check1.Available && check1.Remaining == 3 });

    // 3. ReserveSeat x3
    var r1 = await client.ReserveSeatAsync(new SeatRequest { CourseId = 1 });
    var r2 = await client.ReserveSeatAsync(new SeatRequest { CourseId = 1 });
    var r3 = await client.ReserveSeatAsync(new SeatRequest { CourseId = 1 });
    bool pass3 = r1.Success && r2.Success && r3.Success && r1.Remaining == 2 && r2.Remaining == 1 && r3.Remaining == 0;
    results.Add(new { step = 3, test = "ReserveSeat x3 (course_id: 1)", expected = "success = true, remaining = 2, 1, 0", actual = $"L1(rem={r1.Remaining}), L2(rem={r2.Remaining}), L3(rem={r3.Remaining})", pass = pass3 });

    // 4. ReserveSeat lần 4
    var r4 = await client.ReserveSeatAsync(new SeatRequest { CourseId = 1 });
    results.Add(new { step = 4, test = "ReserveSeat lần 4 (course_id: 1)", expected = "success = false, 'Lớp đã đủ chỗ'", actual = $"success = {r4.Success}, msg = '{r4.Message}'", pass = !r4.Success && r4.Message == "Lớp đã đủ chỗ" });

    // 5. CheckAvailability lần 2
    var check2 = await client.CheckAvailabilityAsync(new CheckRequest { CourseId = 1 });
    results.Add(new { step = 5, test = "CheckAvailability lần 2", expected = "available = false, remaining = 0", actual = $"available = {check2.Available}, remaining = {check2.Remaining}", pass = !check2.Available && check2.Remaining == 0 });

    // 6. ReleaseSeat
    var rel = await client.ReleaseSeatAsync(new SeatRequest { CourseId = 1 });
    results.Add(new { step = 6, test = "ReleaseSeat (course_id: 1)", expected = "success = true, remaining = 1", actual = $"success = {rel.Success}, remaining = {rel.Remaining}", pass = rel.Success && rel.Remaining == 1 });

    // 7. GetCourse 999
    try
    {
        await client.GetCourseAsync(new GetCourseRequest { CourseId = 999 });
        results.Add(new { step = 7, test = "GetCourse (course_id: 999)", expected = "lỗi NotFound", actual = "Không báo lỗi", pass = false });
    }
    catch (RpcException ex)
    {
        results.Add(new { step = 7, test = "GetCourse (course_id: 999)", expected = "lỗi NotFound", actual = $"Bắt được {ex.StatusCode}: {ex.Status.Detail}", pass = ex.StatusCode == StatusCode.NotFound });
    }

    // Dọn dẹp: trả tiếp 2 chỗ để lớp 1 về 0
    await client.ReleaseSeatAsync(new SeatRequest { CourseId = 1 });
    await client.ReleaseSeatAsync(new SeatRequest { CourseId = 1 });

    return Results.Ok(results);
});

app.Run();
