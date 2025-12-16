using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Enrollment.Infrastructure;
using Enrollment.Domain.Entities;
using BuildingBlocks.Messaging;

namespace Enrollment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EnrollmentsController : ControllerBase
{
    private readonly EnrollmentDbContext _db;
    private readonly IEventBus _eventBus;

    public EnrollmentsController(EnrollmentDbContext db, IEventBus eventBus)
    {
        _db = db;
        _eventBus = eventBus;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Enrollment.Domain.Entities.Enrollment>>> Get()
    {
        var list = await _db.Enrollments.OrderBy(e => e.Id).ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Enrollment.Domain.Entities.Enrollment>> GetById(Guid id)
    {
        var enrollment = await _db.Enrollments.FindAsync(id);
        if (enrollment is null) return NotFound();
        return Ok(enrollment);
    }

    [HttpPost]
    public async Task<ActionResult<Enrollment.Domain.Entities.Enrollment>> Create([FromBody] CreateEnrollmentRequest request)
    {
        var enrollment = new Enrollment.Domain.Entities.Enrollment
        {
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            EnrolledAt = DateTime.UtcNow
        };

        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync();

        // Publicar evento de matrícula creada
        var evt = new EnrollmentCreatedEvent
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            CourseId = enrollment.CourseId,
            EnrolledAt = enrollment.EnrolledAt
        };
        await _eventBus.PublishAsync("university.events", "enrollment.created", evt);

        return CreatedAtAction(nameof(GetById), new { id = enrollment.Id }, enrollment);
    }

    public record CreateEnrollmentRequest(Guid StudentId, Guid CourseId);

    public class EnrollmentCreatedEvent
    {
        public Guid Id { get; set; }
        public Guid StudentId { get; set; }
        public Guid CourseId { get; set; }
        public DateTime EnrolledAt { get; set; }
    }
}