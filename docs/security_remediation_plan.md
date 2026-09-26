# 🛡️ TodoApp — Güvenlik, Hata Düzeltme & Canlıya Dağıtım İyileştirme Planı
### (Security & Production Hardening Guide)

**Doküman Versiyonu:** 1.0  
**Tarih:** 26 Eylül 2026  
**Hedef Kapsam:** Production Deployment, Kimlik Doğrulama/Yetkilendirme Güvenliği, Veri Bütünlüğü ve Kötüye Kullanım Koruması  
**İlgili Katmanlar:** `TodoApp.Api`, `TodoApp.Application`, `TodoApp.Infrastructure`, `TodoApp.Domain`

---

## 📌 İçindekiler
1. [Yönetici Özeti](#1-yönetici-özeti)
2. [Kategori 1: Kritik Güvenlik Zafiyetleri](#2-kategori-1-kritik-güvenlik-zafiyetleri)
   - [2.1. 2FA Akışında Parola Doğrulamasının Atlatılması (Auth Bypass)](#21-2fa-akışında-parola-doğrulamasının-atlatılması-auth-bypass)
   - [2.2. 2FA ve Şifre Sıfırlama Endpoint'lerinde Hız Sınırlaması Eksikliği (Brute-Force)](#22-2fa-ve-şifre-sıfırlama-endpointlerinde-hız-sınırlaması-eksikliği-brute-force)
   - [2.3. Görev ve Liste İlişkisinde Yetki Doğrulama Eksikliği (IDOR)](#23-görev-ve-liste-ilişkisinde-yetki-doğrulama-eksikliği-idor)
3. [Kategori 2: Çalışma Zamanı Hataları & Veritabanı Bütünlüğü](#3-kategori-2-çalışma-zamanı-hataları--veritabanı-bütünlüğü)
   - [3.1. Hesap Silme Sırasında Foreign Key Çökmesi (`ExecuteUpdateAsync` & Soft Delete)](#31-hesap-silme-sırasında-foreign-key-çökmesi-executeupdateasync--soft-delete)
4. [Kategori 3: Canlıya Dağıtım (Deployment) & Konfigürasyon İyileştirmeleri](#4-kategori-3-canlıya-dağıtım-deployment--konfigürasyon-iyileştirmeleri)
   - [4.1. Swagger Arayüzünün Canlı Ortamda 404 Dönmesi](#41-swagger-arayüzünün-canlı-ortamda-404-dönmesi)
   - [4.2. Şifre Sıfırlama E-posta Linkinin Canlıda Localhost Kalması](#42-şifre-sıfırlama-e-posta-linkinin-canlıda-localhost-kalması)
   - [4.3. CORS AllowedOrigins Canlı Domain Tanımı](#43-cors-allowedorigins-canlı-domain-tanımı)
   - [4.4. Çoklu Pod / Scale-Out Ortamında Migration Yarış Durumu (Race Condition)](#44-çoklu-pod--scale-out-ortamında-migration-yarış-durumu-race-condition)
5. [Kategori 4: Kötüye Kullanım Senaryoları & Önleyici Mimari](#5-kategori-4-kötüye-kullanım-senaryoları--önleyici-mimari)
   - [5.1. Kötü Niyetli Hesap Kilitleme Saldırısı (Account Lockout DoS)](#51-kötü-niyetli-hesap-kilitleme-saldırısı-account-lockout-dos)
   - [5.2. E-posta Varlık Tespiti (User Enumeration via Task Share)](#52-e-posta-varlık-tespiti-user-enumeration-via-task-share)
   - [5.3. Süresi Dolan Token Tablolarının Sonsuz Şişmesi (Unbounded Growth)](#53-süresi-dolan-token-tablolarının-sonsuz-şişmesi-unbounded-growth)
6. [Adım Adım Uygulama ve Doğrulama Yol Haritası](#6-adım-adım-uygulama-ve-doğrulama-yol-haritası)

---

## 1. Yönetici Özeti

TodoApp backend altyapısı; Clean Architecture, Global Query Filter, RFC 7807 ProblemDetails, BCrypt şifreleme ve 197 otomatik test ile kurumsal seviyede kurgulanmıştır. 

Ancak uygulamanın gerçek kullanıcılarla ve herkese açık canlı ortamlarda (Azure/Production) kesintisiz, güvenli çalışabilmesi için **kök nedenleri tespit edilen 4 kritik teknik risk** ve **dağıtım yapılandırması** bulunmaktadır:

| Bulgu | Kategori | Risk Seviyesi | Kök Neden Özeti |
|---|---|:---:|---|
| **2FA Parola Atlatma** | Güvenlik | 🔴 **Kritik** | `login-2fa` metodunun parola doğrulaması yapmadan doğrudan `UserId` ve `Code` kabul etmesi. |
| **Hesap Silme SQL Çökmesi** | Hata (Bug) | 🔴 **Yüksek** | Soft-delete görevlerin `ExecuteUpdateAsync` filtresine takılarak `DeletedByUserId`'nin null yapılamaması. |
| **2FA Rate Limit Eksikliği** | Güvenlik | 🟠 **Yüksek** | `login-2fa` üzerinde IP bazlı hız sınırının olmaması (6 haneli kodun taranabilmesi). |
| **TodoList IDOR** | Güvenlik | 🟡 **Orta** | Görev eklerken/güncellerken `TodoListId`'nin çağıran kullanıcıya ait olup olmadığının denetlenmemesi. |
| **Deployment Yapılandırmaları** | Operasyonel | 🟡 **Orta** | Canlıda Swagger'ın kapalı olması, e-postada `localhost` linki gitmesi, CORS canlı domainleri. |

---

## 2. Kategori 1: Kritik Güvenlik Zafiyetleri

---

### 2.1. 2FA Akışında Parola Doğrulamasının Atlatılması (Auth Bypass)
* **Zafiyet Standardı:** CWE-287 (Improper Authentication), CWE-304 (Missing Critical Step in Authentication)
* **İlgili Dosyalar:**
  - [`src/TodoApp.Application/Services/AuthService.cs`](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/AuthService.cs#L349-L367)
  - [`src/TodoApp.Application/DTOs/TwoFactorLoginRequest.cs`](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/DTOs/TwoFactorLoginRequest.cs)
  - [`src/TodoApp.Api/Controllers/AuthController.cs`](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/AuthController.cs#L84-L89)

#### 🧐 Kök Neden (Root Cause)
Mevcut akışta `POST /api/Auth/login` parolayı doğrular ve 2FA açıksa `{ "requiresTwoFactor": true, "userId": "..." }` döner.
İkinci adım olan `POST /api/Auth/login-2fa` metodu ise şu şekildedir:
```csharp
// ❌ MEVCUT GÜVENSİZ KOD:
public async Task<AuthResponse> LoginWithTwoFactorAsync(TwoFactorLoginRequest request)
{
    var user = await _userRepository.GetByIdAsync(request.UserId); // Sadece UserId alıyor!
    // ...
    if (!totp.VerifyTotp(request.Code, ...))
        throw new ValidationException("Geçersiz kod.");

    return await GenerateAuthResponseAsync(user); // Tam yetkili JWT üretiliyor!
}
```
Buradaki kritik eksiklik: Endpoint'in, kullanıcının **parola doğrulamasını başarıyla geçtiğine dair hiçbir kanıt (cryptographic proof)** aramamasıdır. Kullanıcı ID'si (`Guid UserId`) sistemde gizli bir sır değildir; paylaşılan görevlerde, aktivitelerde ve URL'lerde açıkça yer alır.

#### 💥 Olası Saldırı Senaryosu
1. Saldırgan, hedefin `UserId` değerini bir görev paylaşımından veya API yanıtından öğrenir.
2. Saldırgan hedefin parolasını bilmemektedir.
3. Saldırgan doğrudan `POST /api/Auth/login-2fa` adresine `{ "userId": "<hedef_id>", "code": "..." }` gönderir.
4. Hedefin TOTP kodunu tahmin ettiği veya elde ettiği anda parola adımını tamamen baypas ederek hedefin hesabına tam yetkili JWT ile erişir.

#### ✅ Düzeltme Yöntemi
1. `LoginAsync` aşamasında parola doğrulandığında, `UserId` yerine **yalnızca 5 dakika geçerli, özel bir claim (`scope: pre_2fa_verification`) içeren imzalı geçici bir bilet (`TwoFactorToken`)** üretilmelidir.
2. `TwoFactorLoginRequest` nesnesinde `UserId` kaldırılmalı, yerine `TwoFactorToken` konmalıdır.
3. `LoginWithTwoFactorAsync` bu geçici JWT'nin imzasını ve süresini doğrulamalı, içindeki `UserId` claim'ini alarak TOTP kontrolü yapmalıdır.

**Örnek Güvenli Kod:**
```csharp
// 1. DTO
public class TwoFactorLoginRequest
{
    public string TwoFactorToken { get; set; } = string.Empty; // İmzalı geçici bilet
    public string Code { get; set; } = string.Empty;
}

// 2. AuthService.LoginAsync
if (user.TwoFactorEnabled)
{
    var tempToken = _jwtTokenGenerator.GenerateTwoFactorTempToken(user);
    return new AuthResponse
    {
        RequiresTwoFactor = true,
        TwoFactorToken = tempToken // 5 dk geçerli pre-auth token
    };
}

// 3. AuthService.LoginWithTwoFactorAsync
public async Task<AuthResponse> LoginWithTwoFactorAsync(TwoFactorLoginRequest request)
{
    var principal = _jwtTokenGenerator.ValidateTwoFactorTempToken(request.TwoFactorToken);
    if (principal == null)
        throw new ValidationException("Geçersiz veya süresi dolmuş 2FA oturumu. Lütfen tekrar giriş yapın.");

    var userId = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var user = await _userRepository.GetByIdAsync(userId);
    // TOTP kontrolü ve nihai AuthResponse üretimi...
}
```

---

### 2.2. 2FA ve Şifre Sıfırlama Endpoint'lerinde Hız Sınırlaması Eksikliği (Brute-Force)
* **Zafiyet Standardı:** CWE-307 (Improper Restriction of Excessive Authentication Attempts)
* **İlgili Dosyalar:**
  - [`src/TodoApp.Api/Controllers/AuthController.cs`](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/AuthController.cs#L84-L89)
  - [`src/TodoApp.Api/Extensions/RateLimiterExtensions.cs`](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Extensions/RateLimiterExtensions.cs#L78-L81)

#### 🧐 Kök Neden (Root Cause)
`AuthController` içerisindeki `login`, `register` ve `forgot-password` endpoint'lerine özel attribute atanmışken, `POST /api/Auth/login-2fa` ve `POST /api/Auth/reset-password` endpoint'leri korumasız bırakılmıştır.

#### 💥 Olası Saldırı Senaryosu
6 haneli TOTP kodları $10^6$ (1 milyon) olasılıktan ibarettir. Hız limiti olmayan bir endpoint'e çok kanallı (multi-threaded) script ile saniyede yüzlerce istek atılarak geçerli zaman dilimi ($30-60$ saniye) içerisinde kod kaba kuvvetle kırılabilir.

#### ✅ Düzeltme Yöntemi
`RateLimiterExtensions.cs` içerisine IP bazlı yeni politikalar eklenmeli ve controller metodlarına atanmalıdır:
```csharp
// RateLimiterExtensions.cs
options.AddIpPolicy("auth-2fa-verify", permitLimit: 5, windowMinutes: 1);
options.AddIpPolicy("auth-reset-password", permitLimit: 3, windowMinutes: 1);

// AuthController.cs
[EnableRateLimiting("auth-2fa-verify")]
[HttpPost("login-2fa")]
public async Task<IActionResult> LoginWithTwoFactor([FromBody] TwoFactorLoginRequest request) ...

[EnableRateLimiting("auth-reset-password")]
[HttpPost("reset-password")]
public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request) ...
```

---

### 2.3. Görev ve Liste İlişkisinde Yetki Doğrulama Eksikliği (IDOR)
* **Zafiyet Standardı:** CWE-639 (Insecure Direct Object Reference - Inadequate Authorization)
* **İlgili Dosyalar:**
  - [`src/TodoApp.Application/Services/TodoItemService.cs` (`CreateAsync` & `UpdateAsync`)](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TodoItemService.cs#L43)

#### 🧐 Kök Neden (Root Cause)
`TodoItemService.CreateAsync` ve `UpdateAsync` metodları gelen `request.TodoListId` parametresini doğrulamadan doğrudan nesneye atar. İlgili listenin gerçekten isteği atan kullanıcıya (`userId`) ait olup olmadığı ve listenin silinip silinmediği denetlenmez.

#### 💥 Olası Saldırı Senaryosu
Kullanıcı A, Kullanıcı B'nin liste kimliğini (ID) girdiğinde kendi görevini Kullanıcı B'nin listesine ekleyebilir. Bu durum veri izolasyonu ve çoklu kullanıcı gizliliği ihlalidir.

#### ✅ Düzeltme Yöntemi
`TodoItemService` içerisine liste sahipliği doğrulama adımı eklenmelidir:
```csharp
if (request.TodoListId.HasValue)
{
    var list = await _todoListRepository.GetByIdAsync(request.TodoListId.Value);
    if (list == null || list.OwnerId != userId || list.IsDeleted)
    {
        throw new ValidationException("Belirtilen görev listesi bulunamadı veya erişim yetkiniz yok.");
    }
}
```

---

## 3. Kategori 2: Çalışma Zamanı Hataları & Veritabanı Bütünlüğü

---

### 3.1. Hesap Silme Sırasında Foreign Key Çökmesi (`ExecuteUpdateAsync` & Soft Delete)
* **Hata Türü:** Database Constraint Violation (SQL Server 547 / `DbUpdateException`)
* **İlgili Dosya:** [`src/TodoApp.Infrastructure/Repositories/UserRepository.cs` (Satır 40-45)](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Infrastructure/Repositories/UserRepository.cs#L40-L45)

#### 🧐 Kök Neden (Root Cause)
`UserRepository.DeleteAsync` kullanıcıyı silmeden önce SQL Server kısıtlamaları (`NoAction`) nedeniyle ilişkili FK kolonlarını manuel temizler:
```csharp
await _context.TodoItems.Where(t => t.DeletedByUserId == user.Id)
    .ExecuteUpdateAsync(s => s.SetProperty(t => t.DeletedByUserId, (Guid?)null));
```
Ancak `TodoItem` üzerinde `HasQueryFilter(t => !t.IsDeleted)` tanımlıdır.
EF Core'da `ExecuteUpdateAsync` çağrısı bu filtreyi devreye sokar.
Bu nedenle `DeletedByUserId`'si bu kullanıcı olan **tüm soft-delete edilmiş görevler filtrelenir ve null yapılamaz!**
Hemen ardından `_context.Users.Remove(user)` çalıştığında SQL Server yabancı anahtar kısıtı devreye girer ve işlem **500 Internal Server Error** ile çöker.

#### ✅ Düzeltme Yöntemi
Sorguların başına `.IgnoreQueryFilters()` eklenmelidir:
```csharp
// UserRepository.cs - DeleteAsync
await _context.TodoItems.IgnoreQueryFilters()
    .Where(t => t.CompletedByUserId == user.Id)
    .ExecuteUpdateAsync(s => s.SetProperty(t => t.CompletedByUserId, (Guid?)null));

await _context.TodoItems.IgnoreQueryFilters()
    .Where(t => t.DeletedByUserId == user.Id)
    .ExecuteUpdateAsync(s => s.SetProperty(t => t.DeletedByUserId, (Guid?)null));
```

---

## 4. Kategori 3: Canlıya Dağıtım (Deployment) & Konfigürasyon İyileştirmeleri

---

### 4.1. Swagger Arayüzünün Canlı Ortamda 404 Dönmesi
* **Konum:** [`src/TodoApp.Api/Program.cs` (Satır 104-108)](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Program.cs#L104-L108)
* **Kök Neden:** `app.UseSwagger()` ve `app.UseSwaggerUI()` çağrıları `if (app.Environment.IsDevelopment())` bloğu altındadır. Azure ortamında `ASPNETCORE_ENVIRONMENT = Production` olduğundan Swagger canlıda açılmaz.
* **Düzeltme:**
  Portfolyo veya canlı test amacıyla Swagger'ın açık kalması isteniyorsa `Program.cs` güncellenmelidir:
  ```csharp
  var enableSwagger = app.Environment.IsDevelopment() 
      || builder.Configuration.GetValue<bool>("Swagger:EnableInProduction");

  if (enableSwagger)
  {
      app.UseSwagger();
      app.UseSwaggerUI();
  }
  ```

---

### 4.2. Şifre Sıfırlama E-posta Linkinin Canlıda Localhost Kalması
* **Konum:** [`src/TodoApp.Api/appsettings.json` (Satır 43)](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/appsettings.json#L43)
* **Kök Neden:** `"ResetUrl": "http://localhost:5240/api/Auth/reset-password"` tanımlıdır.
* **Düzeltme:**
  Azure App Service Configuration ekranına şu ortam değişkeni tanımlanmalıdır:
  * **Key:** `PasswordReset__ResetUrl`
  * **Value:** `https://todoapp-api-gudhgje6bvfqg3ev.centralus-01.azurewebsites.net/api/Auth/reset-password` (veya Flutter web/mobil derin bağlantısı).

---

### 4.3. CORS AllowedOrigins Canlı Domain Tanımı
* **Konum:** [`src/TodoApp.Api/appsettings.json` (Satır 53-58)](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/appsettings.json#L53-L58)
* **Kök Neden:** Sadece `localhost:3000` ve `5173` mevcuttur.
* **Düzeltme:**
  Canlı frontend adresi belirlendiğinde Azure ortam değişkenlerine eklenmelidir:
  * **Key:** `Cors__AllowedOrigins__0`
  * **Value:** `https://uygulamaniz.vercel.app`

---

### 4.4. Çoklu Pod / Scale-Out Ortamında Migration Yarış Durumu (Race Condition)
* **Konum:** [`src/TodoApp.Api/Program.cs` (Satır 138)](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Program.cs#L138)
* **Kök Neden:** Startup'ta `dbContext.Database.MigrateAsync()` çağrılmaktadır. Uygulama tek instance çalışırken sorunsuzdur; ancak yatayda ölçekleme (scale-out) yapıldığında birden çok container aynı anda migration çalıştırmayı deneyebilir.
* **Tavsiye:** Kurumsal üretim hatlarında migration'lar GitHub Actions CD adımında EF Core Bundle (`bundle.exe`) veya `dotnet ef database update` komutuyla bağımsız çalıştırılmalıdır.

---

## 5. Kategori 4: Kötüye Kullanım Senaryoları & Önleyici Mimari

---

### 5.1. Kötü Niyetli Hesap Kilitleme Saldırısı (Account Lockout DoS)
* **Konum:** [`AuthService.cs` (Satır 95-101)](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/AuthService.cs#L95-L101)
* **Kök Neden:** Herhangi bir IP'den bilinen bir e-posta adresine 5 ardışık hatalı şifre yollandığında hesap 15 dakika kilitlenir.
* **Risk:** Saldırgan yöneticinin veya kurbanın e-postasını sürekli kilitleyerek sisteme erişmesini engelleyebilir.
* **Önleyici İyileştirme:**
  - Sadece e-posta bazlı değil, istemci IP'si ile e-posta bileşimine göre kilit uygulanması.
  - Veya 5 hatalı denemeden sonra hesabı tamamen kilitlemek yerine her denemede artan bekleme süresi (exponential delay) uygulanması.

---

### 5.2. E-posta Varlık Tespiti (User Enumeration via Task Share)
* **Konum:** [`TaskShareService.cs` (Satır 53-56)](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/Services/TaskShareService.cs#L53-L56)
* **Kök Neden:** Var olmayan bir kullanıcıyla görev paylaşılmak istendiğinde `404 Not Found` dönmektedir.
* **Risk:** Giriş yapmış kötü niyetli bir kullanıcı, bir script ile e-posta listesi tarayarak hangilerinin sistemde kayıtlı olduğunu anlayabilir.
* **Önleyici İyileştirme:** `TaskSharesController.ShareTask` endpoint'ine katı bir rate limit (ör. saatte en fazla 10-15 paylaşım denemesi) konulması.

---

### 5.3. Süresi Dolan Token Tablolarının Sonsuz Şişmesi (Unbounded Growth)
* **Konum:** `RefreshTokens` ve `PasswordResetTokens` tabloları.
* **Kök Neden:** Süresi dolan veya iptal edilen (`IsRevoked = true`) token kayıtları silinmemektedir.
* **Risk:** Zaman içinde tablonun yüz binlerce satıra ulaşıp veritabanı boyutunu ve indeks arama sürelerini artırması.
* **Önleyici İyileştirme:**
  `TodoReminderService` arka plan servisi içerisine günde bir kez çalışan şu temizlik sorgusu eklenmelidir:
  ```csharp
  var cutoff = DateTime.UtcNow.AddDays(-7);
  await context.RefreshTokens
      .Where(rt => rt.ExpiresAt < cutoff || (rt.IsRevoked && rt.CreatedAt < cutoff))
      .ExecuteDeleteAsync(cancellationToken);
  ```

---

## 6. Adım Adım Uygulama ve Doğrulama Yol Haritası

| Aşama | Yapılacak İşlem | İlgili Dosyalar | Beklenen Sonuç |
|:---:|---|---|---|
| **Aşama 1** | **2FA Oturum Güvenliği (Kritik)** | `AuthService.cs`, `TwoFactorLoginRequest.cs`, `IJwtTokenGenerator.cs` | 2FA girişinde şifresiz bypass tamamen engellenir, geçici imzalı token doğrulanır. |
| **Aşama 2** | **Hesap Silme SQL Fix** | `UserRepository.cs` | `IgnoreQueryFilters()` eklenerek soft-delete edilmiş görevleri olan kullanıcıların hesabı hatasız silinir. |
| **Aşama 3** | **Auth Rate Limiting** | `RateLimiterExtensions.cs`, `AuthController.cs` | `login-2fa` ve `reset-password` kaba kuvvet saldırılarına karşı korunur. |
| **Aşama 4** | **TodoList IDOR Fix** | `TodoItemService.cs` | Başka kullanıcıların listesine yetkisiz görev bağlama engellenir. |
| **Aşama 5** | **Deploy Ayarları & Swagger** | `Program.cs`, `appsettings.json`, Azure Portal | Canlıda Swagger isteğe bağlı açılır, e-postalardaki şifre sıfırlama linki production domainine bakar. |
| **Aşama 6** | **Doğrulama & Testler** | Tüm test projeleri | 197 testin tamamı korunur, yeni güvenlik senaryoları için birim testleri yazılır. |
