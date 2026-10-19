using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using StudentPortal.Api.Data;
using StudentPortal.Api.Security;

namespace StudentPortal.Api.Authorization;

// Each handler only ever calls Succeed(). If no rule matches, the request is denied.

// Student: own record only. Lecturer: only students enrolled in a course they currently teach.
public sealed class ReadStudentRecordHandler(PortalDbContext db, TimeProvider clock)
    : AuthorizationHandler<ReadStudentRecordRequirement, StudentRecordResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ReadStudentRecordRequirement requirement, StudentRecordResource resource)
    {
        var accountId = context.User.AccountId();
        if (accountId is null)
            return;

        if (context.User.IsInRole(Roles.Student) && accountId == resource.StudentId)
        {
            context.Succeed(requirement);
            return;
        }

        if (context.User.IsInRole(Roles.Lecturer))
        {
            var taughtCourses = db.CourseAssignments.ActiveFor(accountId, clock.Today()).Select(a => a.CourseId);
            var teachesStudent = await db.CourseEnrollments
                .AnyAsync(e => e.StudentId == resource.StudentId && taughtCourses.Contains(e.CourseId));

            if (teachesStudent)
                context.Succeed(requirement);
        }
    }
}

// Lecturer must hold a current assignment for the course.
public sealed class TeachCourseHandler(PortalDbContext db, TimeProvider clock)
    : AuthorizationHandler<TeachCourseRequirement, CourseResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, TeachCourseRequirement requirement, CourseResource resource)
    {
        var accountId = context.User.AccountId();
        if (accountId is null || !context.User.IsInRole(Roles.Lecturer))
            return;

        if (await db.CourseAssignments.ActiveFor(accountId, clock.Today()).AnyAsync(a => a.CourseId == resource.CourseId))
            context.Succeed(requirement);
    }
}

// Lecturer must currently teach the course, and the student must be enrolled in it.
public sealed class UpdateGradeHandler(PortalDbContext db, TimeProvider clock)
    : AuthorizationHandler<UpdateGradeRequirement, CourseGradeResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, UpdateGradeRequirement requirement, CourseGradeResource resource)
    {
        var accountId = context.User.AccountId();
        if (accountId is null || !context.User.IsInRole(Roles.Lecturer))
            return;

        var assigned = await db.CourseAssignments
            .ActiveFor(accountId, clock.Today())
            .AnyAsync(a => a.CourseId == resource.CourseId);

        var enrolled = await db.CourseEnrollments
            .AnyAsync(e => e.CourseId == resource.CourseId && e.StudentId == resource.StudentId);

        if (assigned && enrolled)
            context.Succeed(requirement);
    }
}
