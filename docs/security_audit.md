# 🛡️ TodoApp — Güvenlik & Performans Denetim Raporu

**Tarih:** 1 Eylül 2026 (Son Güncelleme: 7 Eylül 2026)  
**Kapsam:** EPIC 1 (Auth) · EPIC 2 (TodoItem CRUD) · EPIC 3 (SubTask) · EPIC 4 (Tag Management) · EPIC 4.5 (Security) · EPIC 5 (TaskShare) · EPIC 6 (TaskAuthorization)  
**Denetlenen Dosya Sayısı:** 17 dosya / kategori  

---

## 📊 Özet Durum Tablosu

| # | Bulgu | Şiddet | Kategori | Durum |
|:---:|---|:---:|:---:|:---:|
| 1 | Reflected XSS — Şifre Sıfırlama Sayfası | 🔴 **Kritik** | Güvenlik | ✅ **Çözüldü (EPIC 4.5)** |
| 2 | Hardcoded Secrets — JWT Key & DB Bağlantısı | 🔴 **Kritik** | Güvenlik | ✅ **Çözüldü (EPIC 8)** |
| 3 | Pagination Yok — Tüm Liste Endpoint'leri | 🔴 **Yüksek** | Performans | ✅ **Çözüldü (EPIC 4.5)** |
| 4 | Rate Limiting Yok — Auth Endpoint'leri | 🟠 **Yüksek** | Güvenlik | ✅ **Çözüldü (EPIC 8)** |
| 5 | Hassas Bilgi Sızıntısı — Hata Mesajları | 🟠 **Yüksek** | Güvenlik | ✅ **Çözüldü (EPIC 4.5)** |
| 6 | DTO Input Validation Eksik — Tüm DTO'lar | 🟠 **Yüksek** | Güvenlik | ✅ **Çözüldü (EPIC 4.5)** |
| 7 | Timing Attack — Login Endpoint'i | 🟡 **Orta** | Güvenlik | ✅ **Çözüldü (EPIC 8)** |
| 8 | CORS Yapılandırması Yok | 🟡 **Orta** | Güvenlik | ✅ **Çözüldü (EPIC 8)** |
| 9 | Refresh Token Düz Metin Saklanıyor | 🟡 **Orta** | Güvenlik | ✅ **Çözüldü (EPIC 8)** |
| 10 | Soft Delete Akışı Kırık (Dead Code) | 🟡 **Orta** | İş Mantığı | ✅ **Çözüldü (EPIC 4.5 & 6)** |
| 11 | Şifre Sıfırlamada Refresh Token'lar İptal Edilmiyor | 🟡 **Orta** | Güvenlik | ✅ **Çözüldü (EPIC 8)** |
| 12 | Şifre Politikası Yok (Uzunluk / Karmaşıklık) | 🟡 **Orta** | Güvenlik | ✅ **Çözüldü (EPIC 4.5 & 8)** |
| 13 | Tag Aramada Non-SARGable Sorgu (`ToLower()`) | 🟡 **Orta** | Performans | ✅ **Çözüldü (EPIC 8)** |
| 14 | Read Sorgularında `AsNoTracking()` Eksik | 🔵 **Düşük** | Performans | ✅ **Çözüldü (EPIC 8)** |
| 15 | `nvarchar(max)` Kolon Boyutu Kontrolsüz | 🔵 **Düşük** | Performans | ✅ **Çözüldü (EPIC 8)** |
| 16 | Security Headers Eksik (HSTS, CSP, X-Frame) | 🔵 **Düşük** | Güvenlik | ✅ **Çözüldü (EPIC 8 & 12)** |

---

## 🔴 KRİTİK SEVİYE BULGULAR

---

### 1. Reflected XSS — Şifre Sıfırlama Sayfası

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 4.5 — Task T4.5.4.1)**  
> `AuthController.ResetPasswordPage` metodunda gelen `token` parametresi `System.Net.WebUtility.HtmlEncode(token)` ile encode edilerek XSS açığı tamamen kapatılmıştır.

**Dosya:** [AuthController.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/AuthController.cs)

**Sorun:** `ResetPasswordPage` metodunda `token` query parametresi doğrudan HTML içine **encode edilmeden** yerleştiriliyordu:

```csharp
// ❌ ESKİ ZAYIF KOD:
value="{token}"   // token içeriği encode edilmiyordu!

// ✅ YENİ GÜVENLİ KOD:
var encodedToken = System.Net.WebUtility.HtmlEncode(token);
value="{encodedToken}"
```

---

### 2. Hardcoded Secrets — JWT Key & DB Connection String

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 8 — Task T8.2.5)**  
> `TodoApp.Api` projesinde `UserSecretsId` yapılandırılmış, yerel geliştirme sırları (`Jwt:Key`, `ConnectionStrings:DefaultConnection`, `Smtp:Password`) `dotnet user-secrets` deposuna aktarılmış, `appsettings.json` ve `appsettings.*.json` Git'ten (`.gitignore`) tamamen hariç tutulmuş ve yeni geliştiriciler için dummy değerler içeren `appsettings.Example.json` şablonu oluşturulmuştur.

