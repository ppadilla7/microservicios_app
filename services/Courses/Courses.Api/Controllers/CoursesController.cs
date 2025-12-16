using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Courses.Infrastructure;
using Courses.Domain.Entities;

namespace Courses.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly CoursesDbContext _db;

    public CoursesController(CoursesDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Course>>> Get()
    {
        var list = await _db.Courses.OrderBy(c => c.Id).ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Course>> GetById(Guid id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course is null) return NotFound();
        return Ok(course);
    }

    [HttpPost]
    public async Task<ActionResult<Course>> Create([FromBody] CreateCourseRequest request)
    {
        var course = new Course
        {
            Code = request.Code,
            Name = request.Name,
            Credits = request.Credits,
            CreatedAt = DateTime.UtcNow
        };

        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = course.Id }, course);
    }

    public record CreateCourseRequest(string Code, string Name, int Credits);
}