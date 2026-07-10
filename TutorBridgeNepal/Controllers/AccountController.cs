using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Models;
using TutorBridgeNepal.ViewModels;

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
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null || !await _userManager.IsInRoleAsync(user, model.Role))
        {
            ModelState.AddModelError("", "Invalid login details for this role.");
            return View(model);
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
            return View(model);
        }

        return model.Role switch
        {
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            "Tutor" => RedirectToAction("Dashboard", "Tutor"),
            "Student" => RedirectToAction("Dashboard", "Student"),
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
        }
        else
        {
            _context.StudentProfiles.Add(new StudentProfile
            {
                UserId = user.Id,
                GradeLevel = model.GradeLevel
            });
        }

        await _context.SaveChangesAsync();
        await _signInManager.SignInAsync(user, isPersistent: false);

        return model.Role switch
        {
            "Tutor" => RedirectToAction("Dashboard", "Tutor"),
            "Student" => RedirectToAction("Dashboard", "Student"),
            _ => RedirectToAction("Index", "Home")
        };
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