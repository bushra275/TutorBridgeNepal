namespace TutorBridgeNepal.ViewModels;

public class TutorReviewsPageViewModel
{
    public string Tab { get; set; } = "all";
    public string Sort { get; set; } = "newest";

    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public List<StarBucketViewModel> StarDistribution { get; set; } = new();

    public int FiveStarCount { get; set; }
    public int FourStarCount { get; set; }
    public int ThreeAndBelowCount { get; set; }

    public List<string> CommonPhrases { get; set; } = new();

    public decimal PlatformAverageRating { get; set; }
    public int? ResponseRatePercent { get; set; }
    public int? RepeatBookingRatePercent { get; set; }
    public bool IsTopFivePercent { get; set; }

    public List<TutorReviewRowViewModel2> Reviews { get; set; } = new();
}

public class StarBucketViewModel
{
    public int Stars { get; set; }
    public int Count { get; set; }
    public int Percent { get; set; }
}

// Named distinctly from the existing dashboard TutorReviewRowViewModel since
// this page needs extra fields (reply, subject, session date, review id).
public class TutorReviewRowViewModel2
{
    public int ReviewId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentInitials { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? TutorReply { get; set; }
    public DateTime? TutorRepliedAt { get; set; }
}