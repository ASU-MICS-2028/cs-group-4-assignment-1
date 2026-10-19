using StudentPortal.Api.Data;

namespace StudentPortal.Api.Authorization;

public static class CourseAssignmentRules
{
    // A lecturer's rights come from a current course assignment, not from the lecturer role alone.
    public static IQueryable<CourseAssignment> ActiveFor(this IQueryable<CourseAssignment> assignments, string lecturerId, DateOnly today) =>
        assignments.Where(a => a.LecturerId == lecturerId
                               && a.StartsOn <= today
                               && (a.EndsOn == null || a.EndsOn >= today));

    public static DateOnly Today(this TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
