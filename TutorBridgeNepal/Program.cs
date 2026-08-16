using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Platform maintenance mode (Admin > Settings > Platform configuration).
// Admins and the Account controller (so an admin can still log in) always
// pass through; everyone else gets a 503 maintenance page while it's on.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var isExempt = path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase);

    if (!isExempt && !(context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("Admin")))
    {
        var db = context.RequestServices.GetRequiredService<ApplicationDbContext>();
        var settings = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync();
        if (settings != null && settings.PlatformMaintenanceMode)
        {
            context.Response.StatusCode = 503;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(
                "<!DOCTYPE html><html><head><title>Under maintenance - TutorBridge Nepal</title>" +
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" /></head>" +
                "<body style=\"font-family:Inter,Arial,sans-serif;background:#f6f7f4;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;\">" +
                "<div style=\"text-align:center;max-width:420px;padding:2rem;\">" +
                "<h1 style=\"color:#1f6f54;margin-bottom:.5rem;\">We'll be right back</h1>" +
                "<p style=\"color:#555;\">TutorBridge Nepal is undergoing scheduled maintenance. Please check back shortly.</p>" +
                "</div></body></html>");
            return;
        }
    }

    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedRolesAndAdminAsync(scope.ServiceProvider);
    await DbSeeder.SeedSampleTutorsAsync(scope.ServiceProvider);
    await DbSeeder.SeedTutorVerificationApplicationsAsync(scope.ServiceProvider);
    await DbSeeder.SeedNotificationsAsync(scope.ServiceProvider);
}

app.Run();