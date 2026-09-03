using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DnsClient;
using DnsClient.Protocol;
using System.Net;
using System.Security.Claims;
using System.Text;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Helpers;
using TutorBridgeNepal.Models;
using TutorBridgeNepal.ViewModels;

namespace TutorBridgeNepal.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly TutorBridgeNepal.Services.IEmailSender _emailSender;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        TutorBridgeNepal.Services.IEmailSender emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _emailSender = emailSender;
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

        // Admin accounts get a fresh emailed code on every single login, on
        // top of the password - this runs before the EmailConfirmed check
        // below because the seeded admin has EmailConfirmed = true and would
        // otherwise skip straight through to a normal sign-in.
        if (model.Role == "Admin")
        {
            var adminPasswordCheck = await _signInManager.CheckPasswordSignInAsync(
                user, model.Password, lockoutOnFailure: true);

            if (!adminPasswordCheck.Succeeded)
            {
                ModelState.AddModelError("", adminPasswordCheck.IsLockedOut
                    ? "This account is locked out. Try again later."
                    : "Invalid email or password.");
                return ViewForRole();
            }

            await SendEmailOtpAsync(user);
            return RedirectToAction("VerifyAdminCode", new { email = user.Email, rememberMe = model.RememberMe });
        }

        if (!user.EmailConfirmed)
        {
            // Verify password first so this doesn't become a way to check
            // whether an email is registered without knowing the password.
            var passwordCheck = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordCheck)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return ViewForRole();
            }

            if (!await SendEmailOtpAsync(user))
            {
                TempData["SettingsError"] = "We couldn't send a code to that email address. Double-check it's correct, then tap Resend code below.";
            }
            return RedirectToAction("VerifyEmail", new { email = user.Email });
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
            return RedirectToAction("VerifyAuthenticatorCode", new { rememberMe = model.RememberMe, role = model.Role });
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
    public IActionResult VerifyAuthenticatorCode(bool rememberMe = false, string role = "")
    {
        return View(new TwoFactorViewModel { RememberMe = rememberMe, Role = role });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyAuthenticatorCode(TwoFactorViewModel model)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            return model.Role switch
            {
                "Tutor" => RedirectToAction("TutorLogin"),
                "Student" => RedirectToAction("StudentLogin"),
                _ => RedirectToAction("AdminLogin")
            };
        }

        if (!ModelState.IsValid) return View(model);

        var code = (model.Code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);

        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(code, model.RememberMe, rememberClient: false);

        if (result.Succeeded)
        {
            await StartDeviceSessionAsync(user, model.RememberMe);
            return model.Role switch
            {
                "Tutor" => RedirectToAction("VerificationPending", "Tutor"),
                "Student" => RedirectToAction("Dashboard", "Student"),
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                _ => RedirectToAction("Index", "Home")
            };
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

        var sender = _emailSender as TutorBridgeNepal.Services.SmtpEmailSender;
        if (sender != null && sender.IsConfigured)
        {
            var subject = "Reset your TutorBridge Nepal password";
            var body = $@"
                <p>Hi {WebUtility.HtmlEncode(user.FullName)},</p>
                <p>We received a request to reset your TutorBridge Nepal password. Click the link below to choose a new one:</p>
                <p><a href=""{resetLink}"">Reset my password</a></p>
                <p>If you didn't request this, you can safely ignore this email - your password won't be changed.</p>
                <p>This link will expire shortly for your security.</p>";

            await _emailSender.SendEmailAsync(model.Email, subject, body);
        }
        else
        {
            // SMTP isn't configured (EmailSettings is empty), so fall back to
            // showing the reset link directly on the confirmation page. Fill
            // in appsettings.Development.json's EmailSettings to send a real
            // email instead.
            ViewData["ResetLink"] = resetLink;
        }

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

    // Returns false if the email genuinely couldn't be sent (bad address,
    // SMTP rejected it, connection failure, etc.) so callers can show a
    // clear message instead of letting the exception crash the request.
    private async Task<bool> SendEmailOtpAsync(ApplicationUser user)
    {
        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.EmailOtpCode = code;
        user.EmailOtpExpiresAt = DateTime.UtcNow.AddMinutes(10);
        await _userManager.UpdateAsync(user);

        var subject = "Your TutorBridge Nepal verification code";
        var body = $@"
            <p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
            <p>Your verification code is:</p>
            <p style=""font-size:28px; font-weight:700; letter-spacing:4px;"">{code}</p>
            <p>This code expires in 10 minutes. If you didn't request this, you can ignore this email.</p>";

        var sender = _emailSender as TutorBridgeNepal.Services.SmtpEmailSender;
        if (sender != null && sender.IsConfigured)
        {
            try
            {
                await _emailSender.SendEmailAsync(user.Email!, subject, body);
            }
            catch
            {
                // Already logged inside SmtpEmailSender - just report the
                // failure up so the caller can show a friendly message
                // instead of a crashed request.
                return false;
            }
        }
        else
        {
            // SMTP isn't configured yet - surface the code directly so
            // registration/login still works end-to-end during development.
            TempData["DevOtpCode"] = code;
        }

        return true;
    }

    // Called from the registration wizard's step-1 "Next" click. Checks
    // whether the email's domain actually has mail servers configured
    // (MX records) - catches obviously fake domains like "zyz.com"
    // immediately, instead of only failing much later at the OTP step.
    // Deliberately fails OPEN (returns valid) if the DNS lookup itself
    // errors out or times out, rather than blocking registration entirely
    // over a network hiccup - the OTP step is still the real backstop.
    [HttpGet]
    public async Task<IActionResult> CheckEmailDomain(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return Json(new { valid = false });
        }

        var domain = email.Split('@').Last().Trim();

        try
        {
            var lookup = new LookupClient(new LookupClientOptions { Timeout = TimeSpan.FromSeconds(4) });
            var result = await lookup.QueryAsync(domain, QueryType.MX);
            var hasMx = result.Answers.OfType<MxRecord>().Any();
            return Json(new { valid = hasMx });
        }
        catch
        {
            return Json(new { valid = true });
        }
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
        // Same reasoning as Login's ViewForRole(): StudentRegister/
        // TutorRegister don't share a view with the generic "Register" page,
        // so a bare View(model) here would silently swap the person onto the
        // wrong page (the generic role-toggle one) instead of showing the
        // error back on the wizard they were actually using.
        IActionResult ViewForRole() => model.Role switch
        {
            "Student" => View("StudentRegister", model),
            "Tutor" => View("TutorRegister", model),
            _ => View(model)
        };

        if (!ModelState.IsValid) return ViewForRole();

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return ViewForRole();
        }

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
            return ViewForRole();
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
        if (!await SendEmailOtpAsync(user))
        {
            TempData["SettingsError"] = "We couldn't send a code to that email address. Double-check it's correct, then tap Resend code below.";
        }

        return RedirectToAction("VerifyEmail", new { email = user.Email });
    }

    [HttpGet]
    public IActionResult VerifyEmail(string email)
    {
        return View(new VerifyEmailViewModel { Email = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError("", "We couldn't find that account.");
            return View(model);
        }

        if (user.EmailConfirmed)
        {
            // Already verified (e.g. they hit back after succeeding once) -
            // just let them through instead of erroring.
        }
        else if (string.IsNullOrEmpty(user.EmailOtpCode)
            || user.EmailOtpExpiresAt == null
            || user.EmailOtpExpiresAt < DateTime.UtcNow
            || user.EmailOtpCode != model.Code)
        {
            ModelState.AddModelError(nameof(model.Code), "That code is incorrect or has expired. Request a new one below.");
            return View(model);
        }
        else
        {
            user.EmailConfirmed = true;
            user.EmailOtpCode = null;
            user.EmailOtpExpiresAt = null;
            await _userManager.UpdateAsync(user);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        await StartDeviceSessionAsync(user, false);

        var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
        return role switch
        {
            "Tutor" => RedirectToAction("VerificationPending", "Tutor"),
            "Student" => RedirectToAction("Dashboard", "Student"),
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendEmailOtp(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null && !user.EmailConfirmed)
        {
            if (await SendEmailOtpAsync(user))
            {
                TempData["SettingsSuccessGlobal"] = "A new code has been sent to your email.";
            }
            else
            {
                TempData["SettingsError"] = "We couldn't send a code to that email address. Double-check it's correct, then try again.";
            }
        }

        return RedirectToAction("VerifyEmail", new { email });
    }

    [HttpGet]
    public IActionResult VerifyAdminCode(string email, bool rememberMe = false)
    {
        return View(new AdminOtpViewModel { Email = email, RememberMe = rememberMe });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyAdminCode(AdminOtpViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
        {
            ModelState.AddModelError("", "We couldn't find that admin account.");
            return View(model);
        }

        if (string.IsNullOrEmpty(user.EmailOtpCode)
            || user.EmailOtpExpiresAt == null
            || user.EmailOtpExpiresAt < DateTime.UtcNow
            || user.EmailOtpCode != model.Code)
        {
            ModelState.AddModelError(nameof(model.Code), "That code is incorrect or has expired. Request a new one below.");
            return View(model);
        }

        user.EmailOtpCode = null;
        user.EmailOtpExpiresAt = null;
        await _userManager.UpdateAsync(user);

        await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);
        await StartDeviceSessionAsync(user, model.RememberMe);

        return RedirectToAction("Dashboard", "Admin");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendAdminCode(string email, bool rememberMe)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
        {
            await SendEmailOtpAsync(user);
            TempData["SettingsSuccessGlobal"] = "A new code has been sent to your email.";
        }

        return RedirectToAction("VerifyAdminCode", new { email, rememberMe });
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