using Microsoft.EntityFrameworkCore;
using Security.Domain.Models;

namespace Security.Infrastructure.Data;

public class SecurityDbContext : DbContext
{
    public SecurityDbContext(DbContextOptions<SecurityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Student> Students => Set<Student>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(r => r.Name).IsUnique();
        modelBuilder.Entity<Resource>().HasIndex(r => r.Name).IsUnique();
        modelBuilder.Entity<Operation>().HasIndex(o => o.Name).IsUnique();
        modelBuilder.Entity<Course>().HasIndex(c => c.Code).IsUnique();
        modelBuilder.Entity<Student>().HasIndex(s => s.StudentNumber).IsUnique();

        // Evitar que Security cree/actualice tablas pertenecientes a otros servicios
        modelBuilder.Entity<Course>().ToTable("Courses", tb => tb.ExcludeFromMigrations());
        modelBuilder.Entity<Student>().ToTable("Students", tb => tb.ExcludeFromMigrations());

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId);

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Resource)
            .WithMany(res => res.RolePermissions)
            .HasForeignKey(rp => rp.ResourceId);

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Operation)
            .WithMany(op => op.RolePermissions)
            .HasForeignKey(rp => rp.OperationId);
    }
}