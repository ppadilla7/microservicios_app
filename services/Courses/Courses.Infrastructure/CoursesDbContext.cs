using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Courses.Domain.Entities;

namespace Courses.Infrastructure;

public class CoursesDbContext : DbContext
{
    public CoursesDbContext(DbContextOptions<CoursesDbContext> options) : base(options) { }
    public DbSet<Course> Courses => Set<Course>();
}

public static class CoursesInfrastructureExtensions
{
    public static IServiceCollection AddCoursesInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["DB_PROVIDER"] ?? config["Database:Provider"] ?? "Postgres";
        if (string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            var pg = config.GetConnectionString("Postgres") ?? "Host=localhost;Database=coursesdb;Username=postgres;Password=postgres";
            services.AddDbContext<CoursesDbContext>(opt => opt.UseNpgsql(pg));
        }
        else
        {
            var conn = config.GetConnectionString("Default") ?? "Server=localhost;Database=coursesdb;Trusted_Connection=True;TrustServerCertificate=True";
            services.AddDbContext<CoursesDbContext>(opt => opt.UseSqlServer(conn));
        }
        return services;
    }
}