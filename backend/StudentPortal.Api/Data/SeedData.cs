using Microsoft.EntityFrameworkCore;

namespace StudentPortal.Api.Data;

// Synthetic records only. Names, emails and grades are fictional.
public static class SeedData
{
    private static readonly DateTimeOffset Created = new(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly SemesterStart = new(2026, 8, 24);

    public static void Apply(ModelBuilder model)
    {
        model.Entity<Account>().HasData(
            new Account { Id = "S001", DisplayName = "Ama Owusu", Role = Roles.Student, Email = "s001@students.portal.test", UserHandle = Convert.FromHexString("6F1C2A9B4E7D30C85A12F09E3B6D4C71"), CreatedAt = Created },
            new Account { Id = "S002", DisplayName = "Kwame Asante", Role = Roles.Student, Email = "s002@students.portal.test", UserHandle = Convert.FromHexString("A43E9D0172B85FC6E1D4093A7C2B5E88"), CreatedAt = Created },
            new Account { Id = "S003", DisplayName = "Esi Boateng", Role = Roles.Student, Email = "s003@students.portal.test", UserHandle = Convert.FromHexString("1D7B3F60C9E2A4851B0F6E2D93C7A415"), CreatedAt = Created },
            new Account { Id = "L001", DisplayName = "Dr. Kofi Mensah", Role = Roles.Lecturer, Email = "l001@staff.portal.test", UserHandle = Convert.FromHexString("C02E5A8D71F49B36E8A10D4C5F2B9763"), CreatedAt = Created },
            new Account { Id = "L002", DisplayName = "Dr. Abena Ofori", Role = Roles.Lecturer, Email = "l002@staff.portal.test", UserHandle = Convert.FromHexString("58D9E14B2A6C07F3B9E5D82A1C4F60B7"), CreatedAt = Created });

        model.Entity<StudentProfile>().HasData(
            new StudentProfile { AccountId = "S001", Programme = "BSc Computer Science", YearGroup = 2028 },
            new StudentProfile { AccountId = "S002", Programme = "BSc Management Information Systems", YearGroup = 2028 },
            new StudentProfile { AccountId = "S003", Programme = "BSc Computer Engineering", YearGroup = 2027 });

        model.Entity<Course>().HasData(
            new Course { Id = "C101", Title = "Introduction to Information Security", Semester = "2026/27 Semester 1" },
            new Course { Id = "C102", Title = "Data Structures and Algorithms", Semester = "2026/27 Semester 1" });

        model.Entity<CourseAssignment>().HasData(
            new CourseAssignment { LecturerId = "L001", CourseId = "C101", StartsOn = SemesterStart },
            new CourseAssignment { LecturerId = "L002", CourseId = "C102", StartsOn = SemesterStart });

        model.Entity<CourseEnrollment>().HasData(
            new CourseEnrollment { StudentId = "S001", CourseId = "C101", Score = 78, GradeUpdatedAt = Created, GradeUpdatedBy = "L001" },
            new CourseEnrollment { StudentId = "S002", CourseId = "C101", Score = 64, GradeUpdatedAt = Created, GradeUpdatedBy = "L001" },
            new CourseEnrollment { StudentId = "S001", CourseId = "C102", Score = 71, GradeUpdatedAt = Created, GradeUpdatedBy = "L002" },
            new CourseEnrollment { StudentId = "S003", CourseId = "C102", Score = 85, GradeUpdatedAt = Created, GradeUpdatedBy = "L002" });
    }
}
