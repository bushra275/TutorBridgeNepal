namespace TutorBridgeNepal.ViewModels;

public class TwoFactorViewModel
{
    public string Code { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class AuthenticatorSetupViewModel
{
    public string SharedKey { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public bool Is2faEnabled { get; set; }
}