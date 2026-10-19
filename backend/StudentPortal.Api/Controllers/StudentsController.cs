using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentPortal.Api.Authorization;
using StudentPortal.Api.Data;
using StudentPortal.Api.Grading;
using StudentPortal.Api.Security;

namespace StudentPortal.Api.Controllers;

public sealed record CourseResultDto(string CourseId, string CourseTitle, string Semester, int? Score, string? Grade, DateTimeOffset? UpdatedAt);
public sealed record StudentRecordDto(string StudentId, string Name, string Email, string Programme, int YearGroup, IReadOnlyList<CourseResultDto> Courses);

[ApiController]
[Route("api/students")]
public sealed class StudentsController(
    PortalDbContext db,
    IAuthorizationService authorization,
    AuditLog audit,
    TimeProvider clock) : ControllerBase
{
    // GET /api/students/{studentId}
    // Student: own record only. Lecturer: students in a course they currently teach.
    [HttpGet("{studentId}")]
    public async Task<IActionResult> Get(string studentId)
    {
        studentId = studentId.Trim().ToUpperInvariant();

        var decision = await authorization.AuthorizeAsync(User, new StudentRecordResource(studentId), Policies.ReadStudentRecord);
        if (!decision.Succeeded)
            return Forbid();

        var student = await db.Accounts
            .AsNoTracking()
            .Include(a => a.StudentProfile)
            .SingleOrDefaultAsync(a => a.Id == studentId && a.Role == Roles.Student);
        if (student?.StudentProfile is null)
            return NotFound();

        var enrollments = db.CourseEnrollments.AsNoTracking().Where(e => e.StudentId == studentId);

        // Least privilege: a lecturer sees only the results for courses they teach.
        if (User.IsInRole(Roles.Lecturer))
        {
            var taughtCourses = db.CourseAssignments.ActiveFor(User.AccountId()!, clock.Today()).Select(a => a.CourseId);
            enrollments = enrollments.Where(e => taughtCourses.Contains(e.CourseId));
        }

        var courses = await enrollments
            .OrderBy(e => e.CourseId)
            .Select(e => new { e.CourseId, e.Course!.Title, e.Course.Semester, e.Score, e.GradeUpdatedAt })
            .ToListAsync();

        await audit.RecordAsync("record.read", AuditOutcome.Success, User.AccountId(), studentId);

        return Ok(new StudentRecordDto(
            student.Id,
            student.DisplayName,
            student.Email,
            student.StudentProfile.Programme,
            student.StudentProfile.YearGroup,
            courses.Select(c => new CourseResultDto(c.CourseId, c.Title, c.Semester, c.Score, GradeScale.Letter(c.Score), c.GradeUpdatedAt)).ToList()));
    }
}
