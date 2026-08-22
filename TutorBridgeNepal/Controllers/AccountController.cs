using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Helpers;
using TutorBridgeNepal.Models;
using TutorBridgeNepal.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace TutorBridgeNepal.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
    }

    private async Task StartDeviceSessionAsync(ApplicationUser user, bool isPersistent)
    {
        var sessionToken = Guid.NewGuid().ToString("N");

        _context.UserDevices.Add(new UserDevice
        {
            UserId = user.Id,
            SessionToken = sessionToken,
            DeviceLabel = DeviceHelper.DescribeUserAgent(Request.Headers["User-Agent"].ToString()),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        Response.Cookies.Append("tbn_device", sessionToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = isPersistent ? DateTimeOffset.UtcNow.AddDays(30) : (DateTimeOffset?)null,
            IsEssential = true
        });
    }

    [HttpGet]
    public IActionResult Login(string role = "Student")
    {
        return View(new LoginViewModel { Role = role });
    }

    [HttpGet]
    public IActionResult StudentLogin()
    {
        return View(new LoginViewModel { Role = "Student" });
    }

    [HttpGet]
    public IActionResult TutorLogin()
    {
        return View(new LoginViewModel { Role = "Tutor" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // Every failure path below needs to re-render whichever page the
        // submission actually came from - StudentLogin/TutorLogin don't have
        // the role toggle, so falling back to the generic "Login" view would
        // silently swap the page out from under the person mid-flow. Admin
        // has no separate view file - AdminLogin() itself renders "Login" -
        // so Role == "Admin" correctly falls through to the default case.
        IActionResult ViewForRole() => model.Role switch
        {
            "Student" => View("StudentLogin", model),
            "Tutor" => View("TutorLogin", model),
            _ => View("Login", model)
        };

        if (!ModelState.IsValid) return ViewForRole();

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null || !await _userManager.IsInRoleAsync(user, model.Role))
        {
            ModelState.AddModelError("", "Invalid login details for this role.");
            return ViewForRole();
        }

        if (user.IsSuspended)
        {
            ModelState.AddModelError("", "This account has been suspended. Contact support for help.");
            return ViewForRole();
        }

        if (model.Role == "Tutor")
        {
            var tutorProfile = await _context.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == user.Id);
            if (tutorProfile != null && tutorProfile.IsDeactivated)
            {
                ModelState.AddModelError("", "This tutor account has been deactivated. Contact support to reactivate it.");
                return ViewForRole();
            }
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
        {
            return RedirectToAction("VerifyAuthenticatorCode", new { rememberMe = model.RememberMe });
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return ViewForRole();
        }

        await StartDeviceSessionAsync(user, model.RememberMe);

        return model.Role switch
        {
            "Tutor" => RedirectToAction("VerificationPending", "Tutor"),
            "Student" => RedirectToAction("Dashboard", "Student"),
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    [HttpGet]
    public IActionResult VerifyAuthenticatorCode(bool rememberMe = false)
    {
        return View(new TwoFactorViewModel { RememberMe = rememberMe });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyAuthenticatorCode(TwoFactorViewModel model)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            return RedirectToAction("AdminLogin");
        }

        if (!ModelState.IsValid) return View(model);

        var code = (model.Code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);

        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(code, model.RememberMe, rememberClient: false);

        if (result.Succeeded)
        {
            await StartDeviceSessionAsync(user, model.RememberMe);
            return RedirectToAction("Dashboard", "Admin");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError("", "Account locked out due to too many failed attempts. Try again later.");
            return View(model);
        }

        ModelState.AddModelError("", "Invalid authenticator code. Please try again.");
        return View(model);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> AdminSetupAuthenticator()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("AdminLogin");

        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = await _userManager.GetEmailAsync(user);
        const string issuer = "TutorBridgeNepal";
        var otpAuthUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email!)}" +
                          $"?secret={unformattedKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6";

        var vm = new AuthenticatorSetupViewModel
        {
            SharedKey = FormatKey(unformattedKey!),
            QrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=220x220&data={Uri.EscapeDataString(otpAuthUri)}",
            Is2faEnabled = user.TwoFactorEnabled
        };

        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminEnableAuthenticator(string verificationCode)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("AdminLogin");

        var code = (verificationCode ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!isValid)
        {
            TempData["2faError"] = "Invalid verification code. Please try again.";
            return RedirectToAction("AdminSetupAuthenticator");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        TempData["2faSuccess"] = "Two-factor authentication has been enabled for your admin account.";
        return RedirectToAction("AdminSetupAuthenticator");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDisableAuthenticator()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);

        return RedirectToAction("AdminSetupAuthenticator");
    }

    [Authorize(Roles = "Tutor")]
    [HttpGet]
    public async Task<IActionResult> TutorSetupAuthenticator()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("TutorLogin");

        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = await _userManager.GetEmailAsync(user);
        const string issuer = "TutorBridgeNepal";
        var otpAuthUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email!)}" +
                          $"?secret={unformattedKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6";

        var vm = new AuthenticatorSetupViewModel
        {
            SharedKey = FormatKey(unformattedKey!),
            QrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=220x220&data={Uri.EscapeDataString(otpAuthUri)}",
            Is2faEnabled = user.TwoFactorEnabled
        };

        return View(vm);
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TutorEnableAuthenticator(string verificationCode)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("TutorLogin");

        var code = (verificationCode ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!isValid)
        {
            TempData["2faError"] = "Invalid verification code. Please try again.";
            return RedirectToAction("TutorSetupAuthenticator");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        TempData["2faSuccess"] = "Two-factor authentication has been enabled for your account.";
        return RedirectToAction("TutorSetupAuthenticator");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Tutor")]
    public async Task<IActionResult> TutorDisableAuthenticator()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("TutorLogin");

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);

        return RedirectToAction("TutorSetupAuthenticator");
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }
        return result.ToString().ToLowerInvariant();
    }

    [HttpGet]
    public IActionResult ForgotPassword(string role = "Student")
    {
        return View(new ForgotPasswordViewModel { Role = role });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null || !await _userManager.IsInRoleAsync(user, model.Role))
        {
            // Don't reveal whether the account exists.
            return View("ForgotPasswordConfirmation", model);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = Url.Action("ResetPassword", "Account",
            new { email = model.Email, token, role = model.Role }, Request.Scheme);

        // No email provider is configured yet, so the reset link is shown directly
        // on the confirmation page instead of being emailed.
        ViewData["ResetLink"] = resetLink;
        return View("ForgotPasswordConfirmation", model);
    }

    [HttpGet]
    public IActionResult ResetPassword(string email, string token, string role = "Student")
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            return RedirectToAction("ForgotPassword", new { role });
        }

        return View(new ResetPasswordViewModel { Email = email, Token = token, Role = role });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return View("ResetPasswordConfirmation");
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        return RedirectToAction("ResetPasswordConfirmation", new { role = model.Role });
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation(string role = "Student")
    {
        ViewData["Role"] = role;
        return View();
    }

    [HttpGet]
    public IActionResult Register(string role = "Student")
    {
        return View(new RegisterViewModel { Role = role });
    }

    [HttpGet]
    public IActionResult StudentRegister()
    {
        return View(new RegisterViewModel { Role = "Student" });
    }

    [HttpGet]
    public IActionResult TutorRegister()
    {
        return View(new RegisterViewModel { Role = "Tutor" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            District = model.District
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, model.Role);

        if (model.Role == "Tutor")
        {
            _context.TutorProfiles.Add(new TutorProfile
            {
                UserId = user.Id,
                Subjects = model.Subjects ?? "",
                YearsOfExperience = model.YearsOfExperience,
                IsVerified = false
            });

            NotificationHelper.Create(_context,
                type: "System",
                title: "New tutor registration",
                message: $"{user.FullName} registered to teach {(string.IsNullOrWhiteSpace(model.Subjects) ? "unspecified subjects" : model.Subjects)}",
                icon: "🧑‍🏫",
                actionLabel: "View profile",
                actionUrl: Url.Action("UserManagement", "Admin", new { search = user.Email }));
        }
        else
        {
            _context.StudentProfiles.Add(new StudentProfile
            {
                UserId = user.Id,
                GradeLevel = model.GradeLevel
            });

            NotificationHelper.Create(_context,
                type: "System",
                title: "New student registration",
                message: $"{user.FullName} registered" + (string.IsNullOrWhiteSpace(user.District) ? "" : $" from {user.District}") + (string.IsNullOrWhiteSpace(model.GradeLevel) ? "" : $" · {model.GradeLevel}"),
                icon: "🧑‍🎓",
                actionLabel: "View profile",
                actionUrl: Url.Action("UserManagement", "Admin", new { search = user.Email }));
        }

        await _context.SaveChangesAsync();
        await _signInManager.SignInAsync(user, isPersistent: false);
        await StartDeviceSessionAsync(user, false);

        return model.Role switch
        {
            "Tutor" => RedirectToAction("VerificationPending", "Tutor"),
            "Student" => RedirectToAction("Dashboard", "Student"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult GoogleLogin(string role)
    {
        var redirectUrl = Url.Action("GoogleCallback", "Account", new { role });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
        return Challenge(properties, "Google");
    }

    [HttpGet]
    public async Task<IActionResult> GoogleCallback(string role = "Student", string? remoteError = null)
    {
        IActionResult BackToLogin(string message)
        {
            TempData["GoogleAuthError"] = message;
            return RedirectToAction(role == "Tutor" ? "TutorLogin" : "StudentLogin");
        }

        if (remoteError != null)
        {
            return BackToLogin("Google sign-in was cancelled or failed.");
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            return BackToLogin("Google sign-in failed. Please try again.");
        }

        // Case 1: this Google account is already linked to a TutorBridge account.
        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signInResult.Succeeded)
        {
            var existingUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            return await RedirectAfterExternalSignInAsync(existingUser!);
        }

        if (signInResult.IsLockedOut)
        {
            return BackToLogin("This account is temporarily locked. Please try again later.");
        }

        // Case 2: no link yet - see if a TutorBridge account already exists
        // with this Google account's email, and link it if so.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var fullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email ?? "TutorBridge User";

        if (string.IsNullOrWhiteSpace(email))
        {
            return BackToLogin("Couldn't get your email address from Google. Please try a different sign-in method.");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            if (user.IsSuspended)
            {
                return BackToLogin("This account has been suspended. Contact support for help.");
            }

            var addLoginResult = await _userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
            {
                return BackToLogin("Couldn't link your Google account. Please try logging in with your password instead.");
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return await RedirectAfterExternalSignInAsync(user);
        }

        // Case 3: brand new person - create an account using the role of the
        // page they started from. This is what makes the same button work as
        // both "sign in" (StudentLogin/TutorLogin) and "sign up"
        // (StudentRegister/TutorRegister) - first-time use just creates the account.
        var newUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true // Google already verified this email address
        };

        var createResult = await _userManager.CreateAsync(newUser);
        if (!createResult.Succeeded)
        {
            return BackToLogin("Couldn't create an account with that Google email. Please try registering with a password instead.");
        }

        await _userManager.AddToRoleAsync(newUser, role);
        await _userManager.AddLoginAsync(newUser, info);

        if (role == "Tutor")
        {
            _context.TutorProfiles.Add(new TutorProfile
            {
                UserId = newUser.Id,
                Subjects = "",
                YearsOfExperience = 0,
                IsVerified = false
            });

            NotificationHelper.Create(_context,
                type: "System",
                title: "New tutor registration",
                message: $"{newUser.FullName} registered via Google (subjects not yet set)",
                icon: "🧑‍🏫",
                actionLabel: "View profile",
                actionUrl: Url.Action("UserManagement", "Admin", new { search = newUser.Email }));
        }
        else
        {
            _context.StudentProfiles.Add(new StudentProfile { UserId = newUser.Id });

            NotificationHelper.Create(_context,
                type: "System",
                title: "New student registration",
                message: $"{newUser.FullName} registered via Google",
                icon: "🧑‍🎓",
                actionLabel: "View profile",
                actionUrl: Url.Action("UserManagement", "Admin", new { search = newUser.Email }));
        }

        await _context.SaveChangesAsync();
        await _signInManager.SignInAsync(newUser, isPersistent: false);

        return await RedirectAfterExternalSignInAsync(newUser);
    }

    private async Task<IActionResult> RedirectAfterExternalSignInAsync(ApplicationUser user)
    {
        if (await _userManager.IsInRoleAsync(user, "Tutor")) return RedirectToAction("VerificationPending", "Tutor");
        if (await _userManager.IsInRoleAsync(user, "Student")) return RedirectToAction("Dashboard", "Student");
        if (await _userManager.IsInRoleAsync(user, "Admin")) return RedirectToAction("Dashboard", "Admin");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Route("portal-tutorbridgenepal/admin-access")]
    public IActionResult AdminLogin()
    {
        return View("Login", new LoginViewModel { Role = "Admin" });
    }
}