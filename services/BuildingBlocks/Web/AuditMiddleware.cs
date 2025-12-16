using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Web;

public sealed class AuditOptions
{
    public string Topic { get; set; } = "audit.actions";
    public string[] ExcludePathStartsWith { get; set; } = new[] { "/swagger", "/health", "/favicon", "/metrics" };
}

public sealed class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditMiddleware> _logger;
    private readonly IEventBus _bus;
    private readonly AuditOptions _options;

    public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger, IEventBus bus, IOptions<AuditOptions> options)
    {
        _next = next;
        _logger = logger;
        _bus = bus;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.ToString();
        if (_options.ExcludePathStartsWith.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? context.User?.FindFirst("sub")?.Value
                         ?? "anonymous";
            var userName = context.User?.Identity?.Name
                           ?? context.User?.FindFirst("name")?.Value
                           ?? context.User?.FindFirst(ClaimTypes.Email)?.Value
                           ?? string.Empty;
            var service = Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";
            var method = context.Request.Method;
            var query = context.Request.QueryString.ToString();
            var statusCode = context.Response.StatusCode;
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var cid)
                ? cid.ToString()
                : context.TraceIdentifier;

            var auditPayload = new
            {
                timestamp = DateTime.UtcNow,
                service,
                userId,
                userName,
                method,
                path,
                query,
                statusCode,
                durationMs = sw.ElapsedMilliseconds,
                correlationId,
                clientIp
            };

            try
            {
                await _bus.WriteAuditAsync(_options.Topic, auditPayload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit Kafka write failed for {Path}", path);
            }
        }
    }
}