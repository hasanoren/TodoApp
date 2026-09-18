namespace TodoApp.Application.DTOs;

public class TwoFactorEnableResponse
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeUri { get; set; } = string.Empty;
}

