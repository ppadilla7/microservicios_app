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
[Route("api/students")]
public class StudentsController : ControllerBase
{
    private readonly SecurityDbContext _db;
    public StudentsController(SecurityDbContext db) { _db = db; }

    public record CreateStudentRequest(string StudentNumber, string FullName, string? Email);
    public record UpdateStudentRequest(string? StudentNumber, string? FullName, string? Email);

    [Authorize]
    [HttpGet]
    [Resource("students"), Operation("read")]
    public async Task<IActionResult> List()
    {
        var items = await _db.Students.OrderBy(s => s.StudentNumber).ToListAsync();
        return Ok(items);
    }

    [Authorize]
    [HttpPost]
    [Resource("students"), Operation("create")]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.StudentNumber) || string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest("StudentNumber and FullName are required");
        if (await _db.Students.AnyAsync(s => s.StudentNumber == req.StudentNumber))
            return BadRequest("Student number already exists");
        var student = new Student { StudentNumber = req.StudentNumber, FullName = req.FullName, Email = req.Email };
        _db.Students.Add(student);
        await _db.SaveChangesAsync();
        return Ok(student);
    }

    [Authorize]
    [HttpPut("{id}")]
    [Resource("students"), Operation("update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStudentRequest req)
    {
        var student = await _db.Students.FindAsync(id);
        if (student == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(req.StudentNumber)) student.StudentNumber = req.StudentNumber;
        if (!string.IsNullOrWhiteSpace(req.FullName)) student.FullName = req.FullName;
        student.Email = req.Email;
        await _db.SaveChangesAsync();
        return Ok(student);
    }

    [Authorize]
    [HttpDelete("{id}")]
    [Resource("students"), Operation("delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var student = await _db.Students.FindAsync(id);
        if (student == null) return NotFound();
        _db.Students.Remove(student);
        await _db.SaveChangesAsync();
        return Ok(new { message = "deleted" });
    }
}