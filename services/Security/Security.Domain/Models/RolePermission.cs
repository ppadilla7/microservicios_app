using System;

namespace Security.Domain.Models;

public class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;
    public Guid OperationId { get; set; }
    public Operation Operation { get; set; } = null!;
}