namespace TutorBridgeNepal.ViewModels;

public class StudentFindTutorsViewModel
{
    public string? Query { get; set; }
    public string? Subject { get; set; }
    public string? District { get; set; }
    public int? MinExperience { get; set; }
    public string Sort { get; set; } = "rating";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 6;

    public int TotalCount { get; set; }
    public List<TutorSummaryViewModel> Tutors { get; set; } = new();
    public List<string> SubjectOptions { get; set; } = new();
    public List<string> DistrictOptions { get; set; } = new();

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}