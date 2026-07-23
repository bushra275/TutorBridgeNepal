namespace TutorBridgeNepal.ViewModels;

public class TutorDashboardViewModel
{
	public string FullName { get; set; } = string.Empty;
	public string Initials { get; set; } = string.Empty;
	public string Subjects { get; set; } = string.Empty;
	public string? District { get; set; }
	public decimal AverageRating { get; set; }
	public int ReviewCount { get; set; }
	public bool IsVerified { get; set; }
	public bool IsAvailableNow { get; set; }

	public int TotalSessions { get; set; }
	public int TotalSessionsThisMonth { get; set; }
	public int CompletedSessions { get; set; }
	public double HoursTaught { get; set; }
	public int ActiveStudentsCount { get; set; }
	public int NewStudentsThisMonth { get; set; }
	public int TodaySessionsCount { get; set; }
	public int PendingRequestsCount { get; set; }

	public List<TutorBookingRowViewModel> TodaySchedule { get; set; } = new();
	public List<TutorBookingRowViewModel> PendingRequests { get; set; } = new();
	public List<TutorBookingRowViewModel> RecentSessions { get; set; } = new();
	public List<TutorStudentRowViewModel> MyStudents { get; set; } = new();
	public List<TutorReviewRowViewModel> RecentReviews { get; set; } = new();
}

public class TutorBookingRowViewModel
{
	public int BookingId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public string StudentInitials { get; set; } = string.Empty;
	public string? StudentGradeLevel { get; set; }
	public string Subject { get; set; } = string.Empty;
	public DateTime StartTime { get; set; }
	public DateTime EndTime { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTime RequestedAt { get; set; }
}

public class TutorStudentRowViewModel
{
	public int StudentProfileId { get; set; }
	public string FullName { get; set; } = string.Empty;
	public string Initials { get; set; } = string.Empty;
	public string? GradeLevel { get; set; }
	public int SessionsCount { get; set; }
	public DateTime? NextSessionAt { get; set; }
}

public class TutorReviewRowViewModel
{
	public string StudentName { get; set; } = string.Empty;
	public string StudentInitials { get; set; } = string.Empty;
	public int Rating { get; set; }
	public string? Comment { get; set; }
	public DateTime CreatedAt { get; set; }
}