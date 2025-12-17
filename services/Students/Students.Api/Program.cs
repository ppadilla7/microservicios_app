using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Students.Infrastructure;
using BuildingBlocks.Messaging;
using BuildingBlocks.Web;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS dinámico: orígenes por defecto + CORS_ORIGINS (env/config)
var defaultCorsOrigins = new[]
{
    "http://localhost:5173","http://localhost:5174","http://localhost:5175","http://localhost:5176",
    "http://127.0.0.1:5173","http://127.0.0.1:5174","http://127.0.0.1:5175","http://127.0.0.1:5176",
    "http://localhost:8080","http://127.0.0.1:8080","http://localhost:8081","http://127.0.0.1:8081"
};
var extraCorsOriginsRaw = builder.Configuration["CORS_ORIGINS"] ?? string.Empty;
var extraCorsOrigins = extraCorsOriginsRaw
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var allowedCorsOrigins = defaultCorsOrigins.Concat(extraCorsOrigins).Distinct().ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
        policy.WithOrigins(allowedCorsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
    );
});

// Register infrastructure and messaging
builder.Services.AddStudentsInfrastructure(builder.Configuration);
builder.Services.AddEventBus(builder.Configuration);
builder.Services.Configure<AuditOptions>(builder.Configuration.GetSection("Audit"));

// Configure authentication (JWT)
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;

        var jwt = builder.Configuration.GetSection("Jwt");
        var issuer = jwt["Issuer"];
        var audience = jwt["Audience"];
        var secret = jwt["Secret"];

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Evitar redirección HTTPS en contenedor (sólo habilitar en producción si está configurado)
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// Aplicar migraciones pendientes al arrancar con reintentos (DB puede tardar en estar lista)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
    var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("StudentsDbStartup");
    const int maxRetries = 5;
    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            db.Database.Migrate();
            logger?.LogInformation("Students DB migrated successfully on attempt {Attempt}", attempt);
            break;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Students DB migration failed on attempt {Attempt}. Retrying...", attempt);
            if (attempt == maxRetries)
            {
                logger?.LogError(ex, "Students DB migration failed after {MaxRetries} attempts", maxRetries);
            }
            else
            {
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
        }
    }
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();

// Habilitar CORS
app.UseCors("AllowClient");

app.MapControllers();

app.Run();
