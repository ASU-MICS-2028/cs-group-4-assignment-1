namespace StudentPortal.Api.Grading;

// Ashesi graduate grading scale, as published in the ICS570 syllabus.
public static class GradeScale
{
    public static string? Letter(int? score) => score switch
    {
        null => null,
        >= 80 => "A",
        >= 75 => "B+",
        >= 70 => "B",
        >= 65 => "C+",
        >= 60 => "C",
        >= 55 => "D+",
        >= 50 => "D",
        _ => "E",
    };
}
