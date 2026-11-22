using CapacityService.Grpc;
using Microsoft.AspNetCore.Mvc;
using RegistrationApi.Dtos;

namespace RegistrationApi.Controllers;

[ApiController]
[Route("courses")]
public class CoursesController : ControllerBase
{
    private readonly Capacity.CapacityClient _capacity;
    public CoursesController(Capacity.CapacityClient capacity) => _capacity = capacity;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CourseDto>>> GetAll(CancellationToken ct)
    {
        var reply = await _capacity.ListCoursesAsync(new ListCoursesRequest(), cancellationToken: ct);
        return Ok(reply.Courses.Select(CourseDto.From));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CourseDto>> GetById(int id, CancellationToken ct)
    {
        var reply = await _capacity.GetCourseAsync(new GetCourseRequest { CourseId = id }, cancellationToken: ct);
        return Ok(CourseDto.From(reply));
    }
}
