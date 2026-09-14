namespace TodoApp.Application.Settings;

public class PasswordResetSettings
{
    public const string SectionName = "PasswordReset";

    public int ExpiryMinutes { get; set; } = 60;
}

