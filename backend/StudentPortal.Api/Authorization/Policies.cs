using Microsoft.AspNetCore.Authorization;
using StudentPortal.Api.Data;
using StudentPortal.Api.Security;

namespace StudentPortal.Api.Authorization;

public static class Policies
{
    public const string LecturerOnly = nameof(LecturerOnly);
    public const string ReadStudentRecord = nameof(ReadStudentRecord);
    public const string TeachCourse = nameof(TeachCourse);
    public const string UpdateGrade = nameof(UpdateGrade);

    public static void Configure(AuthorizationOptions options)
    {
        // Fail-safe default: every endpoint needs a valid session unless it is explicitly marked [AllowAnonymous].
        options.FallbackPolicy = new AuthorizationPolicyBuilder(SessionAuthenticationHandler.SchemeName)
            .RequireAuthenticatedUser()
            .Build();

        // RBAC: role check on the endpoint.
        options.AddPolicy(LecturerOnly, p => p.RequireAuthenticatedUser().RequireRole(Roles.Lecturer));

        // Resource checks: evaluated per request against the record being touched.
        options.AddPolicy(ReadStudentRecord, p => p.RequireAuthenticatedUser().AddRequirements(new ReadStudentRecordRequirement()));
        options.AddPolicy(TeachCourse, p => p.RequireAuthenticatedUser().RequireRole(Roles.Lecturer).AddRequirements(new TeachCourseRequirement()));
        options.AddPolicy(UpdateGrade, p => p.RequireAuthenticatedUser().RequireRole(Roles.Lecturer).AddRequirements(new UpdateGradeRequirement()));
    }
}

public sealed record StudentRecordResource(string StudentId);
public sealed record CourseResource(string CourseId);
public sealed record CourseGradeResource(string CourseId, string StudentId);

public sealed class ReadStudentRecordRequirement : IAuthorizationRequirement;
public sealed class TeachCourseRequirement : IAuthorizationRequirement;
public sealed class UpdateGradeRequirement : IAuthorizationRequirement;
