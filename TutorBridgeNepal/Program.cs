using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Models;
using TutorBridgeNepal.Services;
using Microsoft.AspNetCore.Authentication.Google;
using TutorBridgeNepal.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<GoogleOAuthOptions>(builder.Configuration.GetSection("Authentication:Google"));
builder.Services.AddHttpClient<GoogleCalendarService>();
builder.Services.AddHttpClient(); // generic IHttpClientFactory, used by the OAuth callback itself
builder.Services.AddScoped<TutorBridgeNepal.Services.IEmailSender, TutorBridgeNepal.Services.SmtpEmailSender>();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthentication()
    .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.CallbackPath = "/signin-google";
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
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

// Per-device session enforcement (Tutor > Settings > Linked devices).
// If the device behind this request has been revoked, sign it out
// immediately instead of waiting for the auth cookie to expire.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var deviceToken = context.Request.Cookies["tbn_device"];
        if (!string.IsNullOrEmpty(deviceToken))
        {
            var db = context.RequestServices.GetRequiredService<ApplicationDbContext>();
            var device = await db.UserDevices.FirstOrDefaultAsync(d => d.SessionToken == deviceToken);

            if (device == null || device.IsRevoked)
            {
                var signInManager = context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
                await signInManager.SignOutAsync();
                context.Response.Cookies.Delete("tbn_device");
                context.Response.Redirect("/Account/Login");
                return;
            }

            // Throttle the write - only bump LastActiveAt once a minute per
            // device instead of on every single request.
            if (DateTime.UtcNow - device.LastActiveAt > TimeSpan.FromMinutes(1))
            {
                device.LastActiveAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
    }

    await next();
});

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
app.MapHub<ChatHub>("/hubs/chat");

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedRolesAndAdminAsync(scope.ServiceProvider);
    await DbSeeder.SeedSampleTutorsAsync(scope.ServiceProvider);
    await DbSeeder.SeedTutorVerificationApplicationsAsync(scope.ServiceProvider);
    await DbSeeder.SeedNotificationsAsync(scope.ServiceProvider);
}

app.Run();