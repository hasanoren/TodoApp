namespace TodoApp.Domain.Entities;

public enum UserRole
{
    User = 0,
    Admin = 1
}

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;      // BR-001: unique constraint (DB seviyesinde ayrıca tanımlanacak)
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;     // BR-005: default olarak "User"

    // İki Adımlı Doğrulama (2FA) Alanları
    public bool TwoFactorEnabled { get; set; } = false;
    public string? TwoFactorSecret { get; set; }

    // Hesap Kilitleme (Account Lockout - T10.2.1)
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }

    // JWT Token İptali / Oturum Geçersiz Kılma (SecurityStamp - T10.2.2)
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>(); // YENİ

    public ICollection<TaskShare> SharedTasks { get; set; } = new List<TaskShare>();
}