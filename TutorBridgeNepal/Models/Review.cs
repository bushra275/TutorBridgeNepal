namespace TutorBridgeNepal.Models;

public class Review
{
    public int Id { get; set; }

    // One review per booking - a student reviews a specific completed session
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = default!;

    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = default!;

    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;

    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}