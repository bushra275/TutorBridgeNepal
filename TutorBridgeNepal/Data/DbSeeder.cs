using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TutorBridgeNepal.Models;

namespace TutorBridgeNepal.Data;

public static class DbSeeder
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = ["Student", "Tutor", "Admin"];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = "admin@tutorbridge.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Platform Administrator",
                EmailConfirmed = true
            };

            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }

    public static async Task SeedSampleTutorsAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        var sampleTutors = new[]
        {
            new { Email = "ram.shrestha@tutorbridge.com", FullName = "Ram Prasad Shrestha", District = "Kathmandu", Subjects = "Mathematics, SEE Prep", Years = 6, Rating = 4.9m },
            new { Email = "sita.bajracharya@tutorbridge.com", FullName = "Sita Bajracharya", District = "Lalitpur", Subjects = "Science, Biology, SEE Prep", Years = 5, Rating = 4.8m },
            new { Email = "arjun.karmacharya@tutorbridge.com", FullName = "Arjun Karmacharya", District = "Lalitpur", Subjects = "English, Communication, IELTS Prep", Years = 4, Rating = 4.7m },
            new { Email = "bimala.gurung@tutorbridge.com", FullName = "Bimala Gurung", District = "Pokhara", Subjects = "Physics, Chemistry, NEB Prep", Years = 7, Rating = 4.9m },
            new { Email = "prakash.adhikari@tutorbridge.com", FullName = "Prakash Adhikari", District = "Kathmandu", Subjects = "Computer Science, Programming", Years = 5, Rating = 4.8m },
            new { Email = "kabita.rai@tutorbridge.com", FullName = "Kabita Rai", District = "Kathmandu", Subjects = "Biology, SEE Prep, NEB Prep", Years = 6, Rating = 4.8m },
        };

        foreach (var sample in sampleTutors)
        {
            var user = await userManager.FindByEmailAsync(sample.Email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = sample.Email,
                    Email = sample.Email,
                    FullName = sample.FullName,
                    District = sample.District,
                    EmailConfirmed = true
                };

                await userManager.CreateAsync(user, "Tutor@123");
                await userManager.AddToRoleAsync(user, "Tutor");
            }

            var alreadyHasProfile = await context.TutorProfiles.AnyAsync(t => t.UserId == user.Id);
            if (!alreadyHasProfile)
            {
                context.TutorProfiles.Add(new TutorProfile
                {
                    UserId = user.Id,
                    Subjects = sample.Subjects,
                    YearsOfExperience = sample.Years,
                    AverageRating = sample.Rating,
                    IsVerified = true
                });
            }
        }

        await context.SaveChangesAsync();

        // Seed availability slots for these sample tutors so students can actually
        // browse and book real sessions instead of hitting an empty calendar.
        var sampleEmails = sampleTutors.Select(s => s.Email).ToList();
        var tutorProfiles = await context.TutorProfiles
            .Include(t => t.User)
            .Where(t => sampleEmails.Contains(t.User.Email!))
            .ToListAsync();

        var slotTimes = new[] { 9, 11, 16, 18 }; // 9am, 11am, 4pm, 6pm

        foreach (var tutor in tutorProfiles)
        {
            var alreadyHasSlots = await context.TutorAvailabilitySlots.AnyAsync(s => s.TutorProfileId == tutor.Id);
            if (alreadyHasSlots)
            {
                continue;
            }

            for (var dayOffset = 1; dayOffset <= 10; dayOffset++)
            {
                var date = DateTime.Today.AddDays(dayOffset);

                foreach (var hour in slotTimes)
                {
                    context.TutorAvailabilitySlots.Add(new TutorAvailabilitySlot
                    {
                        TutorProfileId = tutor.Id,
                        StartTime = date.AddHours(hour),
                        EndTime = date.AddHours(hour).AddHours(1),
                        IsBooked = false
                    });
                }
            }
        }

        await context.SaveChangesAsync();
    }
}