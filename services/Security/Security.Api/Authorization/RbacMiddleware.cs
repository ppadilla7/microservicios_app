using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Security.Api.Authorization;
using Security.Infrastructure.Data;

namespace Security.Api.Authorization;

public class RbacMiddleware
{
    private readonly RequestDelegate _next;
    public RbacMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, SecurityDbContext db)
    {
        var endpoint = context.GetEndpoint();
        var resourceAttr = endpoint?.Metadata.GetMetadata<ResourceAttribute>();
        var operationAttr = endpoint?.Metadata.GetMetadata<OperationAttribute>();

        if (resourceAttr == null || operationAttr == null)
        {
            await _next(context);
            return;
        }

        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                          ?? context.User.Claims.FirstOrDefault(c => c.Type == "sub");
        if (userIdClaim == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        var userId = System.Guid.Parse(userIdClaim.Value);

        var resName = resourceAttr.Name?.Trim().ToLowerInvariant();
        var opName = operationAttr.Name?.Trim().ToLowerInvariant();

        var hasAccess = await db.RolePermissions
            .Include(rp => rp.Role)
            .Include(rp => rp.Resource)
            .Include(rp => rp.Operation)
            .Where(rp => rp.Resource.Name.ToLower() == resName && rp.Operation.Name.ToLower() == opName)
            .AnyAsync(rp => db.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == rp.RoleId));

        if (!hasAccess)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden");
            return;
        }

        await _next(context);
    }
}