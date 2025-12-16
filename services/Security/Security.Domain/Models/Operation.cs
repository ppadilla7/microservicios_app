using System;
using System.Collections.Generic;

namespace Security.Domain.Models;

public class Operation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; // e.g., read, write, delete
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}