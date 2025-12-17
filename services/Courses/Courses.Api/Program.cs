using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.EntityFrameworkCore;
using Courses.Infrastructure;
using BuildingBlocks.Messaging;
using BuildingBlocks.Web;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddCoursesInfrastructure(builder.Configuration);
builder.Services.AddEventBus(builder.Configuration);
builder.Services.Configure<AuditOptions>(builder.Configuration.GetSection("Audit"));

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

// Aplicar migraciones al arrancar
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoursesDbContext>();
    try { db.Database.Migrate(); } catch { }
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();

// Habilitar CORS
app.UseCors("AllowClient");

app.MapControllers();

app.Run();
