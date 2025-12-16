using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Students.Infrastructure;
using Students.Domain.Entities;

namespace Students.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly StudentsDbContext _db;

    public StudentsController(StudentsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Student>>> Get()
    {
        var list = await _db.Students.OrderBy(s => s.Id).ToListAsync();
        return Ok(list);
    }

    [HttpGet("by-email/{email}")]
    public async Task<ActionResult<Student?>> GetByEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email requerido");
        var lowered = email.ToLowerInvariant();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Email.ToLower() == lowered);
        if (student is null) return NotFound();
        return Ok(student);
    }

    [HttpGet("by-user/{userId:guid}")]
    public async Task<ActionResult<Student?>> GetByUser(Guid userId)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.UserId == userId);
        if (student is null) return NotFound();
        return Ok(student);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Student>> GetById(Guid id)
    {
        var student = await _db.Students.FindAsync(id);
        if (student is null) return NotFound();
        return Ok(student);
    }

    [HttpPost]
    public async Task<ActionResult<Student>> Create([FromBody] CreateStudentRequest request)
    {
        var student = new Student
        {
            UserId = request.UserId,
            FullName = request.FullName,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow
        };

        _db.Students.Add(student);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = student.Id }, student);
    }

    public record CreateStudentRequest(Guid UserId, string FullName, string Email);
}