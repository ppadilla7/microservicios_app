using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using BCrypt.Net;
using OtpNet;
using Security.Infrastructure.Data;
using Security.Domain.Models;
using Security.Application.Services;

namespace Security.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly SecurityDbContext _db;
    private readonly TokenService _tokens;
    private readonly ILogger<AuthController> _logger;
    public AuthController(SecurityDbContext db, TokenService tokens, ILogger<AuthController> logger)
    {
        _db = db; _tokens = tokens; _logger = logger;
    }

    public record RegisterRequest(string Email, string Password);
    public record LoginRequest(string Email, string Password);
    public record MfaSetupResponse(string Secret, string OtpauthUrl);
    public record MfaVerifyRequest(string Code, string PendingToken);
    public record MeResponse(string Email, bool IsMfaEnabled);
    public record MfaSetupPendingRequest(string PendingToken);
    public record UserSummary(Guid Id, string Email, bool IsMfaEnabled, IEnumerable<string> Roles);
    public record ToggleMfaRequest(Guid UserId, bool Enable);
    public record HasPermissionQuery(string Resource, string Operation);
    public record UpdateUserRequest(string? Email, string? Password);

    private bool IsAdmin()
    {
        if (User.IsInRole("admin")) return true;
        var roleClaimTypes = new[] { System.Security.Claims.ClaimTypes.Role, "role", "roles", "cognito:groups", "groups" };
        foreach (var t in roleClaimTypes)
        {
            var values = User.Claims.Where(c => c.Type == t).Select(c => c.Value);
            foreach (var v in values)
            {
                if (string.Equals(v, "admin", StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(v))
                {
                    var parts = v.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Any(p => string.Equals(p, "admin", StringComparison.OrdinalIgnoreCase))) return true;
                }
            }
        }
        return false;
    }

    private Guid? GetUserIdFromClaims()
    {
        var sub = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                  ?? User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(sub) && Guid.TryParse(sub, out var parsed)) return parsed;
        return null;
    }

    private async Task<bool> HasPermission(Guid userId, string resourceName, string operationName)
    {
        var r = (resourceName ?? "").Trim().ToLowerInvariant();
        var o = (operationName ?? "").Trim().ToLowerInvariant();
        var q = from ur in _db.UserRoles
                where ur.UserId == userId
                join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
                join res in _db.Resources on rp.ResourceId equals res.Id
                join op in _db.Operations on rp.OperationId equals op.Id
                where EF.Functions.Like(res.Name.ToLower(), r) && EF.Functions.Like(op.Name.ToLower(), o)
                select rp;
        return await q.AnyAsync();
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return BadRequest("Email already exists");
        var user = new User { Email = req.Email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password) };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(new { message = "registered" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null || user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized();

        // Bootstrap: if no user-role assignments exist yet, assign admin to the first authenticated user
        if (!await _db.UserRoles.AnyAsync())
        {
            var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "admin");
            if (adminRole != null && !await _db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == adminRole.Id))
            {
                _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
                await _db.SaveChangesAsync();
            }
        }

        var roles = await _db.UserRoles.Where(ur => ur.UserId == user.Id)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).ToListAsync();

        if (user.IsMfaEnabled)
        {
            var pending = _tokens.CreateAccessToken(user, roles, new System.Collections.Generic.Dictionary<string, string> { { "mfa_pending", "true" } });
            return Ok(new { mfaRequired = true, roles, pendingToken = pending });
        }

        var token = _tokens.CreateAccessToken(user, roles);
        return Ok(new { token, roles });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        _logger.LogInformation("Auth.Me start path={Path} traceId={TraceId}", HttpContext.Request.Path, HttpContext.TraceIdentifier);
        // Preferir el userId (sub) para buscar al usuario; si no, caer a email
        var sub = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        _logger.LogInformation("Auth.Me claims: sub={Sub}", sub ?? "<null>");
        var nameId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        _logger.LogInformation("Auth.Me claims: nameIdentifier={NameId}", nameId ?? "<null>");
        User? user = null;
        if (!string.IsNullOrEmpty(sub) && Guid.TryParse(sub, out var userId))
        {
            _logger.LogInformation("Auth.Me lookup by sub parsed userId={UserId}", userId);
            user = await _db.Users.FindAsync(userId);
            _logger.LogInformation("Auth.Me lookup by sub result found={Found}", user != null);
        }
        if (user == null)
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value
                        ?? User.Claims.FirstOrDefault(c => c.Type == "email")?.Value
                        ?? User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            _logger.LogInformation("Auth.Me fallback claim email={Email}", email ?? "<null>");
            if (email == null) return Unauthorized();
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            _logger.LogInformation("Auth.Me lookup by email result found={Found}", user != null);
        }
        if (user == null)
        {
            _logger.LogWarning("Auth.Me unauthorized: user not found via sub/email. traceId={TraceId}", HttpContext.TraceIdentifier);
            return Unauthorized();
        }
        _logger.LogInformation("Auth.Me success: email={Email} isMfaEnabled={IsMfa} traceId={TraceId}", user.Email, user.IsMfaEnabled, HttpContext.TraceIdentifier);
        return Ok(new MeResponse(user.Email, user.IsMfaEnabled));
    }

    [Authorize]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
      
        var users = await _db.Users
            .Select(u => new UserSummary(
                u.Id,
                u.Email,
                u.IsMfaEnabled,
                _db.UserRoles.Where(ur => ur.UserId == u.Id).Select(ur => ur.Role.Name).ToList()
            ))
            .ToListAsync();
        return Ok(users);
    }

    [Authorize]
    [HttpPost("mfa/toggle")]
    public async Task<IActionResult> ToggleMfa([FromBody] ToggleMfaRequest req)
    {
        bool isAdmin = IsAdmin();
        var uid = GetUserIdFromClaims();
        if (!isAdmin)
        {
            if (uid == null) return Forbid();
            if (uid.Value != req.UserId)
            {
                if (!await HasPermission(uid.Value, "users", "update")) return Forbid();
            }
        }
        var user = await _db.Users.FindAsync(req.UserId);
        if (user == null) return NotFound("User not found");
        user.IsMfaEnabled = req.Enable;
        if (!req.Enable)
        {
            user.MfaSecret = null;
        }
        await _db.SaveChangesAsync();
        return Ok(new { message = "mfa_updated", userId = user.Id, isMfaEnabled = user.IsMfaEnabled });
    }

    [Authorize]
    [HttpPost("mfa/setup")]
    public async Task<IActionResult> SetupMfa()
    {
        var email = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value
                    ?? User.Claims.FirstOrDefault(c => c.Type == "email")?.Value
                    ?? User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return Unauthorized();

        var secretKey = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretKey);
        var otpauth = new OtpUri(OtpType.Totp, secret, user.Email, "Security.Api").ToString();

        user.MfaSecret = secret;
        user.IsMfaEnabled = true;
        await _db.SaveChangesAsync();

        return Ok(new MfaSetupResponse(secret, otpauth));
    }

    [HttpPost("mfa/setup/pending")]
    public async Task<IActionResult> SetupMfaWithPending([FromBody] MfaSetupPendingRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PendingToken)) return BadRequest("Missing pending token");
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(req.PendingToken);
        if (!jwt.Claims.Any(c => c.Type == "mfa_pending" && c.Value == "true"))
            return BadRequest("Invalid pending token");
        var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (userIdClaim == null) return Unauthorized();
        var userId = Guid.Parse(userIdClaim.Value);

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        var secretKey = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretKey);
        var otpauth = new OtpUri(OtpType.Totp, secret, user.Email, "Security.Api").ToString();

        user.MfaSecret = secret;
        user.IsMfaEnabled = true;
        await _db.SaveChangesAsync();

        return Ok(new MfaSetupResponse(secret, otpauth));
    }

    [HttpPost("mfa/verify")]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequest req)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(req.PendingToken);
        if (!jwt.Claims.Any(c => c.Type == "mfa_pending" && c.Value == "true"))
            return BadRequest("Invalid pending token");
        var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (userIdClaim == null) return Unauthorized();
        var userId = Guid.Parse(userIdClaim.Value);

        var user = await _db.Users.FindAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.MfaSecret)) return Unauthorized();

        var totp = new Totp(Base32Encoding.ToBytes(user.MfaSecret));
        // Permitir pequeño desfase temporal (+/- 1 paso de 30s)
        var ok = totp.VerifyTotp(req.Code, out _, new VerificationWindow(previous: 1, future: 1));
        if (!ok) return Unauthorized();

        var roles = await _db.UserRoles.Where(ur => ur.UserId == user.Id)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).ToListAsync();
        var token = _tokens.CreateAccessToken(user, roles);
        return Ok(new { token });
    }

    [HttpGet("google")]
    public IActionResult SignInWithGoogle()
    {
        var props = new AuthenticationProperties { RedirectUri = Url.ActionLink(nameof(ExternalCallback)) };
        return Challenge(props, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("github")]
    public IActionResult SignInWithGitHub()
    {
        var props = new AuthenticationProperties { RedirectUri = Url.ActionLink(nameof(ExternalCallback)) };
        return Challenge(props, GitHubAuthenticationDefaults.AuthenticationScheme);
    }

    [HttpGet("external/callback")]
    public async Task<IActionResult> ExternalCallback()
    {
        // Try to authenticate using the provider-specific schemes first (Google/GitHub),
        // then fall back to the default scheme if available.
        var googleResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        var githubResult = await HttpContext.AuthenticateAsync(GitHubAuthenticationDefaults.AuthenticationScheme);
        var result = googleResult?.Succeeded == true ? googleResult :
                     githubResult?.Succeeded == true ? githubResult :
                     await HttpContext.AuthenticateAsync();

        if (result?.Succeeded != true || result.Principal == null)
            return BadRequest("External login failed");

        var email = result.Principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? result.Principal.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        var nameId = result.Principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var provider = result.Properties?.Items.TryGetValue(".AuthScheme", out var s) == true ? s : "external";
        if (email == null && nameId == null) return BadRequest("External login failed");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email) 
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.ExternalProvider == provider && u.ExternalId == nameId);
        if (user == null)
        {
            user = new User { Email = email ?? ($"{provider}:{nameId}"), ExternalProvider = provider, ExternalId = nameId };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        var roles = await _db.UserRoles.Where(ur => ur.UserId == user.Id)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).ToListAsync();

        // If MFA is enabled for the user, issue a pending token (no access until verification)
        string? token = null;
        string? pending = null;
        if (user.IsMfaEnabled)
        {
            pending = _tokens.CreateAccessToken(user, roles, new System.Collections.Generic.Dictionary<string, string> { { "mfa_pending", "true" } });
        }
        else
        {
            token = _tokens.CreateAccessToken(user, roles);
        }

        // Redirect to frontend callback with token or pendingToken
        var cb = HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration)) as Microsoft.Extensions.Configuration.IConfiguration;
        var url = cb?.GetSection("OAuth")["Google:CallbackUrl"] ?? "http://localhost:5173/auth/callback";
        var uri = new UriBuilder(url);
        var qs = System.Web.HttpUtility.ParseQueryString(string.Empty);
        if (pending != null)
        {
            qs["pendingToken"] = pending;
            qs["mfaRequired"] = "true";
        }
        else if (token != null)
        {
            qs["token"] = token;
        }
        uri.Query = qs.ToString();
        return Redirect(uri.ToString());
    }

    [Authorize]
    [HttpGet("has-permission")]
    public async Task<IActionResult> HasPermissionFor([FromQuery] string resource, [FromQuery] string operation)
    {
        if (string.IsNullOrWhiteSpace(resource) || string.IsNullOrWhiteSpace(operation)) return BadRequest("Missing resource/operation");
        if (IsAdmin()) return Ok(new { allowed = true, reason = "admin" });
        var uid = GetUserIdFromClaims();
        if (uid == null) return Forbid();
        var allowed = await HasPermission(uid.Value, resource, operation);
        return Ok(new { allowed });
    }

    [Authorize]
    [HttpGet("permissions")]
    public async Task<IActionResult> GetMyPermissions()
    {
        var uid = GetUserIdFromClaims();
        if (uid == null) return Forbid();

        // Si es admin, devolver todas las combinaciones (admin tiene acceso total)
        if (IsAdmin())
        {
            var all = await (from res in _db.Resources
                             from op in _db.Operations
                             select new { resource = res.Name, operation = op.Name }).ToListAsync();
            return Ok(new { admin = true, permissions = all });
        }

        var perms = await (from ur in _db.UserRoles
                           where ur.UserId == uid.Value
                           join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
                           join res in _db.Resources on rp.ResourceId equals res.Id
                           join op in _db.Operations on rp.OperationId equals op.Id
                           select new { resource = res.Name, operation = op.Name }).Distinct().ToListAsync();
        return Ok(new { admin = false, permissions = perms });
    }

    [Authorize]
    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest req)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound("User not found");

        bool isAdmin = IsAdmin();
        var uid = GetUserIdFromClaims();
        if (!isAdmin)
        {
            if (uid == null) return Forbid();
            // Permitir modificar tu propio usuario, o requerir permiso users:update si modificas otro
            if (uid.Value != id)
            {
                if (!await HasPermission(uid.Value, "users", "update")) return Forbid();
            }
        }

        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var exists = await _db.Users.AnyAsync(u => u.Email == req.Email && u.Id != id);
            if (exists) return BadRequest("Email already in use");
            user.Email = req.Email;
        }
        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        }
        await _db.SaveChangesAsync();
        return Ok(new { message = "user_updated", userId = user.Id });
    }

    [Authorize]
    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound("User not found");

        bool isAdmin = IsAdmin();
        var uid = GetUserIdFromClaims();
        if (!isAdmin)
        {
            if (uid == null) return Forbid();
            if (!await HasPermission(uid.Value, "users", "delete")) return Forbid();
        }

        var userRoles = await _db.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
        if (userRoles.Count > 0) _db.UserRoles.RemoveRange(userRoles);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return Ok(new { message = "user_deleted", userId = id });
    }
}