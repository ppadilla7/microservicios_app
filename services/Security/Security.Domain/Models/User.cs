using System;
using System.Collections.Generic;

namespace Security.Domain.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public bool IsMfaEnabled { get; set; }
    public string? MfaSecret { get; set; }
    public string? ExternalProvider { get; set; }
    public string? ExternalId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}