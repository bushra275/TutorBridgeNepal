using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TutorBridgeNepal.Models;

namespace TutorBridgeNepal.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<TutorProfile> TutorProfiles => Set<TutorProfile>();
    public DbSet<TutorAvailabilitySlot> TutorAvailabilitySlots => Set<TutorAvailabilitySlot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SavedTutor> SavedTutors => Set<SavedTutor>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<StudentAchievement> StudentAchievements => Set<StudentAchievement>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TutorAvailabilitySlot>()
            .HasIndex(s => new { s.TutorProfileId, s.StartTime, s.EndTime });

        builder.Entity<Booking>()
            .HasIndex(b => b.TutorAvailabilitySlotId)
            .IsUnique();

        builder.Entity<Booking>()
            .HasOne(b => b.StudentProfile)
            .WithMany()
            .HasForeignKey(b => b.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Booking>()
            .HasOne(b => b.TutorProfile)
            .WithMany()
            .HasForeignKey(b => b.TutorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Booking>()
            .HasOne(b => b.TutorAvailabilitySlot)
            .WithMany()
            .HasForeignKey(b => b.TutorAvailabilitySlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>()
            .HasOne(m => m.StudentProfile)
            .WithMany()
            .HasForeignKey(m => m.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>()
            .HasOne(m => m.TutorProfile)
            .WithMany()
            .HasForeignKey(m => m.TutorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>()
            .HasIndex(m => new { m.StudentProfileId, m.TutorProfileId, m.SentAt });

        builder.Entity<SavedTutor>()
            .HasOne(s => s.StudentProfile)
            .WithMany()
            .HasForeignKey(s => s.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SavedTutor>()
            .HasOne(s => s.TutorProfile)
            .WithMany()
            .HasForeignKey(s => s.TutorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SavedTutor>()
             .HasIndex(s => new { s.StudentProfileId, s.TutorProfileId })
             .IsUnique();

        builder.Entity<Goal>()
            .HasOne(g => g.StudentProfile)
            .WithMany()
            .HasForeignKey(g => g.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Goal>()
            .HasIndex(g => g.StudentProfileId);

        builder.Entity<StudentAchievement>()
            .HasOne(a => a.StudentProfile)
            .WithMany()
            .HasForeignKey(a => a.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StudentAchievement>()
                    .HasIndex(a => new { a.StudentProfileId, a.AchievementKey })
                    .IsUnique();

        builder.Entity<SupportTicket>()
            .HasOne(t => t.StudentProfile)
            .WithMany()
            .HasForeignKey(t => t.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SupportTicket>()
                    .HasIndex(t => t.StudentProfileId);

        // Restrict (not Cascade) on all three FKs, consistent with Booking/Message/
        // SavedTutor - avoids multiple-cascade-path conflicts and requires the
        // explicit manual cleanup already used elsewhere (see DeleteAccount).
        builder.Entity<Review>()
            .HasOne(r => r.Booking)
            .WithMany()
            .HasForeignKey(r => r.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Review>()
            .HasOne(r => r.StudentProfile)
            .WithMany()
            .HasForeignKey(r => r.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Review>()
            .HasOne(r => r.TutorProfile)
            .WithMany()
            .HasForeignKey(r => r.TutorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Review>()
            .HasIndex(r => r.BookingId)
            .IsUnique();
    }
}