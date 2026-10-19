using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace StudentPortal.Api.Data;

public class PortalDbContext(DbContextOptions<PortalDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseAssignment> CourseAssignments => Set<CourseAssignment>();
    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();
    public DbSet<PasskeyCredential> PasskeyCredentials => Set<PasskeyCredential>();
    public DbSet<EnrollmentCode> EnrollmentCodes => Set<EnrollmentCode>();
    public DbSet<WebAuthnCeremony> WebAuthnCeremonies => Set<WebAuthnCeremony>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Account>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasMaxLength(10);
            e.Property(a => a.Role).HasMaxLength(20);
            e.ToTable(t => t.HasCheckConstraint("ck_accounts_role", "role IN ('student', 'lecturer')"));
            e.HasIndex(a => a.UserHandle).IsUnique();
            e.HasOne(a => a.StudentProfile).WithOne().HasForeignKey<StudentProfile>(p => p.AccountId);
        });

        model.Entity<StudentProfile>().HasKey(p => p.AccountId);

        model.Entity<Course>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasMaxLength(10);
        });

        model.Entity<CourseAssignment>(e =>
        {
            e.HasKey(a => new { a.LecturerId, a.CourseId });
            e.HasOne<Account>().WithMany().HasForeignKey(a => a.LecturerId);
            e.HasOne(a => a.Course).WithMany().HasForeignKey(a => a.CourseId);
        });

        model.Entity<CourseEnrollment>(e =>
        {
            e.HasKey(x => new { x.StudentId, x.CourseId });
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId);
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId);
            e.ToTable(t => t.HasCheckConstraint("ck_course_enrollments_score", "score IS NULL OR (score >= 0 AND score <= 100)"));
        });

        model.Entity<PasskeyCredential>(e =>
        {
            e.HasIndex(c => c.CredentialId).IsUnique();
            e.HasOne(c => c.Account).WithMany().HasForeignKey(c => c.AccountId);
        });

        model.Entity<EnrollmentCode>(e =>
        {
            e.HasOne<Account>().WithMany().HasForeignKey(c => c.AccountId);
            e.HasIndex(c => c.AccountId);
        });

        model.Entity<WebAuthnCeremony>(e =>
        {
            e.ToTable("webauthn_ceremonies");
            e.HasIndex(c => c.TokenHash).IsUnique();
        });

        model.Entity<Session>(e =>
        {
            e.HasIndex(s => s.TokenHash).IsUnique();
            e.HasOne(s => s.Account).WithMany().HasForeignKey(s => s.AccountId);
        });

        model.Entity<AuditEvent>().HasIndex(a => a.OccurredAt);

        SeedData.Apply(model);
        UseSnakeCaseNames(model);
    }

    private static void UseSnakeCaseNames(ModelBuilder model)
    {
        foreach (var entity in model.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.GetTableName()!));
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));
            foreach (var key in entity.GetKeys())
                key.SetName(ToSnakeCase(key.GetName()!));
            foreach (var foreignKey in entity.GetForeignKeys())
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName()!));
            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
        }
    }

    private static string ToSnakeCase(string name) =>
        Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
