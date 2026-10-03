namespace TodoApp.Application.Settings;

public class PasswordResetSettings
{
    public const string SectionName = "PasswordReset";

    public int ExpiryMinutes { get; set; } = 60;
    public string ResetUrl { get; set; } = "https://todoapp-api-gudhgje6bvfqg3ev.centralus-01.azurewebsites.net/reset-password?token={token}&email={email}";
}
