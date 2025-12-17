using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AspNet.Security.OAuth.GitHub;
using Security.Infrastructure.Data;
using Security.Application.Services;
using Security.Api.Authorization;
using Security.Domain.Models;
using BuildingBlocks.Messaging;
using BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext (SQL Server o Postgres)
var dbProvider = builder.Configuration["DB_PROVIDER"] ?? builder.Configuration["Database:Provider"] ?? "Postgres";
if (string.Equals(dbProvider, "Postgres", StringComparison.OrdinalIgnoreCase))
{
    var pgConn = builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Database=securitydb;Username=postgres;Password=postgres";
    builder.Services.AddDbContext<SecurityDbContext>(opts => opts.UseNpgsql(pgConn));
}
else
{
    var connString = builder.Configuration.GetConnectionString("Default") ?? "Server=localhost;Database=securitydb;Trusted_Connection=True;TrustServerCertificate=True";
    builder.Services.AddDbContext<SecurityDbContext>(opts => opts.UseSqlServer(connString));
}

// Token service
builder.Services.AddScoped<TokenService>();

// Auth: JWT + Cookie + Google/GitHub
var jwtSection = builder.Configuration.GetSection("Jwt");
var issuer = jwtSection["Issuer"]!;
var audience = jwtSection["Audience"]!;
var secret = jwtSection["Secret"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
        };
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["OAuth:Google:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["OAuth:Google:ClientSecret"] ?? "";
        options.CallbackPath = "/signin-google";
    })
    .AddGitHub(options =>
    {
        options.ClientId = builder.Configuration["OAuth:GitHub:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["OAuth:GitHub:ClientSecret"] ?? "";
        options.CallbackPath = "/signin-github";
        options.Scope.Add("user:email");
    });

builder.Services.AddAuthorization();
// CORS: permitir orígenes por defecto y adicionales vía variable CORS_ORIGINS
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
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedCorsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// Event bus y auditoría (registrar ANTES de construir la app)
builder.Services.AddEventBus(builder.Configuration);
builder.Services.Configure<AuditOptions>(builder.Configuration.GetSection("Audit"));

var app = builder.Build();

// Bootstrap RBAC (one-time seed) if database has no resources
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch { }

    if (!db.Resources.Any())
    {
        var resNames = new[] { "roles", "resources", "operations", "user_roles", "permissions", "users" };
        foreach (var rn in resNames)
            db.Resources.Add(new Resource { Name = rn });

        var opNames = new[] { "create", "read", "update", "delete" };
        foreach (var on in opNames)
            db.Operations.Add(new Operation { Name = on });

        var adminRole = db.Roles.FirstOrDefault(r => r.Name == "admin") ?? new Role { Name = "admin", Description = "Administrador" };
        if (adminRole.Id == Guid.Empty || !db.Roles.Any(r => r.Id == adminRole.Id))
            db.Roles.Add(adminRole);

        db.SaveChanges();

        var resources = db.Resources.ToList();
        var operations = db.Operations.ToList();
        foreach (var res in resources)
        {
            foreach (var op in operations)
            {
                if (!db.RolePermissions.Any(rp => rp.RoleId == adminRole.Id && rp.ResourceId == res.Id && rp.OperationId == op.Id))
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, ResourceId = res.Id, OperationId = op.Id });
                }
            }
        }

        // Asegurar permiso mínimo para supervisor: users:read
        var supervisorRole = db.Roles.FirstOrDefault(r => r.Name == "supervisor") ?? new Role { Name = "supervisor", Description = "Supervisor" };
        if (supervisorRole.Id == Guid.Empty || !db.Roles.Any(r => r.Id == supervisorRole.Id))
            db.Roles.Add(supervisorRole);
        db.SaveChanges();
        var usersRes = db.Resources.FirstOrDefault(r => r.Name.ToLower() == "users");
        var readOp = db.Operations.FirstOrDefault(o => o.Name.ToLower() == "read");
        if (usersRes != null && readOp != null)
        {
            if (!db.RolePermissions.Any(rp => rp.RoleId == supervisorRole.Id && rp.ResourceId == usersRes.Id && rp.OperationId == readOp.Id))
            {
                db.RolePermissions.Add(new RolePermission { RoleId = supervisorRole.Id, ResourceId = usersRes.Id, OperationId = readOp.Id });
                db.SaveChanges();
            }
        }

        var firstUser = db.Users.OrderBy(u => u.CreatedAt).FirstOrDefault();
        if (firstUser != null && !db.UserRoles.Any(ur => ur.UserId == firstUser.Id && ur.RoleId == adminRole.Id))
        {
            db.UserRoles.Add(new UserRole { UserId = firstUser.Id, RoleId = adminRole.Id });
        }

        db.SaveChanges();
    }

    // Asegurar siempre que exista el rol supervisor y tenga permiso users:read
    var supervisor = db.Roles.FirstOrDefault(r => r.Name == "supervisor");
    if (supervisor == null)
    {
        supervisor = new Role { Name = "supervisor", Description = "Supervisor" };
        db.Roles.Add(supervisor);
        db.SaveChanges();
    }
    var usersResource = db.Resources.FirstOrDefault(r => r.Name.ToLower() == "users");
    var readOperation = db.Operations.FirstOrDefault(o => o.Name.ToLower() == "read");
    if (usersResource != null && readOperation != null)
    {
        var hasPerm = db.RolePermissions.Any(rp => rp.RoleId == supervisor.Id && rp.ResourceId == usersResource.Id && rp.OperationId == readOperation.Id);
        if (!hasPerm)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = supervisor.Id, ResourceId = usersResource.Id, OperationId = readOperation.Id });
            db.SaveChanges();
        }
    }

    // Otorgar permisos adicionales de lectura al rol supervisor para varias URLs
    var supReadResources = new[] { "roles", "resources", "operations", "courses", "students" };
    foreach (var rn in supReadResources)
    {
        var res = db.Resources.FirstOrDefault(r => r.Name.ToLower() == rn.ToLower());
        if (res != null && readOperation != null)
        {
            var exists = db.RolePermissions.Any(rp => rp.RoleId == supervisor.Id && rp.ResourceId == res.Id && rp.OperationId == readOperation.Id);
            if (!exists)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = supervisor.Id, ResourceId = res.Id, OperationId = readOperation.Id });
            }
        }
    }
    db.SaveChanges();

    // Ensure resources for domain: courses and students
    var coursesRes = db.Resources.FirstOrDefault(r => r.Name == "courses");
    if (coursesRes == null)
    {
        coursesRes = new Resource { Name = "courses", Description = "Cursos" };
        db.Resources.Add(coursesRes);
        db.SaveChanges();
    }
    var studentsRes = db.Resources.FirstOrDefault(r => r.Name == "students");
    if (studentsRes == null)
    {
        studentsRes = new Resource { Name = "students", Description = "Estudiantes" };
        db.Resources.Add(studentsRes);
        db.SaveChanges();
    }
    // Ensure resource for domain: enrollments
    var enrollmentsRes = db.Resources.FirstOrDefault(r => r.Name == "enrollments");
    if (enrollmentsRes == null)
    {
        enrollmentsRes = new Resource { Name = "enrollments", Description = "Matrículas" };
        db.Resources.Add(enrollmentsRes);
        db.SaveChanges();
    }

    // Ensure roles: docente and estudiante
    var docenteRole = db.Roles.FirstOrDefault(r => r.Name == "docente");
    if (docenteRole == null)
    {
        docenteRole = new Role { Name = "docente", Description = "Docente" };
        db.Roles.Add(docenteRole);
        db.SaveChanges();
    }
    var estudianteRole = db.Roles.FirstOrDefault(r => r.Name == "estudiante");
    if (estudianteRole == null)
    {
        estudianteRole = new Role { Name = "estudiante", Description = "Estudiante" };
        db.Roles.Add(estudianteRole);
        db.SaveChanges();
    }

    // Grant permissions: estudiante => courses:read
    var opCreate = db.Operations.FirstOrDefault(o => o.Name.ToLower() == "create");
    var opUpdate = db.Operations.FirstOrDefault(o => o.Name.ToLower() == "update");
    var opDelete = db.Operations.FirstOrDefault(o => o.Name.ToLower() == "delete");
    if (readOperation != null && coursesRes != null)
    {
        var exists = db.RolePermissions.Any(rp => rp.RoleId == estudianteRole.Id && rp.ResourceId == coursesRes.Id && rp.OperationId == readOperation.Id);
        if (!exists)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = estudianteRole.Id, ResourceId = coursesRes.Id, OperationId = readOperation.Id });
            db.SaveChanges();
        }
    }
    // Grant permissions: estudiante => enrollments:create
    var opCreateEnroll = db.Operations.FirstOrDefault(o => o.Name.ToLower() == "create");
    if (opCreateEnroll != null && enrollmentsRes != null)
    {
        var hasEnrollCreate = db.RolePermissions.Any(rp => rp.RoleId == estudianteRole.Id && rp.ResourceId == enrollmentsRes.Id && rp.OperationId == opCreateEnroll.Id);
        if (!hasEnrollCreate)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = estudianteRole.Id, ResourceId = enrollmentsRes.Id, OperationId = opCreateEnroll.Id });
            db.SaveChanges();
        }
    }

    // Grant permissions: docente => CRUD on courses and students
    var opsAll = new[] { opCreate, readOperation, opUpdate, opDelete }.Where(o => o != null).ToList();
    foreach (var res in new[] { coursesRes, studentsRes })
    {
        if (res == null) continue;
        foreach (var op in opsAll)
        {
            var any = db.RolePermissions.Any(rp => rp.RoleId == docenteRole.Id && rp.ResourceId == res.Id && rp.OperationId == op!.Id);
            if (!any)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = docenteRole.Id, ResourceId = res.Id, OperationId = op!.Id });
            }
        }
    }
    db.SaveChanges();
}

// Solo forzar redirección HTTPS en producción, para evitar warning en Development
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
// Importante: CORS debe ir antes de auth/authorization para que los 401/403 incluyan headers CORS
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RbacMiddleware>();
app.UseMiddleware<AuditMiddleware>();

app.MapControllers();

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
