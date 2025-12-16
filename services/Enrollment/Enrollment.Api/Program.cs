using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.SwaggerGen;
using Enrollment.Infrastructure;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Messaging;
using BuildingBlocks.Web;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS para cliente local
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
        policy.WithOrigins(
                "http://localhost:8080",
                "http://localhost:8081"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
    );
});

builder.Services.AddEnrollmentInfrastructure(builder.Configuration);
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
    var db = scope.ServiceProvider.GetRequiredService<EnrollmentDbContext>();
    try { db.Database.Migrate(); } catch { }
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();

// Habilitar CORS
app.UseCors("AllowClient");

app.MapControllers();

app.Run();
