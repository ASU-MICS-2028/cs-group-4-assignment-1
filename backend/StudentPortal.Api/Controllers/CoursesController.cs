using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentPortal.Api.Authorization;
using StudentPortal.Api.Data;
using StudentPortal.Api.Grading;
using StudentPortal.Api.Security;

namespace StudentPortal.Api.Controllers;

public sealed record CourseSummaryDto(string CourseId, string Title, string Semester, int StudentCount);
public sealed record RosterEntryDto(string StudentId, string Name, string Programme, int? Score, string? Grade, DateTimeOffset? UpdatedAt, string? UpdatedBy);
public sealed record UpdateGradeRequest([Required, Range(0, 100)] int? Score);
public sealed record GradeDto(string CourseId, string StudentId, int? Score, string? Grade, DateTimeOffset? UpdatedAt, string? UpdatedBy);

[ApiController]
[Route("api/courses")]
[Authorize(Policy = Policies.LecturerOnly)]
public sealed class CoursesController(
    PortalDbContext db,
    IAuthorizationService authorization,
    AuditLog audit,
    TimeProvider clock) : ControllerBase
{
    // Courses the signed-in lecturer currently teaches.
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var courses = await db.CourseAssignments
            .AsNoTracking()
            .ActiveFor(User.AccountId()!, clock.Today())
            .OrderBy(a => a.CourseId)
            .Select(a => new CourseSummaryDto(
                a.CourseId,
                a.Course!.Title,
                a.Course.Semester,
                db.CourseEnrollments.Count(e => e.CourseId == a.CourseId)))
            .ToListAsync();

        return Ok(courses);
    }

    [HttpGet("{courseId}/roster")]
    public async Task<IActionResult> Roster(string courseId)
    {
        courseId = courseId.Trim().ToUpperInvariant();

        var decision = await authorization.AuthorizeAsync(User, new CourseResource(courseId), Policies.TeachCourse);
        if (!decision.Succeeded)
            return Forbid();

        var roster = await db.CourseEnrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .OrderBy(e => e.StudentId)
            .Select(e => new { e.StudentId, e.Student!.DisplayName, e.Student.StudentProfile!.Programme, e.Score, e.GradeUpdatedAt, e.GradeUpdatedBy })
            .ToListAsync();

        return Ok(roster.Select(r => new RosterEntryDto(r.StudentId, r.DisplayName, r.Programme, r.Score, GradeScale.Letter(r.Score), r.GradeUpdatedAt, r.GradeUpdatedBy)));
    }

    // POST /api/courses/{courseId}/grades/{studentId}
    // Role check (LecturerOnly, on the class) runs first; then the course-assignment check.
    [HttpPost("{courseId}/grades/{studentId}")]
    [Consumes("application/json")]
    public async Task<IActionResult> UpdateGrade(string courseId, string studentId, UpdateGradeRequest request)
    {
        courseId = courseId.Trim().ToUpperInvariant();
        studentId = studentId.Trim().ToUpperInvariant();
        var lecturerId = User.AccountId()!;

        var decision = await authorization.AuthorizeAsync(User, new CourseGradeResource(courseId, studentId), Policies.UpdateGrade);
        if (!decision.Succeeded)
            return Forbid();

        var enrollment = await db.CourseEnrollments.SingleAsync(e => e.CourseId == courseId && e.StudentId == studentId);
        var previous = enrollment.Score;

        enrollment.Score = request.Score;
        enrollment.GradeUpdatedAt = clock.GetUtcNow();
        enrollment.GradeUpdatedBy = lecturerId;
        await db.SaveChangesAsync();

        await audit.RecordAsync("grade.updated", AuditOutcome.Success, lecturerId, $"{courseId}/{studentId}",
            $"{previous?.ToString() ?? "none"} -> {request.Score}");

        return Ok(new GradeDto(courseId, studentId, enrollment.Score, GradeScale.Letter(enrollment.Score), enrollment.GradeUpdatedAt, enrollment.GradeUpdatedBy));
    }
}
