using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Models;
using TutorBridgeNepal.ViewModels;

namespace TutorBridgeNepal.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        private static string GetInitials(string fullName)
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
            return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        private static TutorSummaryViewModel ToSummary(TutorProfile t) => new()
        {
            TutorProfileId = t.Id,
            FullName = t.User.FullName,
            Initials = GetInitials(t.User.FullName),
            Subjects = t.Subjects,
            District = t.User.District,
            YearsOfExperience = t.YearsOfExperience,
            AverageRating = t.AverageRating,
            IsVerified = t.IsVerified
        };

        public async Task<IActionResult> Index()
        {
            var topTutors = await _context.TutorProfiles
                .Include(t => t.User)
                .Where(t => t.IsVerified)
                .OrderByDescending(t => t.AverageRating)
                .Take(3)
                .ToListAsync();

            var vm = new HomeIndexViewModel
            {
                TopTutors = topTutors.Select(ToSummary).ToList()
            };

            return View(vm);
        }

        public async Task<IActionResult> FindTutors(string? q)
        {
            var query = _context.TutorProfiles
                .Include(t => t.User)
                .Where(t => t.IsVerified)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(t =>
                    t.Subjects.Contains(term) ||
                    t.User.FullName.Contains(term) ||
                    (t.User.District != null && t.User.District.Contains(term)));
            }

            var tutors = await query
                .OrderByDescending(t => t.AverageRating)
                .ToListAsync();

            var vm = new FindTutorsViewModel
            {
                Query = q,
                Results = tutors.Select(ToSummary).ToList()
            };

            return View(vm);
        }

        public IActionResult HowItWorks()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        public IActionResult Cookies()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}