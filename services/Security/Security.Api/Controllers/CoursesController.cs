using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Security.Api.Authorization;
using Security.Infrastructure.Data;
using Security.Domain.Models;

namespace Security.Api.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly SecurityDbContext _db;
    public CoursesController(SecurityDbContext db) { _db = db; }

    public record CreateCourseRequest(string Code, string Name, string? Description);
    public record UpdateCourseRequest(string? Code, string? Name, string? Description);

    [Authorize]
    [HttpGet]
    [Resource("courses"), Operation("read")]
    public async Task<IActionResult> List()
    {
        var items = await _db.Courses.OrderBy(c => c.Code).ToListAsync();
        return Ok(items);
    }

    [Authorize]
    [HttpPost]
    [Resource("courses"), Operation("create")]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Code and Name are required");
        if (await _db.Courses.AnyAsync(c => c.Code == req.Code))
            return BadRequest("Course code already exists");
        var course = new Course { Code = req.Code, Name = req.Name, Description = req.Description };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();
        return Ok(course);
    }

    [Authorize]
    [HttpPut("{id}")]
    [Resource("courses"), Operation("update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseRequest req)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(req.Code)) course.Code = req.Code;
        if (!string.IsNullOrWhiteSpace(req.Name)) course.Name = req.Name;
        course.Description = req.Description;
        await _db.SaveChangesAsync();
        return Ok(course);
    }

    [Authorize]
    [HttpDelete("{id}")]
    [Resource("courses"), Operation("delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();
        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();
        return Ok(new { message = "deleted" });
    }
}