**Dosya:** [appsettings.json](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/appsettings.json)

```json
"Jwt": {
  "Key": "a3d4f8e1b7c94a2f85e6d7c3b1a09f8e7d6c5b4a3e2f1d0c9b8a7f6e5d4c3b2",  // ❌ Hardcoded!
  "Issuer": "TodoApp",
  "Audience": "TodoAppUsers"
},
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;...Password=YourStrongPassword123!;..."  // ❌ Hardcoded!
}
```

---

## 🟠 YÜKSEK SEVİYE BULGULAR

---

### 3. Pagination Yok — Tüm Liste Endpoint'leri (DoS / OOM Riski)

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 4.5 — User Story 4.5.2)**  
> `PaginatedRequest` ve `PaginatedResponse<T>` altyapısı kuruldu. `GET /api/TodoItems`, `GET /api/TodoItems/trash` ve `GET /api/tags/{tagId}/todoitems` endpoint'lerine `?page=1&pageSize=20` (max 100) sayfalama eklendi.

**Etkilenen Endpoint'ler:**
* `GET /api/TodoItems` (Sayfalandı ✅)
* `GET /api/TodoItems/trash` (Sayfalandı ✅)
* `GET /api/tags/{tagId}/todoitems` (Sayfalandı ✅)

---

### 4. Rate Limiting Yok — Auth Endpoint'leri (Brute Force Riski)

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 8 — Task T8.1.1)**  
> ASP.NET Core 10 yerleşik `Microsoft.AspNetCore.RateLimiting` middleware'i `RateLimiterServiceExtensions` sınıfında yapılandırıldı. `auth-login` (5 istek/dk), `auth-register` (3 istek/dk) ve `auth-forgot-password` (2 istek/dk) politikaları eklendi. Kota aşıldığında RFC 7807 uyumlu `429 Too Many Requests` yanıtı dönülmektedir.

---

### 5. Hassas Bilgi Sızıntısı — Hata Mesajlarında İç Detaylar

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 4.5 — User Story 4.5.3)**  
> `ExceptionHandlingMiddleware` güncellendi. 500 hatalarında iç SQL ve exception mesajları istemciye sızdırılmayıp genel `"Beklenmeyen bir hata oluştu."` mesajı dönülmektedir. Geliştirme ortamında debug kolaylığı için `IHostEnvironment.IsDevelopment()` kontrolü eklendi.

**Dosya:** [ExceptionHandlingMiddleware.cs](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Middleware/ExceptionHandlingMiddleware.cs)

---

### 6. DTO Input Validation Eksik — Tüm DTO'lar

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 4.5 — User Story 4.5.1)**  
> `FluentValidation` ve `FluentValidation.AspNetCore` kütüphaneleri sisteme entegre edildi. Tüm DTO'lar için validatörler yazıldı. Şifre alanı için **BCrypt DoS saldırılarını engelleyen** min 8, max 128 karakter kuralı eklendi ve birim testleri yazıldı.

**Eklenen Validatörler:**
* `RegisterRequestValidator`, `LoginRequestValidator`, `ForgotPasswordRequestValidator`, `ResetPasswordRequestValidator`, `ChangePasswordRequestValidator`, `RefreshTokenRequestValidator`
* `CreateTodoItemRequestValidator`, `UpdateTodoItemRequestValidator`, `CreateSubTaskRequestValidator`, `CreateTagRequestValidator`, `ShareTaskRequestValidator`, `CreateTransferRequestDtoValidator`

---

## 🟡 ORTA SEVİYE BULGULAR

---

### 7. Timing Attack — Login Endpoint'inde E-posta Numaralandırma

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 8 — Task T8.1.4)**  
> `AuthService.LoginAsync` metodunda kullanıcı veritabanında bulunamadığında dahi önceden üretilmiş dummy bir hash üzerinde `BCrypt.Verify` veya `BCrypt.EnhancedVerify` çalıştırılarak yanıt sürelerinin eşitlenmesi sağlandı. Böylece saldırgan yanıt gecikmesinden e-postanın sistemde kayıtlı olup olmadığını anlayamaz.

---

### 8. CORS Yapılandırması Yok

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 8 — Task T8.1.2)**  
> `CorsServiceExtensions` oluşturularak `AllowCredentials()`, izin verilen Frontend ve Mobil origin'ler, HTTP metotları (`GET, POST, PUT, DELETE, PATCH, OPTIONS`) ve header yapılandırması SignalR ile uyumlu şekilde tanımlandı.

---

### 9. Refresh Token Düz Metin Saklanıyor

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 8 — Task T8.1.7)**  
> `RefreshToken` nesnesinin ham değeri istemciye iletilirken veritabanına `SHA-256` ile özetlenmiş `TokenHash` değeri kaydedilmektedir. Doğrulama yapılırken istemciden gelen token hash'lenerek DB'deki hash ile kıyaslanır. Veritabanı sızıntısında token'lar doğrudan ele geçirilemez.

