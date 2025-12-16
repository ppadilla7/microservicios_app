using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Security.Infrastructure.Data;
using Security.Domain.Models;

namespace Security.Api.Controllers;

[ApiController]
[Route("rbac")]
public class RbacController : ControllerBase
{
    private readonly SecurityDbContext _db;
    public RbacController(SecurityDbContext db) { _db = db; }

    public record CreateRoleRequest(string Name, string? Description);
    public record CreateResourceRequest(string Name, string? Description);
    public record CreateOperationRequest(string Name, string? Description);
    public record AssignUserRoleRequest(Guid UserId, Guid RoleId);
    public record AssignPermissionRequest(Guid RoleId, Guid ResourceId, Guid OperationId);

    private bool IsAdmin()
    {
        // Admin si el proveedor mapea roles a Role principal
        if (User.IsInRole("admin")) return true;
        // Cubrir proveedores que usan diferentes tipos de claim para roles/grupos
        var roleClaimTypes = new[] { ClaimTypes.Role, "role", "roles", "cognito:groups", "groups" };
        foreach (var t in roleClaimTypes)
        {
            var values = User.Claims.Where(c => c.Type == t).Select(c => c.Value);
            foreach (var v in values)
            {
                if (string.Equals(v, "admin", StringComparison.OrdinalIgnoreCase)) return true;
                // Si viene como lista separada por comas/espacios
                if (!string.IsNullOrEmpty(v))
                {
                    var parts = v.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Any(p => string.Equals(p, "admin", StringComparison.OrdinalIgnoreCase))) return true;
                }
            }
        }
        return false;
    }

    [HttpPost("roles")]
    [Authorize]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest req)
    {
        if (!IsAdmin()) return Forbid();
        var role = new Role { Name = req.Name, Description = req.Description };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return Ok(role);
    }

    [HttpGet("roles")]
    [Authorize]
    public async Task<IActionResult> GetRoles() => Ok(await _db.Roles.ToListAsync());

    [HttpPost("resources")]
    [Authorize]
    public async Task<IActionResult> CreateResource([FromBody] CreateResourceRequest req)
    {
        if (!IsAdmin()) return Forbid();
        var res = new Resource { Name = req.Name, Description = req.Description };
        _db.Resources.Add(res);
        await _db.SaveChangesAsync();
        return Ok(res);
    }

    [HttpGet("resources")]
    [Authorize]
    public async Task<IActionResult> GetResources() => Ok(await _db.Resources.ToListAsync());

    [HttpPost("operations")]
    [Authorize]
    public async Task<IActionResult> CreateOperation([FromBody] CreateOperationRequest req)
    {
        if (!IsAdmin()) return Forbid();
        var op = new Operation { Name = req.Name, Description = req.Description };
        _db.Operations.Add(op);
        await _db.SaveChangesAsync();
        return Ok(op);
    }

    [HttpGet("operations")]
    [Authorize]
    public async Task<IActionResult> GetOperations() => Ok(await _db.Operations.ToListAsync());

    [HttpPost("assign/user-role")]
    [Authorize]
    public async Task<IActionResult> AssignUserRole([FromBody] AssignUserRoleRequest req)
    {
        if (!IsAdmin()) return Forbid();
        var exists = await _db.UserRoles.AnyAsync(ur => ur.UserId == req.UserId && ur.RoleId == req.RoleId);
        if (!exists)
        {
            _db.UserRoles.Add(new UserRole { UserId = req.UserId, RoleId = req.RoleId });
            await _db.SaveChangesAsync();

            // Si el rol asignado es 'estudiante', asegurar perfil en Students.Api por UserId
            try
            {
                var role = await _db.Roles.FindAsync(req.RoleId);
                if (role != null && string.Equals(role.Name, "estudiante", StringComparison.OrdinalIgnoreCase))
                {
                    var user = await _db.Users.FindAsync(req.UserId);
                    if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                    {
                        var fullName = (user.Email.Split('@').FirstOrDefault() ?? user.Email).Trim();
                        using var http = new HttpClient();
                        var baseUrl = Environment.GetEnvironmentVariable("Students__BaseUrl") ?? "http://students:5084";

                        // Verificar si ya existe por UserId
                        var checkResp = await http.GetAsync($"{baseUrl}/api/students/by-user/{req.UserId}");
                        if (checkResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            var payload = System.Text.Json.JsonSerializer.Serialize(new { userId = req.UserId, fullName = fullName, email = user.Email });
                            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                            await http.PostAsync($"{baseUrl}/api/students", content);
                        }
                        // Ignorar errores de duplicado u otros (idempotencia best-effort)
                    }
                }
            }
            catch { /* No bloquear la asignación de rol si falla la creación de perfil */ }
        }
        return Ok(new { message = "assigned" });
    }

    [HttpPost("assign/permission")]
    [Authorize]
    public async Task<IActionResult> AssignPermission([FromBody] AssignPermissionRequest req)
    {
        if (!IsAdmin()) return Forbid();
        var exists = await _db.RolePermissions.AnyAsync(rp => rp.RoleId == req.RoleId && rp.ResourceId == req.ResourceId && rp.OperationId == req.OperationId);
        if (!exists)
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = req.RoleId, ResourceId = req.ResourceId, OperationId = req.OperationId });
            await _db.SaveChangesAsync();
        }
        return Ok(new { message = "assigned" });
    }

    [HttpGet("roles/{roleId}/permissions")]
    [Authorize]
    public async Task<IActionResult> GetRolePermissions(Guid roleId)
    {
        if (!IsAdmin()) return Forbid();
        var role = await _db.Roles.FindAsync(roleId);
        if (role == null) return NotFound("Role not found");
        var perms = await (from rp in _db.RolePermissions
                           where rp.RoleId == roleId
                           join res in _db.Resources on rp.ResourceId equals res.Id
                           join op in _db.Operations on rp.OperationId equals op.Id
                           select new { id = rp.Id, resource = res.Name, operation = op.Name }).ToListAsync();
        return Ok(new { role = role.Name, permissions = perms });
    }

    [HttpDelete("permissions/{id}")]
    [Authorize]
    public async Task<IActionResult> RemovePermission(Guid id)
    {
        if (!IsAdmin()) return Forbid();
        var rp = await _db.RolePermissions.FindAsync(id);
        if (rp == null) return NotFound("Permission not found");
        _db.RolePermissions.Remove(rp);
        await _db.SaveChangesAsync();
        return Ok(new { message = "removed", id });
    }
}