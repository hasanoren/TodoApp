# API Controllers Endpoint İş Akışları ve Kod Rehberi (AuthController & TodoItemsController)

Bu doküman, `AuthController` ve `TodoItemsController` içerisindeki tüm endpoint'lerin istemciden başlayıp veritabanına ve yanıt dönüşüne kadar izlediği mimari iş akışlarını ve çalıştırma sırasına göre tüm C# kod parçalarını içermektedir.

---

# BÖLÜM 1: AuthController Endpoints

## 1. POST /api/Auth/register (Kullanıcı Kaydı)

### Part 1: Adım Adım Mimari İş Akışı

1. **İstek & Middleware Katmanı:**
   * İstemci `POST /api/Auth/register` adresine `email` ve `password` içeren JSON gövdesi ile istek atar.
   * İstek `ExceptionHandlingMiddleware` içerisinden geçer. Herhangi bir domain istisnası (`ConflictException` vb.) yakalanarak HTTP statü koduna çevrilmek üzere dinlenir.

2. **Model Binding & Input Validation (FluentValidation):**
   * İstek gövdesi `RegisterRequest` DTO nesnesine bind edilir.
   * `RegisterRequestValidator` çalışır:
     * `Email`: Boş olamaz, geçerli format, maks 256 karakter.
     * `Password`: Boş olamaz, min 8 - maks 128 karakter (BCrypt DoS koruması), en az 1 büyük harf, 1 küçük harf, 1 rakam ve 1 özel karakter içermeli.
   * *Hata durumunda:* ASP.NET Core filtresi `400 Bad Request` döner.

3. **Controller Katmanı:**
   * `AuthController.Register` action metodu isteği alır ve `_authService.RegisterAsync(request)` metodunu çağırır.

4. **Application Service & İş Kuralları (`AuthService`):**
   * **BR-001 (Email Uniqueness):** `_userRepository.GetByEmailAsync(request.Email)` çağrılır. E-posta sistemde zaten varsa `ConflictException` fırlatılır (Middleware bunu `409 Conflict` yanıtına çevirir).
   * **Şifre Hashleme:** `BCrypt.Net.BCrypt.HashPassword(request.Password)` ile şifre güvenli olarak hashlenir.
   * **Entity Oluşturma:** `Role = UserRole.User` ve `CreatedAt = DateTime.UtcNow` ile yeni `User` nesnesi oluşturulur.
   * **DB Kaydı:** `_userRepository.AddAsync(user)` ve `_userRepository.SaveChangesAsync()` ile veritabanına kaydedilir.
   * **Token Üretimi:** `GenerateAuthResponseAsync` üzerinden 60 dakikalık JWT Access Token ve 7 günlük Refresh Token oluşturulup `RefreshTokens` tablosuna kaydedilir.

5. **HTTP Yanıtı:**
   * Controller `200 OK` statü kodu ile `AuthResponse` (`userId`, `email`, `token`, `refreshToken`) nesnesini döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 1.1 `RegisterRequest.cs` (DTO)
[RegisterRequest.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/DTOs/RegisterRequest.cs)
```csharp
namespace TodoApp.Application.DTOs;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
```

#### 1.2 `RegisterRequestValidator.cs` (FluentValidation)
[RegisterRequestValidator.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Validators/RegisterRequestValidator.cs)
```csharp
using FluentValidation;
using TodoApp.Application.DTOs;

namespace TodoApp.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
            .MaximumLength(256).WithMessage("E-posta adresi en fazla 256 karakter olabilir.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre zorunludur.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.")
            .MaximumLength(128).WithMessage("Şifre en fazla 128 karakter olabilir.")
            .Matches(@"[A-Z]").WithMessage("Şifre en az bir büyük harf içermelidir.")
            .Matches(@"[a-z]").WithMessage("Şifre en az bir küçük harf içermelidir.")
            .Matches(@"[0-9]").WithMessage("Şifre en az bir rakam içermelidir.")
            .Matches(@"[!@#$%^&*(),.?"":{}|<>]").WithMessage("Şifre en az bir özel karakter içermelidir.");
    }
}
```