---

### 10. Soft Delete Akışı Kırık (Dead Code)

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 4.5 & EPIC 6)**  
> `TodoItemService.DeleteAsync` soft delete olarak düzeltildi (`IsDeleted=true`, `DeletedByUserId`, `DeletedAt`). Kalıcı silme `DELETE /api/todoitems/{id}/permanent` endpoint'ine taşındı. Yetkilendirme `ITaskAuthorizationService` ile merkezi ve hatasız hale getirildi.

---

### 11. Şifre Sıfırlamada Aktif Oturumlar İptal Edilmiyor

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 8 — Task T8.1.6)**  
> Hem `ChangePasswordAsync` hem de `ResetPasswordAsync` akışlarında kullanıcının tüm aktif refresh token'ları (`RevokedAt = DateTime.UtcNow`) iptal edilmekte ve `SecurityStamp` değeri yenilenerek tüm eski JWT oturumları anında geçersiz kılınmaktadır.

---

### 12. Şifre Politikası Yok

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 4.5 & EPIC 8 — Task T8.1.5)**  
> `FluentValidation` kuralları ile: En az 8, en fazla 128 karakter, en az bir büyük harf, en az bir küçük harf, en az bir rakam ve en az bir özel karakter zorunluluğu regex doğrulamasıyla garanti altına alındı.

---

## 🔵 DÜŞÜK SEVİYE BULGULAR

---

### 13. Tag Aramada Full Table Scan (`ToLower()`)

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 8 — Task T8.2.7)**  
> Veri girişi aşamasında (ingestion-time) `Tag.Name` ve `User.Email` küçük harfe normalize edilerek (`ToLowerInvariant()`) DB'ye yazılmaktadır. Arama sorgularında ise kolon üzerinde `ToLower()` çağrısı kaldırılmış, sadece parametre normalize edilerek doğrudan eşitlik (`t.Name == normalized`) sağlanmıştır. Bu sayede veritabanı motoru bağımlılığı olmadan %100 sargable B-Tree **Index Seek ($O(\log N)$)** garanti altına alınmıştır.

---

### 14. Read Sorgularında `AsNoTracking()` Eksik

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 8 — Task T8.3.1)**  
> Okuma sorgularına `.AsNoTracking()` eklenerek bellek optimizasyonu EPIC 8'de sağlanmıştır.

---

### 15. Kontrolsüz `nvarchar(max)` Kolonlar

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 8 — Task T8.2.6)**  
> Veritabanında `Title` (200), `Description` (2000), `Email` (256), `PasswordHash` (256), `SubTask.Title` (200), `Tag.Name` (50) ve Token'lar (450) için `MaxLength` kısıtlamaları uygulanmış ve `AddMaxLengthConstraints` migration'ı oluşturulmuştur.

---

### 16. Security Headers Eksik

> [!NOTE]
> **Durum:** ✅ **Çözüldü (EPIC 8 & 12 — Task T8.1.3 & NetworkSecurityExtensions)**  
> `NetworkSecurityExtensions` middleware katmanında `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Content-Security-Policy` ve `HSTS` başlıkları eklenmiştir.

---

## 🎯 Güncel Aksiyon Planı & İlerleme

| Öncelik | Aksiyon | Durum |
|:---:|---|:---:|
| 🔴 P0 | XSS düzeltmesi (HTML encode) | ✅ **Tamamlandı (EPIC 4.5)** |
| 🔴 P0 | Secrets'ı `user-secrets` / env vars'a taşı | ✅ **Tamamlandı (EPIC 8)** |
| 🟠 P1 | Tüm DTO'lara input validation ekle | ✅ **Tamamlandı (EPIC 4.5)** |
| 🟠 P1 | ExceptionHandling'de 500 mesajlarını gizle | ✅ **Tamamlandı (EPIC 4.5)** |
| 🟠 P1 | Pagination altyapısı kur | ✅ **Tamamlandı (EPIC 4.5)** |
| 🟠 P1 | Rate Limiting middleware ekle | ✅ **Tamamlandı (EPIC 8)** |
| 🟡 P2 | Login timing attack düzeltmesi | ✅ **Tamamlandı (EPIC 8)** |
| 🟡 P2 | Soft-delete akışını düzelt | ✅ **Tamamlandı (EPIC 4.5 & 6)** |
| 🟡 P2 | Şifre politikası uygula | ✅ **Tamamlandı (EPIC 4.5 & 8)** |
| 🟡 P2 | ResetPassword'da refresh token'ları iptal et | ✅ **Tamamlandı (EPIC 8)** |
| 🟡 P2 | CORS yapılandır | ✅ **Tamamlandı (EPIC 8)** |
| 🔵 P3 | AsNoTracking, MaxLength, Security Headers | ✅ **Tamamlandı (EPIC 8 & 12)** |
| 🏆 **SONUÇ** | **Tüm Güvenlik ve Performans Bulguları** | **✅ %100 Çözüldü (16/16)** |
