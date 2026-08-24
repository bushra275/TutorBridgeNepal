namespace TutorBridgeNepal.ViewModels;

public class HelpSupportPageViewModel
{
    public List<SupportTicketRowViewModel> MyTickets { get; set; } = new();
    public List<BookingOptionViewModel> RecentBookings { get; set; } = new();
}

public class BookingOptionViewModel
{
    public int BookingId { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class SupportTicketRowViewModel
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}