namespace TodoApp.Application.DTOs;

public class TwoFactorLoginRequest
{
    public string TwoFactorToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}