#### 1.3 `AuthController.cs` -> `Register` Action
[AuthController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/AuthController.cs#L20-L27)
```csharp
[HttpPost("register")]
public async Task<IActionResult> Register(RegisterRequest request)
{
    var result = await _authService.RegisterAsync(request);
    return Ok(result);
}
```

#### 1.4 `AuthService.cs` -> `RegisterAsync`
[AuthService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/AuthService.cs#L31-L52)
```csharp
public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
{
    var existingUser = await _userRepository.GetByEmailAsync(request.Email);
    if (existingUser is not null)
    {
        throw new ConflictException("Bu e-posta adresi zaten kayıtlı.");
    }

    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = request.Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        Role = UserRole.User,
        CreatedAt = DateTime.UtcNow
    };

    await _userRepository.AddAsync(user);
    await _userRepository.SaveChangesAsync();

    return await GenerateAuthResponseAsync(user);
}
```

#### 1.5 `UserRepository.cs` -> `GetByEmailAsync` & `AddAsync`
[UserRepository.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Infrastructure/Repositories/UserRepository.cs#L17-L30)
```csharp
public async Task<User?> GetByEmailAsync(string email)
{
    return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
}

public async Task AddAsync(User user)
{
    await _context.Users.AddAsync(user);
}

public async Task SaveChangesAsync()
{
    await _context.SaveChangesAsync();
}
```

#### 1.6 `AuthService.cs` -> `GenerateAuthResponseAsync`
[AuthService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/AuthService.cs#L79-L104)
```csharp
private async Task<AuthResponse> GenerateAuthResponseAsync(User user)
{
    var accessToken = _jwtTokenGenerator.GenerateToken(user);
    var (refreshTokenValue, expiresAt) = _jwtTokenGenerator.GenerateRefreshToken();

    var refreshToken = new RefreshToken
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        Token = refreshTokenValue,
        ExpiresAt = expiresAt,
        IsRevoked = false,
        CreatedAt = DateTime.UtcNow
    };

    await _refreshTokenRepository.AddAsync(refreshToken);
    await _refreshTokenRepository.SaveChangesAsync();

    return new AuthResponse
    {
        UserId = user.Id,
        Email = user.Email,
        Token = accessToken,
        RefreshToken = refreshTokenValue
    };
}
```

---

## 2. POST /api/Auth/login (Kullanıcı Girişi)

### Part 1: Adım Adım Mimari İş Akışı

1. **İstek & Validation:**
   * İstemci `POST /api/Auth/login` adresine `email` ve `password` bilgilerini gönderir.
   * `LoginRequestValidator` ile e-posta formatı ve şifre zorunluluğu doğrulanır.
2. **Kullanıcı & Şifre Doğrulama (`AuthService.LoginAsync`):**
   * `_userRepository.GetByEmailAsync(request.Email)` ile kullanıcı aranır.
   * Kullanıcı yoksa **VEYA** `BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)` `false` dönerse `throw new ValidationException("E-posta veya şifre hatalı.");` fırlatılır.
   * Middleware bu istisnayı yakalayıp `400 Bad Request` döner.
3. **Token Üretimi & Oturum Kaydı:**
   * Doğrulama başarılıysa `GenerateAuthResponseAsync(user)` çalışarak yeni JWT ve Refresh Token üretilip veritabanına eklenir.
4. **HTTP Yanıtı:**
   * `200 OK` ve `AuthResponse` DTO döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 2.1 `LoginRequest.cs`
[LoginRequest.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/DTOs/LoginRequest.cs)
```csharp
namespace TodoApp.Application.DTOs;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
```

#### 2.2 `LoginRequestValidator.cs`
[LoginRequestValidator.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Validators/LoginRequestValidator.cs)
```csharp
using FluentValidation;
using TodoApp.Application.DTOs;

namespace TodoApp.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
            .MaximumLength(256).WithMessage("E-posta adresi en fazla 256 karakter olabilir.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre zorunludur.")
            .MaximumLength(128).WithMessage("Şifre en fazla 128 karakter olabilir.");
    }
}
```

#### 2.3 `AuthController.cs` -> `Login` Action
[AuthController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/AuthController.cs#L29-L34)
```csharp
[HttpPost("login")]
public async Task<IActionResult> Login(LoginRequest request)
{
    var result = await _authService.LoginAsync(request);
    return Ok(result);
}
```

#### 2.4 `AuthService.cs` -> `LoginAsync`
[AuthService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/AuthService.cs#L54-L63)
```csharp
public async Task<AuthResponse> LoginAsync(LoginRequest request)
{
    var user = await _userRepository.GetByEmailAsync(request.Email);
    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
        throw new ValidationException("E-posta veya şifre hatalı.");
    }

    return await GenerateAuthResponseAsync(user);
}
```

---

## 3. POST /api/Auth/refresh (Refresh Token ile Access Token Yenileme)

### Part 1: Adım Adım Mimari İş Akışı

1. **İstek & Validation:**
   * Süresi dolmuş Access Token'a sahip istemci `POST /api/Auth/refresh` adresine `refreshToken` string'i gönderir.
   * `RefreshTokenRequestValidator` ile token'ın boş olmaması doğrulanır.
2. **Token Kontrolü (`AuthService.RefreshTokenAsync`):**
   * `_refreshTokenRepository.GetByTokenAsync(request.RefreshToken)` ile token veritabanında sorgulanır (ilişkili `User` entity'si dahil çekilir).
   * Token veritabanında bulunamazsa **VEYA** `!storedToken.IsActive` (süresi dolmuşsa/iptal edilmişse) `throw new ValidationException("Geçersiz veya süresi dolmuş refresh token.");` fırlatılır (`400 Bad Request`).
3. **Token Rotasyonu (Token Rotation):**
   * Eski refresh token iptal edilir (`storedToken.IsRevoked = true`).
   * `GenerateAuthResponseAsync(storedToken.User)` çağrılarak kullanıcı için **yeni** bir JWT Access Token ve **yeni** bir Refresh Token üretilip veritabanına eklenir.
4. **HTTP Yanıtı:**
   * `200 OK` ve yeni `AuthResponse` DTO döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 3.1 `RefreshTokenRequest.cs`
[RefreshTokenRequest.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/DTOs/RefreshTokenRequest.cs)
```csharp
namespace TodoApp.Application.DTOs;

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
```

#### 3.2 `RefreshTokenRequestValidator.cs`
[RefreshTokenRequestValidator.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Validators/RefreshTokenRequestValidator.cs)
```csharp
using FluentValidation;
using TodoApp.Application.DTOs;

namespace TodoApp.Application.Validators;

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token zorunludur.");
    }
}
```

#### 3.3 `AuthController.cs` -> `Refresh` Action
[AuthController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/AuthController.cs#L36-L41)
```csharp
[HttpPost("refresh")]
public async Task<IActionResult> Refresh(RefreshTokenRequest request)
{
    var result = await _authService.RefreshTokenAsync(request);
    return Ok(result);
}
```

#### 3.4 `AuthService.cs` -> `RefreshTokenAsync`
[AuthService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/AuthService.cs#L65-L77)
```csharp
public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
{
    var storedToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

    if (storedToken is null || !storedToken.IsActive)
    {
        throw new ValidationException("Geçersiz veya süresi dolmuş refresh token.");
    }

    storedToken.IsRevoked = true;

    return await GenerateAuthResponseAsync(storedToken.User);
}
```

#### 3.5 `RefreshTokenRepository.cs` -> `GetByTokenAsync`
[RefreshTokenRepository.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Infrastructure/Repositories/RefreshTokenRepository.cs#L22-L27)
```csharp
public async Task<RefreshToken?> GetByTokenAsync(string token)
{
    return await _context.RefreshTokens
        .Include(rt => rt.User)
        .FirstOrDefaultAsync(rt => rt.Token == token);
}
```

---

## 4. POST /api/Auth/logout (Çıkış Yapma)

### Part 1: Adım Adım Mimari İş Akışı

1. **İstek & Validation:**
   * İstemci oturumu kapatmak için `POST /api/Auth/logout` adresine mevcut `refreshToken` bilgisini gönderir.
2. **Sessiz İptal İşlemi (`AuthService.LogoutAsync`):**
   * `_refreshTokenRepository.GetByTokenAsync(request.RefreshToken)` ile token aranır.
   * Güvenlik tasarımı gereği: Token veritabanında yoksa veya silinmişse, saldırgana token varlığı bilgisi sızdırmamak adına işlem sessizce başarıyla sonlandırılır.
   * Token bulunursa `storedToken.IsRevoked = true` yapılır ve `_refreshTokenRepository.SaveChangesAsync()` çağrılır.
3. **HTTP Yanıtı:**
   * Controller `204 No Content` döner (İşlem başarılı, gövdede veri yok).

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 4.1 `AuthController.cs` -> `Logout` Action
[AuthController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/AuthController.cs#L43-L48)
```csharp
[HttpPost("logout")]
public async Task<IActionResult> Logout(RefreshTokenRequest request)
{
    await _authService.LogoutAsync(request);
    return NoContent(); // 204 — başarılı ama dönecek içerik yok
}
```

#### 4.2 `AuthService.cs` -> `LogoutAsync`
[AuthService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/AuthService.cs#L106-L119)
```csharp
public async Task LogoutAsync(RefreshTokenRequest request)
{
    var storedToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

    if (storedToken is null)
    {
        // Token zaten yoksa veya daha önce silinmişse, sessizce başarı say
        return;
    }

    storedToken.IsRevoked = true;
    await _refreshTokenRepository.SaveChangesAsync();
}
```

---

## 5. POST /api/Auth/forgot-password (Şifremi Unuttum)

### Part 1: Adım Adım Mimari İş Akışı

1. **İstek & Validation:**
   * İstemci `POST /api/Auth/forgot-password` adresine `email` gönderir.
   * `ForgotPasswordRequestValidator` ile e-posta formatı doğrulanır.
2. **Kullanıcı Kontrolü & Reset Token Oluşturma (`AuthService.ForgotPasswordAsync`):**
   * `_userRepository.GetByEmailAsync(request.Email)` ile kullanıcı kontrol edilir.
   * Kullanıcı yoksa **BR (T1.4.5)** kuralı gereği e-posta gönderilmez ve sessizce çıkılır (Controller her durumda aynı başarı mesajını döner ki e-posta varlığı sızdırılmasın).
   * Kullanıcı varsa `_jwtTokenGenerator.GeneratePasswordResetToken()` ile 60 dakika geçerli rastgele sıfırlama token'ı üretilir.
   * `PasswordResetToken` entity'si veritabanına eklenir.
3. **E-posta Gönderimi:**
   * HTML şifre sıfırlama bağlantısı (`http://localhost:5240/api/Auth/reset-password?token=...`) hazırlanır ve `_emailSender.SendEmailAsync` ile gönderilir.
4. **HTTP Yanıtı:**
   * `200 OK` ve `{ message = "Eğer bu e-posta adresi kayıtlıysa, şifre sıfırlama bağlantısı gönderildi." }` döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 5.1 `ForgotPasswordRequest.cs` & `ForgotPasswordRequestValidator.cs`
[ForgotPasswordRequestValidator.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Validators/ForgotPasswordRequestValidator.cs)
```csharp
public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
            .MaximumLength(256).WithMessage("E-posta adresi en fazla 256 karakter olabilir.");
    }
}
```

#### 5.2 `AuthController.cs` -> `ForgotPassword` Action
[AuthController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/AuthController.cs#L50-L55)
```csharp
[HttpPost("forgot-password")]
public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
{
    await _authService.ForgotPasswordAsync(request);
    return Ok(new { message = "Eğer bu e-posta adresi kayıtlıysa, şifre sıfırlama bağlantısı gönderildi." });
}
```

#### 5.3 `AuthService.cs` -> `ForgotPasswordAsync`
[AuthService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/AuthService.cs#L121-L161)
```csharp
public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
{
    var user = await _userRepository.GetByEmailAsync(request.Email);

    if (user is null)
    {
        return; // BR (T1.4.5): kullanıcı yoksa sessizce çık
    }

    var (token, expiresAt) = _jwtTokenGenerator.GeneratePasswordResetToken();

    var resetToken = new PasswordResetToken
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        Token = token,
        ExpiresAt = expiresAt,
        IsUsed = false,
        CreatedAt = DateTime.UtcNow
    };

    await _passwordResetTokenRepository.AddAsync(resetToken);
    await _passwordResetTokenRepository.SaveChangesAsync();

    var resetLink = $"http://localhost:5240/api/Auth/reset-password?token={Uri.EscapeDataString(token)}";

    var htmlBody = $"""
    <p>Merhaba,</p>
    <p>Şifreni sıfırlamak için aşağıdaki linke tıkla:</p>
    <p><a href="{resetLink}">Şifremi Sıfırla</a></p>
    <p>Bu link 60 dakika geçerlidir.</p>
    """;

    await _emailSender.SendEmailAsync(user.Email, "TodoApp - Şifre Sıfırlama", htmlBody);
}
```

---

## 6. GET /api/Auth/reset-password (Şifre Sıfırlama HTML Sayfası)

### Part 1: Adım Adım Mimari İş Akışı

1. **İstek:**
   * Kullanıcı e-postasına gelen linke tıklar (`GET /api/Auth/reset-password?token=...`).
2. **HTML Formu Sunumu:**
   * Query String içerisinden `token` parametresi okunur.
   * `WebUtility.HtmlEncode(token)` yapılarak XSS saldırılarına karşı temizlenir.
   * Şifre sıfırlama HTML formu oluşturulur (Gizli `Token` input'u ve `NewPassword` password input'u içerir).
3. **HTTP Yanıtı:**
   * `Content(html, "text/html")` ile HTML sayfası olarak `200 OK` döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 6.1 `AuthController.cs` -> `ResetPasswordPage` Action
[AuthController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/AuthController.cs#L57-L97)
```csharp
[HttpGet("reset-password")]
public IActionResult ResetPasswordPage([FromQuery] string token)
{
    var encodedToken = System.Net.WebUtility.HtmlEncode(token ?? string.Empty);

    var html = $"""
    <!DOCTYPE html>
    <html lang="tr">
    <head>
        <meta charset="UTF-8">
        <title>Şifre Sıfırla</title>
    </head>
    <body>
        <h2>Şifre Sıfırla</h2>
        <form method="post" action="/api/Auth/reset-password">
            <input type="hidden" name="Token" value="{encodedToken}" />
            <label>Yeni Şifre:</label><br />
            <input type="password" name="NewPassword" required /><br /><br />
            <button type="submit">Şifreyi Değiştir</button>
        </form>
    </body>
    </html>
    """;

    return Content(html, "text/html");
}
```

---

## 7. POST /api/Auth/reset-password (Şifre Sıfırlama İsteğini İşleme)

### Part 1: Adım Adım Mimari İş Akışı

1. **İstek & Validation:**
   * Kullanıcı HTML formunu doldurup Gönder'e basar (`POST /api/Auth/reset-password` - Form Data).
   * `ResetPasswordRequestValidator` ile token ve yeni şifre karmaşıklık kuralları kontrol edilir.
2. **Sıfırlama İşlemi (`AuthService.ResetPasswordAsync`):**
   * `_passwordResetTokenRepository.GetByTokenAsync(request.Token)` çağrılarak token sorgulanır.
   * Token yoksa **VEYA** `!storedToken.IsActive` (kullanılmışsa veya süresi dolmuşsa) `throw new ValidationException("Geçersiz veya süresi dolmuş sıfırlama bağlantısı.");` fırlatılır.
   * Yeni şifre BCrypt ile hashlenip kullanıcının `PasswordHash` alanına yazılır.
   * Token kullanıldı olarak işaretlenir (`storedToken.IsUsed = true`).
   * `SaveChangesAsync()` ile değişiklikler kaydedilir.
3. **HTTP Yanıtı:**
   * `Content("<h2>Şifreniz başarıyla değiştirildi.</h2>", "text/html")` HTML yanıtı döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 7.1 `ResetPasswordRequest.cs` & `ResetPasswordRequestValidator.cs`
[ResetPasswordRequestValidator.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Validators/ResetPasswordRequestValidator.cs)
```csharp
public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Token zorunludur.");
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Yeni şifre zorunludur.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.")
            .MaximumLength(128).WithMessage("Şifre en fazla 128 karakter olabilir.")
            .Matches(@"[A-Z]").WithMessage("Şifre en az bir büyük harf içermelidir.")
            .Matches(@"[a-z]").WithMessage("Şifre en az bir küçük harf içermelidir.")
            .Matches(@"[0-9]").WithMessage("Şifre en az bir rakam içermelidir.")
            .Matches(@"[!@#$%^&*(),.?"":{}|<>]").WithMessage("Şifre en az bir özel karakter içermelidir.");
    }
}
```

#### 7.2 `AuthController.cs` -> `ResetPassword` Action
[AuthController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/AuthController.cs#L99-L108)
```csharp
[HttpPost("reset-password")]
public async Task<IActionResult> ResetPassword([FromForm] ResetPasswordRequest request)
{
    await _authService.ResetPasswordAsync(request);
    return Content("<h2>Şifreniz başarıyla değiştirildi.</h2>", "text/html");
}
```

#### 7.3 `AuthService.cs` -> `ResetPasswordAsync`
[AuthService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/AuthService.cs#L163-L176)
```csharp
public async Task ResetPasswordAsync(ResetPasswordRequest request)
{
    var storedToken = await _passwordResetTokenRepository.GetByTokenAsync(request.Token);

    if (storedToken is null || !storedToken.IsActive)
    {
        throw new ValidationException("Geçersiz veya süresi dolmuş sıfırlama bağlantısı.");
    }

    storedToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
    storedToken.IsUsed = true;

    await _passwordResetTokenRepository.SaveChangesAsync();
}
```

---

## 8. PUT /api/Auth/change-password (Oturum Açmış Kullanıcı Şifre Değiştirme)

### Part 1: Adım Adım Mimari İş Akışı

1. **Authentication & Validation:**
   * İstek `[Authorize]` özniteliği ile korunur (`Authorization: Bearer <JWT_TOKEN>`).
   * Token içerisinden `ClaimTypes.NameIdentifier` okunarak kullanıcı `userId`'si elde edilir.
   * `ChangePasswordRequestValidator` ile mevcut şifre, yeni şifre formatı ve *yeni şifrenin mevcut şifre ile aynı olmaması* kuralı doğrulanır.
2. **Şifre Kontrolü & Güvenlik Temizliği (`AuthService.ChangePasswordAsync`):**
   * `_userRepository.GetByIdAsync(userId)` ile kullanıcı (ve kullanıcının aktif `RefreshTokens` listesi) çekilir.
   * `BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash)` kontrol edilir. Yanlışsa `ValidationException("Mevcut şifre hatalı.")` fırlatılır.
   * Yeni şifre hashlenerek kaydedilir (`user.PasswordHash = BCrypt.HashPassword(...)`).
   * **Güvenlik Kuralı:** Şifre değiştiğinde kullanıcının diğer tüm cihazlardaki oturumlarını sonlandırmak için `user.RefreshTokens` içerisindeki tüm token'lar `IsRevoked = true` yapılır.
   * `_userRepository.SaveChangesAsync()` ile kaydedilir.
3. **HTTP Yanıtı:**
   * Controller `204 No Content` döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 8.1 `ChangePasswordRequest.cs` & `ChangePasswordRequestValidator.cs`
[ChangePasswordRequestValidator.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Validators/ChangePasswordRequestValidator.cs)
```csharp
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Mevcut şifre zorunludur.")
            .MaximumLength(128).WithMessage("Mevcut şifre en fazla 128 karakter olabilir.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Yeni şifre zorunludur.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.")
            .MaximumLength(128).WithMessage("Şifre en fazla 128 karakter olabilir.")
            .Matches(@"[A-Z]").WithMessage("Şifre en az bir büyük harf içermelidir.")
            .Matches(@"[a-z]").WithMessage("Şifre en az bir küçük harf içermelidir.")
            .Matches(@"[0-9]").WithMessage("Şifre en az bir rakam içermelidir.")
            .Matches(@"[!@#$%^&*(),.?"":{}|<>]").WithMessage("Şifre en az bir özel karakter içermelidir.")
            .NotEqual(x => x.CurrentPassword).WithMessage("Yeni şifre mevcut şifreyle aynı olamaz.");
    }
}
```

#### 8.2 `AuthController.cs` -> `ChangePassword` Action
[AuthController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/AuthController.cs#L110-L128)
```csharp
[Authorize]
[HttpPut("change-password")]
public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
{
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    if (!Guid.TryParse(userIdClaim, out var userId))
    {
        return Unauthorized();
    }

    await _authService.ChangePasswordAsync(userId, request);
    return NoContent();
}
```

#### 8.3 `AuthService.cs` -> `ChangePasswordAsync`
[AuthService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/AuthService.cs#L178-L208)
```csharp
public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
{
    var user = await _userRepository.GetByIdAsync(userId);

    if (user is null)
    {
        throw new ValidationException("Kullanıcı bulunamadı.");
    }

    var isPasswordCorrect = BCrypt.Net.BCrypt.Verify(
        request.CurrentPassword,
        user.PasswordHash);

    if (!isPasswordCorrect)
    {
        throw new ValidationException("Mevcut şifre hatalı.");
    }

    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

    // Kullanıcının tüm refresh tokenlarını geçersiz hale getir
    foreach (var refreshToken in user.RefreshTokens)
    {
        refreshToken.IsRevoked = true;
    }

    await _userRepository.SaveChangesAsync();
}
```

---

# BÖLÜM 2: TodoItemsController Endpoints

## 9. POST /api/TodoItems (Yeni Görev Oluşturma)

### Part 1: Adım Adım Mimari İş Akışı

1. **Authentication & Identity:**
   * İstek `Authorization: Bearer <JWT_TOKEN>` header'ı ile gelir.
   * `[Authorize]` özniteliği JWT token'ı doğrular. `GetCurrentUserId()` metodu token içerisinden `userId` bilgisini çıkarır.
2. **DTO Binding & Validation:**
   * İstek gövdesi `CreateTodoItemRequest` nesnesine bind edilir.
   * `CreateTodoItemRequestValidator` çalışır:
     * `Title`: Boş olamaz, maks 200 karakter.
     * `Description`: Maks 2000 karakter.
3. **Controller & Service Çağrısı:**
   * `TodoItemsController.Create` metodu `_todoItemService.CreateAsync(userId, request)` metodunu çağırır.
4. **İş Mantığı ve Entity Oluşturma (`TodoItemService`):**
   * **BR-006:** `OwnerId = userId` olarak set edilir (Görev sahibi zorunludur).
   * Varsayılan durum `Status = TodoItemStatus.Open` ve `CreatedAt = DateTime.UtcNow` atanır.
   * `_todoItemRepository.AddAsync(todoItem)` ve `SaveChangesAsync()` çalıştırılır.
   * `MapToResponse` static helper metodu ile `TodoItemResponse` DTO'su oluşturulur.
5. **HTTP Yanıtı:**
   * Controller `CreatedAtAction` ile **`210 Created`** statü kodu, yeni görevin detay adresi (`Location` header'ı) ve `TodoItemResponse` nesnesini döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 9.1 `CreateTodoItemRequest.cs` (DTO)
[CreateTodoItemRequest.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/DTOs/CreateTodoItemRequest.cs)
```csharp
namespace TodoApp.Application.DTOs;

public class CreateTodoItemRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
}
```

#### 9.2 `CreateTodoItemRequestValidator.cs` (FluentValidation)
[CreateTodoItemRequestValidator.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Validators/CreateTodoItemRequestValidator.cs)
```csharp
using FluentValidation;
using TodoApp.Application.DTOs;

namespace TodoApp.Application.Validators;

public class CreateTodoItemRequestValidator : AbstractValidator<CreateTodoItemRequest>
{
    public CreateTodoItemRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Görev başlığı zorunludur.")
            .MaximumLength(200).WithMessage("Görev başlığı en fazla 200 karakter olabilir.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Görev açıklaması en fazla 2000 karakter olabilir.");
    }
}
```

#### 9.3 `TodoItemsController.cs` -> `Create` Action
[TodoItemsController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/TodoItemsController.cs#L21-L27)
```csharp
[Authorize]
[HttpPost]
public async Task<IActionResult> Create(CreateTodoItemRequest request)
{
    var userId = GetCurrentUserId();
    var result = await _todoItemService.CreateAsync(userId, request);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}
```

#### 9.4 `TodoItemService.cs` -> `CreateAsync`
[TodoItemService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TodoItemService.cs#L21-L38)
```csharp
public async Task<TodoItemResponse> CreateAsync(Guid userId, CreateTodoItemRequest request)
{
    var todoItem = new TodoItem
    {
        Id = Guid.NewGuid(),
        OwnerId = userId,           // BR-006: owner NOT NULL
        Title = request.Title,
        Description = request.Description,
        DueDate = request.DueDate,
        Status = TodoItemStatus.Open,
        CreatedAt = DateTime.UtcNow
    };

    await _todoItemRepository.AddAsync(todoItem);
    await _todoItemRepository.SaveChangesAsync();

    return MapToResponse(todoItem, userId);
}
```

#### 9.5 `TodoItemRepository.cs` -> `AddAsync`
[TodoItemRepository.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Infrastructure/Repositories/TodoItemRepository.cs#L71-L85)
```csharp
public async Task AddAsync(TodoItem todoItem)
{
    await _context.TodoItems.AddAsync(todoItem);
}

public async Task SaveChangesAsync()
{
    await _context.SaveChangesAsync();
}
```

---

## 10. GET /api/TodoItems (Erişilebilir Görevleri Listeleme - Sayfalamalı)

### Part 1: Adım Adım Mimari İş Akışı

1. **Authentication & Query Binding:**
   * Token'dan `userId` elde edilir. Query string'den `PaginatedRequest` (`Page`, `PageSize`) okunur.
2. **Sayfalama Sınırlandırması (`TodoItemService.GetAllAsync`):**
   * `Page` minimum 1, `PageSize` 1 ile 100 (`MaxPageSize`) arasında `Math.Clamp` ile sınırlandırılır.
3. **Erişim Kontrolü ve DB Sorgusu (`TodoItemRepository.GetAccessibleByUserAsync`):**
   * **BR-011 (Soft Delete):** `!t.IsDeleted` şartı eklenir (Silinmiş görevler listede görünmez).
   * **BR-020 / BR-028 (Erişim Yetkisi):** Görevin sahibi oturum açan kullanıcı mı (`OwnerId == userId`) **VEYA** görev bu kullanıcıyla paylaşılmış mı (`TaskShares.Any(ts => ts.UserId == userId)`) kontrol edilir.
   * Görevler `CreatedAt` tarihine göre azalan sıralanır, `Skip` ve `Take` ile sayfalanır. Tag'ler ve Paylaşım bilgileri `Include` ile çekilir.
4. **HTTP Yanıtı:**
   * `200 OK` statü kodu ile `PaginatedResponse<TodoItemResponse>` (sayfa detayları, toplam kayıt sayısı ve DTO listesi) döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 10.1 `PaginatedRequest.cs`
[PaginatedRequest.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/DTOs/PaginatedRequest.cs)
```csharp
namespace TodoApp.Application.DTOs;

public class PaginatedRequest
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;
}
```

#### 10.2 `TodoItemsController.cs` -> `GetAll` Action
[TodoItemsController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/TodoItemsController.cs#L29-L35)
```csharp
[HttpGet]
public async Task<IActionResult> GetAll([FromQuery] PaginatedRequest request)
{
    var userId = GetCurrentUserId();
    var result = await _todoItemService.GetAllAsync(userId, request);
    return Ok(result);
}
```

#### 10.3 `TodoItemService.cs` -> `GetAllAsync`
[TodoItemService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TodoItemService.cs#L46-L56)
```csharp
public async Task<PaginatedResponse<TodoItemResponse>> GetAllAsync(Guid userId, PaginatedRequest request)
{
    var page = Math.Max(1, request.Page);
    var pageSize = Math.Clamp(request.PageSize, 1, PaginatedRequest.MaxPageSize);

    var (items, totalCount) = await _todoItemRepository.GetAccessibleByUserAsync(userId, page, pageSize);
    var mappedItems = items.Select(item => MapToResponse(item, userId)).ToList();

    return new PaginatedResponse<TodoItemResponse>(mappedItems, totalCount, page, pageSize);
}
```

#### 10.4 `TodoItemRepository.cs` -> `GetAccessibleByUserAsync`
[TodoItemRepository.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Infrastructure/Repositories/TodoItemRepository.cs#L31-L49)
```csharp
public async Task<(List<TodoItem> Items, int TotalCount)> GetAccessibleByUserAsync(Guid userId, int page, int pageSize)
{
    var query = _context.TodoItems
        .Where(t => (t.OwnerId == userId || t.TaskShares.Any(ts => ts.UserId == userId)) && !t.IsDeleted);

    var totalCount = await query.CountAsync();

    var items = await query
        .Include(t => t.TodoItemTags)
            .ThenInclude(tit => tit.Tag)
        .Include(t => t.TaskShares)
            .ThenInclude(ts => ts.User)
        .OrderByDescending(t => t.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return (items, totalCount);
}
```

---

## 11. GET /api/TodoItems/{id} (Tekil Görev Detayı Getirme)

### Part 1: Adım Adım Mimari İş Akışı

1. **Authentication & Controller:**
   * Route'dan `id` (`Guid`), Token'dan `userId` okunur.
2. **Yetkilendirme ve Varlık Sızdırmama Kontrolü (`TaskAuthorizationService.EnsureCanReadAsync`):**
   * Görev DB'de aratılır (`GetByIdAsync`). SubTasks, Tags ve TaskShares dahil çekilir.
   * Görev yoksa -> `404 NotFoundException` fırlatılır.
   * **BR-029 (Varlık Sızdırmama):** Kullanıcı görev sahibi veya paylaşılan kişi değilse, yetkisiz olduğunu belli etmemek için `403 Forbidden` yerine **`404 NotFound`** fırlatılır.
   * **BR-011:** Görev soft-delete yapılmışsa (`IsDeleted == true`) yine `404 NotFound` fırlatılır.
3. **HTTP Yanıtı:**
   * Yetkili istek için `200 OK` ve alt görevler, etiketler ile birlikte `TodoItemResponse` DTO döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 11.1 `TodoItemsController.cs` -> `GetById` Action
[TodoItemsController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/TodoItemsController.cs#L37-L43)
```csharp
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetById(Guid id)
{
    var userId = GetCurrentUserId();
    var result = await _todoItemService.GetByIdAsync(userId, id);
    return Ok(result);
}
```

#### 11.2 `TodoItemService.cs` -> `GetByIdAsync`
[TodoItemService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TodoItemService.cs#L40-L44)
```csharp
public async Task<TodoItemResponse> GetByIdAsync(Guid userId, Guid todoItemId)
{
    var todoItem = await _taskAuthorizationService.EnsureCanReadAsync(todoItemId, userId);
    return MapToResponse(todoItem, userId);
}
```

#### 11.3 `TaskAuthorizationService.cs` -> `EnsureCanReadAsync`
[TaskAuthorizationService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TaskAuthorizationService.cs#L20-L49)
```csharp
public async Task<TodoItem> EnsureCanReadAsync(Guid taskId, Guid userId, bool allowTrash = false)
{
    var task = await _todoItemRepository.GetByIdAsync(taskId);

    if (task is null)
    {
        throw new NotFoundException("Görev bulunamadı.");
    }

    var isOwner = task.OwnerId == userId;
    var isShared = task.TaskShares != null && task.TaskShares.Any(ts => ts.UserId == userId);

    // BR-029: Yetkisiz kullanıcıya 404 (varlık sızdırmama)
    if (!isOwner && !isShared)
    {
        throw new NotFoundException("Görev bulunamadı.");
    }

    // BR-011: Soft-delete edilmiş görev aktif detayda görünmez
    if (task.IsDeleted)
    {
        if (!allowTrash || !isOwner)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }
    }

    return task;
}
```

---

## 12. PUT /api/TodoItems/{id} (Görev Güncelleme)

### Part 1: Adım Adım Mimari İş Akışı

1. **Authentication & Validation:**
   * `UpdateTodoItemRequestValidator` ile başlık zorunluluğu ve uzunluk kuralları kontrol edilir.
2. **Güncelleme Yetkisi Kontrolü (`TaskAuthorizationService.EnsureCanModifyAsync`):**
   * **BR-025:** Görevi hem sahibi hem de kendisiyle paylaşılan kullanıcılar güncelleyebilir.
   * Silinmiş bir görev güncellenemez (`404 NotFound`).
3. **Entity Güncelleme & DB Kaydı (`TodoItemService.UpdateAsync`):**
   * Entity nesnesinin `Title`, `Description`, `DueDate` alanları güncellenir.
   * `_todoItemRepository.SaveChangesAsync()` ile kaydedilir.
4. **HTTP Yanıtı:**
   * `200 OK` ve güncellenmiş `TodoItemResponse` döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 12.1 `UpdateTodoItemRequest.cs` & `UpdateTodoItemRequestValidator.cs`
[UpdateTodoItemRequestValidator.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Validators/UpdateTodoItemRequestValidator.cs)
```csharp
public class UpdateTodoItemRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
}

public class UpdateTodoItemRequestValidator : AbstractValidator<UpdateTodoItemRequest>
{
    public UpdateTodoItemRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Görev başlığı zorunludur.")
            .MaximumLength(200).WithMessage("Görev başlığı en fazla 200 karakter olabilir.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Görev açıklaması en fazla 2000 karakter olabilir.");
    }
}
```

#### 12.2 `TodoItemsController.cs` -> `Update` Action
[TodoItemsController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/TodoItemsController.cs#L45-L51)
```csharp
[HttpPut("{id:guid}")]
public async Task<IActionResult> Update(Guid id, UpdateTodoItemRequest request)
{
    var userId = GetCurrentUserId();
    var result = await _todoItemService.UpdateAsync(userId, id, request);
    return Ok(result);
}
```

#### 12.3 `TodoItemService.cs` -> `UpdateAsync`
[TodoItemService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TodoItemService.cs#L58-L70)
```csharp
public async Task<TodoItemResponse> UpdateAsync(Guid userId, Guid todoItemId, UpdateTodoItemRequest request)
{
    var todoItem = await _taskAuthorizationService.EnsureCanModifyAsync(todoItemId, userId);

    todoItem.Title = request.Title;
    todoItem.Description = request.Description;
    todoItem.DueDate = request.DueDate;

    await _todoItemRepository.SaveChangesAsync();

    return MapToResponse(todoItem, userId);
}
```

---

## 13. PATCH /api/TodoItems/{id}/complete (Görevi Tamamlama)

### Part 1: Adım Adım Mimari İş Akışı

1. **Authentication & Authorization:**
   * `TaskAuthorizationService.EnsureCanCompleteAsync` ile kullanıcının görevi tamamlamaya yetkisi (Sahip veya Paylaşılan) kontrol edilir.
2. **Tamamlama Mantığı (`TodoItemService.CompleteAsync`):**
   * `Status = TodoItemStatus.Completed` yapılır.
   * **BR-015:** Görevi tamamlama izi olarak `CompletedByUserId = userId` ve `CompletedAt = DateTime.UtcNow` atanır (Görevin paylaşımı kalksa bile bu iz korunur).
   * `SaveChangesAsync()` çağrılır.
3. **HTTP Yanıtı:**
   * `200 OK` ve güncellenmiş DTO döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 13.1 `TodoItemsController.cs` -> `Complete` Action
[TodoItemsController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/TodoItemsController.cs#L53-L59)
```csharp
[HttpPatch("{id:guid}/complete")]
public async Task<IActionResult> Complete(Guid id)
{
    var userId = GetCurrentUserId();
    var result = await _todoItemService.CompleteAsync(userId, id);
    return Ok(result);
}
```

#### 13.2 `TodoItemService.cs` -> `CompleteAsync`
[TodoItemService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TodoItemService.cs#L72-L84)
```csharp
public async Task<TodoItemResponse> CompleteAsync(Guid userId, Guid todoItemId)
{
    var todoItem = await _taskAuthorizationService.EnsureCanCompleteAsync(todoItemId, userId);

    todoItem.Status = TodoItemStatus.Completed;
    todoItem.CompletedByUserId = userId;
    todoItem.CompletedAt = DateTime.UtcNow;

    await _todoItemRepository.SaveChangesAsync();

    return MapToResponse(todoItem, userId);
}
```

---

## 14. DELETE /api/TodoItems/{id} (Görevi Çöp Kutusuna Taşıma - Soft Delete)

### Part 1: Adım Adım Mimari İş Akışı

1. **Authentication & Sahiplik Yetkilendirmesi:**
   * `TaskAuthorizationService.EnsureCanDeleteAsync` çağrılır.
   * **BR-008, BR-026, BR-029:** YALNIZCA görevin sahibi silebilir! Kendisine görev paylaşılan kullanıcı bu endpoint'i çağırırsa `404 NotFound` alır.
2. **Soft Delete İşlemi (`TodoItemService.DeleteAsync`):**
   * **BR-008:** Görev veritabanından silinmez. Çöp kutusuna aktarılmak üzere:
     * `IsDeleted = true`
     * `DeletedByUserId = userId`
     * `DeletedAt = DateTime.UtcNow`
   * `SaveChangesAsync()` çağrılır.
3. **HTTP Yanıtı:**
   * `204 No Content` döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 14.1 `TodoItemsController.cs` -> `Delete` Action
[TodoItemsController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/TodoItemsController.cs#L61-L67)
```csharp
[HttpDelete("{id:guid}")]
public async Task<IActionResult> Delete(Guid id)
{
    var userId = GetCurrentUserId();
    await _todoItemService.DeleteAsync(userId, id);
    return NoContent();
}
```

#### 14.2 `TodoItemService.cs` -> `DeleteAsync`
[TodoItemService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TodoItemService.cs#L86-L97)
```csharp
public async Task DeleteAsync(Guid userId, Guid todoItemId)
{
    var todoItem = await _taskAuthorizationService.EnsureCanDeleteAsync(todoItemId, userId);

    todoItem.IsDeleted = true;
    todoItem.DeletedByUserId = userId;
    todoItem.DeletedAt = DateTime.UtcNow;

    await _todoItemRepository.SaveChangesAsync();
}
```

#### 14.3 `TaskAuthorizationService.cs` -> `EnsureCanDeleteAsync`
[TaskAuthorizationService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TaskAuthorizationService.cs#L63-L74)
```csharp
public async Task<TodoItem> EnsureCanDeleteAsync(Guid taskId, Guid userId)
{
    var task = await _todoItemRepository.GetByIdAsync(taskId);

    if (task is null || task.OwnerId != userId || task.IsDeleted)
    {
        throw new NotFoundException("Görev bulunamadı.");
    }

    return task;
}
```

---

## 15. DELETE /api/TodoItems/{id}/permanent (Kalıcı Silme - Hard Delete)

### Part 1: Adım Adım Mimari İş Akışı

1. **Authentication & Sahiplik Kontrolü:**
   * `TaskAuthorizationService.EnsureOwnerAsync` çağrılarak kullanıcının görevin mutlak sahibi olduğu doğrulanır.
2. **Çöp Kutusu Durumu Kontrolü (`TodoItemService.PermanentDeleteAsync`):**
   * **BR-010:** Görev çöp kutusunda (`IsDeleted == true`) değilse kalıcı olarak silinemez. `throw new ValidationException("Yalnızca çöp kutusundaki görevler kalıcı olarak silinebilir.");` fırlatılır (`400 Bad Request`).
3. **Hard Delete İşlemi:**
   * `_todoItemRepository.Delete(todoItem)` (`DbSet.Remove`) çalıştırılır. İlişkili alt görevler ve etiket bağları veritabanından kalıcı olarak silinir.
   * `SaveChangesAsync()` çağrılır.
4. **HTTP Yanıtı:**
   * `204 No Content` döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 15.1 `TodoItemsController.cs` -> `PermanentDelete` Action
[TodoItemsController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/TodoItemsController.cs#L69-L75)
```csharp
[HttpDelete("{id:guid}/permanent")]
public async Task<IActionResult> PermanentDelete(Guid id)
{
    var userId = GetCurrentUserId();
    await _todoItemService.PermanentDeleteAsync(userId, id);
    return NoContent();
}
```

#### 15.2 `TodoItemService.cs` -> `PermanentDeleteAsync`
[TodoItemService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TodoItemService.cs#L99-L111)
```csharp
public async Task PermanentDeleteAsync(Guid userId, Guid todoItemId)
{
    var todoItem = await _taskAuthorizationService.EnsureOwnerAsync(todoItemId, userId);

    if (!todoItem.IsDeleted)
    {
        throw new ValidationException("Yalnızca çöp kutusundaki görevler kalıcı olarak silinebilir.");
    }

    _todoItemRepository.Delete(todoItem);
    await _todoItemRepository.SaveChangesAsync();
}
```

---

## 16. POST /api/TodoItems/{id}/restore (Çöp Kutusundan Geri Yükleme)

### Part 1: Adım Adım Mimari İş Akışı

1. **Authentication & Sahiplik Kontrolü:**
   * `TaskAuthorizationService.EnsureOwnerAsync` ile kullanıcının görevin sahibi olduğu doğrulanır.
2. **Çöp Kutusu Kontrolü & Restore Mantığı (`TodoItemService.RestoreAsync`):**
   * Görev zaten aktifse (`!IsDeleted`) `ValidationException("Bu görev zaten aktif durumda.")` fırlatılır.
   * Görev soft delete durumunda ise `IsDeleted = false`, `DeletedByUserId = null`, `DeletedAt = null` yapılır.
   * `SaveChangesAsync()` çağrılır.
3. **HTTP Yanıtı:**
   * `200 OK` ve restore edilen `TodoItemResponse` DTO döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 16.1 `TodoItemsController.cs` -> `Restore` Action
[TodoItemsController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/TodoItemsController.cs#L77-L83)
```csharp
[HttpPost("{id:guid}/restore")]
public async Task<IActionResult> Restore(Guid id)
{
    var userId = GetCurrentUserId();
    var result = await _todoItemService.RestoreAsync(userId, id);
    return Ok(result);
}
```

#### 16.2 `TodoItemService.cs` -> `RestoreAsync`
[TodoItemService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TodoItemService.cs#L113-L130)
```csharp
public async Task<TodoItemResponse> RestoreAsync(Guid userId, Guid todoItemId)
{
    var todoItem = await _taskAuthorizationService.EnsureOwnerAsync(todoItemId, userId);

    if (!todoItem.IsDeleted)
    {
        throw new ValidationException("Bu görev zaten aktif durumda.");
    }

    todoItem.IsDeleted = false;
    todoItem.DeletedByUserId = null;
    todoItem.DeletedAt = null;

    await _todoItemRepository.SaveChangesAsync();

    return MapToResponse(todoItem, userId);
}
```

---

## 17. GET /api/TodoItems/trash (Çöp Kutusundaki Görevleri Listeleme)

### Part 1: Adım Adım Mimari İş Akışı

1. **Authentication & Query Binding:**
   * Token'dan `userId`, Query'den `PaginatedRequest` alınır.
2. **Çöp Kutusu Sorgusu (`TodoItemRepository.GetDeletedByOwnerAsync`):**
   * Sadece oturum açan kullanıcının sahibi olduğu (`OwnerId == userId`) ve silinmiş olan (`IsDeleted == true`) görevler getirilir.
   * `DeletedAt` tarihine göre azalan sırada sıralanır.
3. **HTTP Yanıtı:**
   * `200 OK` ve `PaginatedResponse<TodoItemResponse>` nesnesi döner.

---

### Part 2: İşlem Sırasına Göre Kodlar

#### 17.1 `TodoItemsController.cs` -> `GetTrash` Action
[TodoItemsController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/TodoItemsController.cs#L85-L91)
```csharp
[HttpGet("trash")]
public async Task<IActionResult> GetTrash([FromQuery] PaginatedRequest request)
{
    var userId = GetCurrentUserId();
    var result = await _todoItemService.GetTrashAsync(userId, request);
    return Ok(result);
}
```

#### 17.2 `TodoItemService.cs` -> `GetTrashAsync`
[TodoItemService.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TodoItemService.cs#L132-L141)
```csharp
public async Task<PaginatedResponse<TodoItemResponse>> GetTrashAsync(Guid userId, PaginatedRequest request)
{
    var page = Math.Max(1, request.Page);
    var pageSize = Math.Clamp(request.PageSize, 1, PaginatedRequest.MaxPageSize);

    var (items, totalCount) = await _todoItemRepository.GetDeletedByOwnerAsync(userId, page, pageSize);
    var mappedItems = items.Select(item => MapToResponse(item, userId)).ToList();

    return new PaginatedResponse<TodoItemResponse>(mappedItems, totalCount, page, pageSize);
}
```

#### 17.3 `TodoItemRepository.cs` -> `GetDeletedByOwnerAsync`
[TodoItemRepository.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Infrastructure/Repositories/TodoItemRepository.cs#L52-L69)
```csharp
public async Task<(List<TodoItem> Items, int TotalCount)> GetDeletedByOwnerAsync(Guid userId, int page, int pageSize)
{
    var query = _context.TodoItems
        .Where(t => t.OwnerId == userId && t.IsDeleted);

    var totalCount = await query.CountAsync();

    var items = await query
        .Include(t => t.SubTasks)
        .Include(t => t.TodoItemTags)
            .ThenInclude(tit => tit.Tag)
        .OrderByDescending(t => t.DeletedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return (items, totalCount);
}
```
