using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Models;

namespace TutorBridgeNepal.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChatHub(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public static string GroupName(int studentProfileId, int tutorProfileId) =>
        $"thread-{studentProfileId}-{tutorProfileId}";

    public async Task JoinThread(int studentProfileId, int tutorProfileId)
    {
        if (!await CallerBelongsToThreadAsync(studentProfileId, tutorProfileId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(studentProfileId, tutorProfileId));
    }

    public async Task LeaveThread(int studentProfileId, int tutorProfileId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(studentProfileId, tutorProfileId));
    }

    // Prevents anyone from joining a thread they aren't actually part of.
    private async Task<bool> CallerBelongsToThreadAsync(int studentProfileId, int tutorProfileId)
    {
        var user = await _userManager.GetUserAsync(Context.User!);
        if (user == null) return false;

        var isThatStudent = await _context.StudentProfiles.AnyAsync(s => s.Id == studentProfileId && s.UserId == user.Id);
        var isThatTutor = await _context.TutorProfiles.AnyAsync(t => t.Id == tutorProfileId && t.UserId == user.Id);
        return isThatStudent || isThatTutor;
    }
}