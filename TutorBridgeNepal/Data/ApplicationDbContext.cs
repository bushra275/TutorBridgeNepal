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
    }
}