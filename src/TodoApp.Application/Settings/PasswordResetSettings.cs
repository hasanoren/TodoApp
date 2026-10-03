namespace TodoApp.Application.Settings;

public class PasswordResetSettings
{
    public const string SectionName = "PasswordReset";

    public int ExpiryMinutes { get; set; } = 60;
    public string ResetUrl { get; set; } = "todoapp://reset-password?token={token}&email={email}";
}
