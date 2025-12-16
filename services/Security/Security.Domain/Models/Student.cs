using System;

namespace Security.Domain.Models;

public class Student
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}