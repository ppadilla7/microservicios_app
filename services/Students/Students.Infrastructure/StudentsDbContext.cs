using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Students.Domain.Entities;

namespace Students.Infrastructure;

public class StudentsDbContext : DbContext
{
    public StudentsDbContext(DbContextOptions<StudentsDbContext> options) : base(options) { }

    public DbSet<Student> Students => Set<Student>();
}

public static class StudentsInfrastructureExtensions
{
    public static IServiceCollection AddStudentsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["DB_PROVIDER"] ?? config["Database:Provider"] ?? "Postgres";
        if (string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            var pg = config.GetConnectionString("Postgres") ?? "Host=localhost;Database=studentsdb;Username=postgres;Password=postgres";
            services.AddDbContext<StudentsDbContext>(opt => opt.UseNpgsql(pg));
        }
        else
        {
            var conn = config.GetConnectionString("Default") ?? "Server=localhost;Database=studentsdb;Trusted_Connection=True;TrustServerCertificate=True";
            services.AddDbContext<StudentsDbContext>(opt => opt.UseSqlServer(conn));
        }
        return services;
    }
}