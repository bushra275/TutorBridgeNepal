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

        var adminEmail = "info.bushrashefi@gmail.com";
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
                    AverageRating = 0m,
                    ReviewCount = 0,
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

    public static async Task SeedTutorVerificationApplicationsAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        var applications = new[]
        {
            new
            {
                Email = "nirajan.pradhan@tutorbridge.com",
                FullName = "Nirajan Pradhan",
                Phone = "9841234512",
                District = "Kaski",
                Subjects = "Chemistry, Physics",
                Education = "B.Sc Chemistry, Tribhuvan University (2021)",
                Experience = "3 years private tutoring · Grade 9-12, NEB Board",
                Years = 3,
                DaysAgo = 2,
                Status = "Pending",
                Documents = new[] { "Citizenship", "CVResume", "DegreeCertificate", "PoliceReport" }
            },
            new
            {
                Email = "sunita.rai.apply@tutorbridge.com",
                FullName = "Sunita Rai",
                Phone = "9845678945",
                District = "Lalitpur",
                Subjects = "English, Nepali, Social Studies",
                Education = "M.Ed English, Kathmandu University (2019)",
                Experience = "7 years school + private tutoring · Class 6-10",
                Years = 7,
                DaysAgo = 1,
                Status = "Pending",
                Documents = new[] { "Citizenship", "CVResume", "DegreeCertificate" } // Police report intentionally missing
            },
            new
            {
                Email = "bikram.tamang@tutorbridge.com",
                FullName = "Bikram Tamang",
                Phone = "9851239878",
                District = "Bhaktapur",
                Subjects = "Physics, Mathematics",
                Education = "B.Sc Physics, Purbanchal University (2023)",
                Experience = "2 years private tutoring · Grade 11-12, NEB Board",
                Years = 2,
                DaysAgo = 3,
                Status = "Pending",
                Documents = new[] { "Citizenship", "CVResume", "DegreeCertificate", "PoliceReport" }
            },
            new
            {
                Email = "manisha.thapa@tutorbridge.com",
                FullName = "Manisha Thapa",
                Phone = "9812345601",
                District = "Kathmandu",
                Subjects = "Accountancy, Economics",
                Education = "BBA Finance, Pokhara University (2020)",
                Experience = "4 years college + private tutoring · +2 level",
                Years = 4,
                DaysAgo = 5,
                Status = "Pending",
                Documents = new[] { "Citizenship", "CVResume", "DegreeCertificate", "PoliceReport" }
            },
            new
            {
                Email = "suresh.magar@tutorbridge.com",
                FullName = "Suresh Magar",
                Phone = "9860012399",
                District = "Chitwan",
                Subjects = "Computer Science",
                Education = "BE Computer Engineering, Tribhuvan University (2022)",
                Experience = "2 years private tutoring · Programming basics",
                Years = 2,
                DaysAgo = 1,
                Status = "Pending",
                Documents = new[] { "Citizenship", "CVResume" } // Degree certificate and police report missing
            },
            new
            {
                Email = "sabina.karki@tutorbridge.com",
                FullName = "Sabina Karki",
                Phone = "9803456712",
                District = "Kathmandu",
                Subjects = "Biology, Science",
                Education = "M.Sc Zoology, Tribhuvan University (2018)",
                Experience = "9 years school teaching · SEE and NEB Board",
                Years = 9,
                DaysAgo = 40,
                Status = "Approved",
                Documents = new[] { "Citizenship", "CVResume", "DegreeCertificate", "PoliceReport" }
            },
            new
            {
                Email = "deepak.oli@tutorbridge.com",
                FullName = "Deepak Oli",
                Phone = "9827788112",
                District = "Pokhara",
                Subjects = "Mathematics",
                Education = "B.Ed Mathematics, Tribhuvan University (2017)",
                Experience = "6 years private tutoring · Grade 9-12",
                Years = 6,
                DaysAgo = 20,
                Status = "Approved",
                Documents = new[] { "Citizenship", "CVResume", "DegreeCertificate", "PoliceReport" }
            },
            new
            {
                Email = "ritu.shah@tutorbridge.com",
                FullName = "Ritu Shah",
                Phone = "9819900221",
                District = "Kathmandu",
                Subjects = "English",
                Education = "Not disclosed",
                Experience = "No verifiable tutoring history provided",
                Years = 0,
                DaysAgo = 15,
                Status = "Rejected",
                Documents = new[] { "Citizenship" } // Missing everything else - rejected for incomplete documents
            },
        };

        foreach (var app in applications)
        {
            var user = await userManager.FindByEmailAsync(app.Email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = app.Email,
                    Email = app.Email,
                    FullName = app.FullName,
                    District = app.District,
                    PhoneNumber = app.Phone,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-app.DaysAgo)
                };

                await userManager.CreateAsync(user, "Tutor@123");
                await userManager.AddToRoleAsync(user, "Tutor");
            }

            var profile = await context.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == user.Id);
            if (profile == null)
            {
                profile = new TutorProfile
                {
                    UserId = user.Id,
                    Subjects = app.Subjects,
                    Education = app.Education,
                    ExperienceSummary = app.Experience,
                    YearsOfExperience = app.Years,
                    AverageRating = 0m,
                    ReviewCount = 0,
                    IsVerified = app.Status == "Approved",
                    VerificationRejected = app.Status == "Rejected",
                    VerificationDecidedAt = app.Status == "Pending"
                        ? null
                        : DateTime.UtcNow.AddDays(-app.DaysAgo + 1)
                };
                context.TutorProfiles.Add(profile);
                await context.SaveChangesAsync();
            }

            var hasDocuments = await context.TutorCredentials.AnyAsync(c => c.TutorProfileId == profile.Id);
            if (!hasDocuments)
            {
                var docLabels = new Dictionary<string, (string FileName, string Icon)>
                {
                    ["Citizenship"] = ("Citizenship.pdf", "🪪"),
                    ["CVResume"] = ("CV_Resume.pdf", "📄"),
                    ["DegreeCertificate"] = ("Degree_Certificate.pdf", "🎓"),
                    ["PoliceReport"] = ("Police_Report.pdf", "🛡️"),
                };

                var order = 0;
                foreach (var docType in app.Documents)
                {
                    var (fileName, icon) = docLabels[docType];
                    context.TutorCredentials.Add(new TutorCredential
                    {
                        TutorProfileId = profile.Id,
                        Title = fileName,
                        FileName = fileName,
                        Icon = icon,
                        SortOrder = order++,
                        DocumentType = docType
                    });
                }
            }
        }

        await context.SaveChangesAsync();
    }

    // Backfills notifications from data that already genuinely exists in
    // the database (seeded tutors, their real verification state, any
    // seeded support tickets) - never fabricates an event that didn't
    // happen. Only runs once: skips entirely if any notification already
    // exists, so it won't duplicate rows on every app restart or interfere
    // with notifications created live by real user actions afterward.
    public static async Task SeedNotificationsAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        if (await context.Notifications.AnyAsync()) return;

        var tutors = await context.TutorProfiles.Include(t => t.User).ToListAsync();

        foreach (var t in tutors.Where(t => !t.IsVerified && !t.VerificationRejected))
        {
            context.Notifications.Add(new Notification
            {
                Type = "Verification",
                Title = "New tutor verification submitted",
                Message = $"{t.User.FullName} submitted documents for {t.Subjects}",
                Icon = "🎓",
                ActionLabel = "Review now",
                ActionUrl = "/Admin/TutorVerification",
                CreatedAt = t.User.CreatedAt.AddHours(1),
                IsRead = false
            });
        }

        foreach (var t in tutors.Where(t => t.IsVerified && t.VerificationDecidedAt.HasValue))
        {
            context.Notifications.Add(new Notification
            {
                Type = "Verification",
                Title = "Tutor application approved",
                Message = $"{t.User.FullName}'s application for {t.Subjects} was approved",
                Icon = "✔️",
                CreatedAt = t.VerificationDecidedAt!.Value,
                IsRead = true
            });
        }

        foreach (var t in tutors.Where(t => t.VerificationRejected && t.VerificationDecidedAt.HasValue))
        {
            context.Notifications.Add(new Notification
            {
                Type = "Verification",
                Title = "Tutor application rejected",
                Message = $"{t.User.FullName}'s application was rejected" + (string.IsNullOrWhiteSpace(t.VerificationNote) ? "" : $": {t.VerificationNote}"),
                Icon = "✖️",
                CreatedAt = t.VerificationDecidedAt!.Value,
                IsRead = true
            });
        }

        await context.SaveChangesAsync();
    }
}