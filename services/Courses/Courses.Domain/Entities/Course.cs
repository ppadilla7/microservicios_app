namespace Courses.Domain.Entities;

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Credits { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}