using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Enrollment.Domain.Entities;

namespace Enrollment.Infrastructure;

public class EnrollmentDbContext : DbContext
{
    public EnrollmentDbContext(DbContextOptions<EnrollmentDbContext> options) : base(options) { }
    public DbSet<Enrollment.Domain.Entities.Enrollment> Enrollments => Set<Enrollment.Domain.Entities.Enrollment>();
}

public static class EnrollmentInfrastructureExtensions
{
    public static IServiceCollection AddEnrollmentInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["DB_PROVIDER"] ?? config["Database:Provider"] ?? "Postgres";
        if (string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            var pg = config.GetConnectionString("Postgres") ?? "Host=localhost;Database=enrollmentdb;Username=postgres;Password=postgres";
            services.AddDbContext<EnrollmentDbContext>(opt => opt.UseNpgsql(pg));
        }
        else
        {
            var conn = config.GetConnectionString("Default") ?? "Server=localhost;Database=enrollmentdb;Trusted_Connection=True;TrustServerCertificate=True";
            services.AddDbContext<EnrollmentDbContext>(opt => opt.UseSqlServer(conn));
        }
        return services;
    }
}