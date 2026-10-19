namespace StudentPortal.Api.Data;

public static class Roles
{
    public const string Student = "student";
    public const string Lecturer = "lecturer";
}

public class Account
{
    public required string Id { get; set; }
    public required string DisplayName { get; set; }
    public required string Role { get; set; }
    public required string Email { get; set; }

    // WebAuthn user.id: random bytes, so the authenticator never stores the student ID or name.
    public required byte[] UserHandle { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public StudentProfile? StudentProfile { get; set; }
}

public class StudentProfile
{
    public required string AccountId { get; set; }
    public required string Programme { get; set; }
    public int YearGroup { get; set; }
}

public class Course
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Semester { get; set; }
}

public class CourseAssignment
{
    public required string LecturerId { get; set; }
    public required string CourseId { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }

    public Course? Course { get; set; }
}

public class CourseEnrollment
{
    public required string StudentId { get; set; }
    public required string CourseId { get; set; }
    public int? Score { get; set; }
    public DateTimeOffset? GradeUpdatedAt { get; set; }
    public string? GradeUpdatedBy { get; set; }

    public Account? Student { get; set; }
    public Course? Course { get; set; }
}

public class PasskeyCredential
{
    public Guid Id { get; set; }
    public required string AccountId { get; set; }
    public required byte[] CredentialId { get; set; }
    public required byte[] PublicKey { get; set; }
    public long SignCount { get; set; }
    public Guid AaGuid { get; set; }
    public string[] Transports { get; set; } = [];
    public required string AttestationFormat { get; set; }
    public bool IsBackupEligible { get; set; }
    public bool IsBackedUp { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public Account? Account { get; set; }
}

public class EnrollmentCode
{
    public Guid Id { get; set; }
    public required string AccountId { get; set; }
    public required byte[] CodeHash { get; set; }
    public required byte[] Salt { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public int FailedAttempts { get; set; }
}

public static class CeremonyKinds
{
    public const string Registration = "registration";
    public const string Authentication = "authentication";
}

public class WebAuthnCeremony
{
    public Guid Id { get; set; }
    public required byte[] TokenHash { get; set; }
    public required string Kind { get; set; }
    public string? AccountId { get; set; }
    public Guid? EnrollmentCodeId { get; set; }
    public required string OptionsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public class Session
{
    public Guid Id { get; set; }
    public required byte[] TokenHash { get; set; }
    public required string AccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public Account? Account { get; set; }
}

public class AuditEvent
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? ActorId { get; set; }
    public required string Event { get; set; }
    public required string Outcome { get; set; }
    public string? Target { get; set; }
    public string? Detail { get; set; }
    public string? IpAddress { get; set; }
}
