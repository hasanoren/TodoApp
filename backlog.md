# To-Do App — Backlog (Epic → User Story → Task)

**Stack:** .NET 10, EF Core, ASP.NET Core Web API, JWT Auth
**Referans dokümanlar:** `business-rules.md`, `business-rules-layers.md`

Her task'ta ilgili BR-XXX numarası belirtilmiştir. Bir task'ı uygularken ilgili kuralın DB mi Servis mi katmanında olduğunu `business-rules-layers.md`'den kontrol et.

---

## EPIC 0: Proje Altyapısı (Setup)

### User Story 0.1 — Solution ve proje yapısı kurulumu
- [x] T0.1.1 — Solution oluştur: `TodoApp.Api`, `TodoApp.Application`, `TodoApp.Domain`, `TodoApp.Infrastructure` projeleri
- [x] T0.1.2 — Proje referanslarını bağla (Api → Application → Domain, Infrastructure → Domain, Infrastructure → Application)
- [x] T0.1.3 — NuGet paketleri: EF Core, EF Core.SqlServer, FluentValidation, JWT Bearer, BCrypt paketleri (her paket doğru katmanda — bkz. paket dağılımı notu)
- [x] T0.1.4 — `appsettings.json` yapılandırması (connection string, JWT secret)
- [x] T0.1.5 — Temel middleware pipeline (global exception handling middleware, Swagger/OpenAPI). CORS henüz eklenmedi — mobile/frontend entegrasyonuna kadar erteleniyor.

### User Story 0.2 — CI/CD ve geliştirme ortamı
- [x] T0.2.1 — `.gitignore`, temel README
- [x] T0.2.2 — Docker Compose ile local SQL Server container
- [x] T0.2.3 — GitHub Actions: build workflow (test adımı, EPIC 7'de gerçek testler yazılınca eklenecek)

---

## EPIC 1: Kullanıcı Yönetimi (User)

### User Story 1.1 — Kayıt ve Kimlik Doğrulama
- [x] T1.1.1 — `User` entity ve EF Core migration (BR-001: Email unique, BR-005: Role kolonu)
- [x] T1.1.2 — `POST /api/Auth/register` endpoint (şifre hash'leme, Role default = User)
- [x] T1.1.3 — `POST /api/Auth/login` endpoint, JWT token üretimi
- [x] T1.1.4 — JWT middleware entegrasyonu (`[Authorize]` attribute'ları için)
- [x] T1.1.5 — Unit test: duplicate email kaydı reddedilmeli (BR-001)

### User Story 1.2 — Kullanıcı Silme
- [x] T1.2.1 — `DELETE /users/me` endpoint
- [x] T1.2.2 — Cascade delete davranışının doğrulanması: User silinince Task ve TaskShare'lerin gittiğini test et (BR-002, BR-003)
- [x] T1.2.3 — Entegrasyon testi: User silindiğinde ilişkili kayıtların temizlendiğini doğrula

### User Story 1.3 — Refresh Token
- [x] T1.3.1 — `RefreshToken` entity ve migration (UserId FK, Token, ExpiresAt, IsRevoked alanları)
- [x] T1.3.2 — Login/Register response'una refresh token eklenmesi (access token kısa ömürlü, refresh token uzun ömürlü)
- [x] T1.3.3 — `POST /api/Auth/refresh` endpoint — geçerli refresh token ile yeni access token üretimi
- [x] T1.3.4 — Refresh token rotasyonu: her kullanımda eski token geçersiz kılınıp yenisi verilir
- [x] T1.3.5 — `POST /api/Auth/logout` endpoint — refresh token'ı geçersiz kılar (IsRevoked = true)
- [x] T1.3.6 — Unit test: süresi dolmuş/geçersiz refresh token reddedilmeli ve token rotasyonu doğrulanmalı

**Not (teknik borç):** `IConfiguration` şu an doğrudan hem Infrastructure (`JwtTokenGenerator`) hem ileride başka yerlerde string-key ile okunuyor. Options Pattern'e (`JwtSettings` strongly-typed sınıfı) geçiş bilinçli olarak ertelendi — ileride toplu bir refactor task'ı olarak ele alınacak.

**Not (JWT authorization):** `[Authorize]` middleware'i kurulu ve `PUT /api/Auth/change-password` endpoint'inde aktif olarak kullanılıyor. JWT doğrulamasının tam uçtan uca çalıştığı, EPIC 2'de (Task CRUD, tüm endpoint'lerde `[Authorize]` kullanımı) kapsamlı olarak doğrulanacak.

### User Story 1.4 — Forgot Password (Şifremi Unuttum)
- [x] T1.4.1 — `PasswordResetToken` entity ve migration (UserId FK, Token, ExpiresAt, IsUsed alanları)
- [x] T1.4.2 — Email gönderme servisi entegrasyonu (MailKit + Gmail SMTP)
- [x] T1.4.3 — `POST /api/Auth/forgot-password` endpoint — email varsa süreli reset token üretip mail gönderir
- [x] T1.4.4 — `GET/POST /api/Auth/reset-password` endpoint — token doğrulanır, yeni şifre hash'lenip kaydedilir (HTML form ile test edilebilir hale getirildi)
- [x] T1.4.5 — Güvenlik: var olmayan email için de aynı "email gönderildi" mesajı dönülüyor (user enumeration önleme)
- [x] T1.4.6 — Unit test: süresi dolmuş veya daha önce kullanılmış reset token reddedilmeli
- [x] T1.4.7 (bonus, plana sonradan eklendi) — `PUT /api/Auth/change-password` endpoint — giriş yapmış kullanıcı şifresini değiştirebilir, tüm refresh token'ları geçersiz kılınır

**Not (teknik borç):** `ForgotPasswordAsync`'de email gönderimi başarısız olursa (SMTP hatası) exception fırlıyor ve `500` dönüyor — bu, "kullanıcı yok" (`200`) durumundan ayırt edilebilir olduğu için hafif bir user-enumeration sinyali oluşturabilir. İleride email gönderimini try-catch ile sarıp hatasını sadece loglamak, her koşulda `200` dönmek daha güvenli olur. Şimdilik giriş seviyesi proje için kabul edilebilir bir risk olarak bırakıldı.

### User Story 1.5 — İki Adımlı Doğrulama (2FA)
- [x] T1.5.1 — `User` entity'sine `TwoFactorEnabled`, `TwoFactorSecret` alanları eklenmesi (migration)
- [x] T1.5.2 — TOTP kütüphanesi entegrasyonu (örn. Otp.NET) — secret üretimi ve QR kod verisi oluşturma
- [x] T1.5.3 — `POST /api/Auth/2fa/enable` endpoint — QR kod verisi döner, kullanıcı authenticator app ile eşler
- [x] T1.5.4 — `POST /api/Auth/2fa/verify` endpoint — ilk doğrulama kodu ile 2FA'yı aktive eder
- [x] T1.5.5 — Login akışının güncellenmesi: `TwoFactorEnabled = true` ise, şifre doğrulamasından sonra ikinci adım (kod girişi) istenir
- [x] T1.5.6 — `POST /api/Auth/2fa/disable` endpoint
- [x] T1.5.7 — Unit test: yanlış TOTP kodu reddedilmeli, doğru kod kabul edilmeli

---

## EPIC 2: Görev Yönetimi (Task)

**Not (naming):** C# `Task` isim çakışması nedeniyle entity adı `TodoItem` olarak belirlendi. DB tablo adı `TodoItems`, endpoint route'u `/api/todoitems`.

### User Story 2.1 — Task CRUD (Temel)
- [x] T2.1.1 — `TodoItem` entity ve migration (OwnerId NOT NULL — BR-006; Status, IsDeleted, DeletedByUserId, DeletedAt, CompletedByUserId, CompletedAt kolonları). FK davranışları: OwnerId CASCADE, CompletedByUserId SET NULL, DeletedByUserId SET NULL (SQL Server multiple cascade paths çözümü)
- [x] T2.1.2 — `POST /api/todoitems` — yeni görev oluşturma
- [x] T2.1.3 — `GET /api/todoitems` — kullanıcının erişebildiği görevleri listeleme (owner, `IsDeleted=false` filtresi — BR-011). Paylaşılan görevler EPIC 5'te eklenecek.
- [x] T2.1.4 — `GET /api/todoitems/{id}` — tekil görev getirme; yetkisiz erişimde 404 (BR-029)
- [x] T2.1.5 — `PUT /api/todoitems/{id}` — görev güncelleme (title, description, dueDate)
- [x] T2.1.6 — Unit test: BR-029 senaryosu — yetkisiz kullanıcı 404 almalı

### User Story 2.2 — Görev Tamamlama
- [x] T2.2.1 — `PATCH /api/todoitems/{id}/complete` endpoint
- [x] T2.2.2 — Yetki kontrolü: şimdilik sadece owner (BR-025 paylaşılan kullanıcı desteği EPIC 5'te eklenecek)
- [x] T2.2.3 — `CompletedByUserId`, `CompletedAt` alanlarının set edilmesi
- [x] T2.2.4 — Unit test: Complete sonrası CompletedByUserId ve CompletedAt doğru set ediliyor mu (BR-015)
- [x] T2.2.5 — Görev tamamlama durumunun Toggle (Aç/Kapa) desteği: Tamamlanmış bir göreve tekrar complete isteği atıldığında durumun `Open`'a çekilmesi, `CompletedByUserId` ve `CompletedAt` alanlarının `null` yapılması (SubTask `CompleteAsync` davranışı ile tutarlılık sağlanması).

### User Story 2.3 — Görev Silme ve Geri Getirme
- [x] T2.3.1 — `DELETE /api/todoitems/{id}` endpoint — yetki kontrolü
- [x] T2.3.2 — Yalnızca Owner silebilir; silme yetkisi sadece sahibe aittir (BR-008)
- [x] T2.3.3 — Paylaşılan kullanıcılar görevi kesinlikle silemez (ne soft ne hard delete) (BR-026); silmeye çalışırsa 404 döner (BR-029)
- [x] T2.3.4 — `POST /api/todoitems/{id}/restore` endpoint — sadece owner çağırabilir (BR-010)
- [x] T2.3.5 — `GET /api/todoitems/trash` — owner'ın soft-delete edilmiş görevlerini görebileceği endpoint
- [x] T2.3.6 — Unit test: tamamlanmış görev de silinebiliyor mu (BR-009)
- [x] T2.3.7 — Unit test: soft-delete edilmiş görev aktif listede görünmüyor mu (BR-011). SubTask ekleme engellemesi (BR-012) EPIC 3'te test edilecek.

---

## EPIC 3: Alt Görevler (SubTask)

### User Story 3.1 — SubTask CRUD
- [x] T3.1.1 — `SubTask` entity ve migration (TaskId NOT NULL, `ON DELETE CASCADE` — BR-016, BR-019)
- [x] T3.1.2 — `POST /api/todoitems/{taskId}/subtasks` — yetki kontrolü parent Task üzerinden (BR-020, BR-029)
- [x] T3.1.3 — `GET /api/todoitems/{taskId}/subtasks` — parent soft-delete ise erişilemez (BR-018)
- [x] T3.1.4 — `PATCH /api/subtasks/{id}/complete` — alt görevi tamamlama/açma (BR-017)
- [x] T3.1.5 — `DELETE /api/subtasks/{id}` — alt görevi silme
- [x] T3.1.6 — Unit test: üst görev tamamlanınca alt görevlerin durumu değişmiyor mu (BR-017)
- [x] T3.1.7 — Unit test: üst görev hard silinince alt görevler cascade siliniyor mu (BR-019)
- [x] T3.1.8 — Unit test: soft-delete edilmiş task'a yeni SubTask eklenemiyor mu (BR-012)
- [x] T3.1.9 — Maksimum Alt Görev Limiti Kuralı (DoS Koruması): Bir ana göreve en fazla 50 alt görev eklenebilmesi iş kuralının eklenmesi (Limit aşıldığında `ValidationException` ile 400 Bad Request dönülmesi ve unit testlerinin yazılması).

---

## EPIC 4: Etiketler (Tag)

### User Story 4.1 — Tag Yönetimi (Admin)
- [x] T4.1.1 — `Tag` entity ve migration (Name unique, case-insensitive — BR-021, BR-023; CreatedByUserId ON DELETE SET NULL)
- [x] T4.1.2 — `POST /api/tags` — sadece Admin rolü oluşturabilir (BR-022)
- [x] T4.1.3 — `GET /api/tags` — herkes listeleyebilir (BR-021)
- [x] T4.1.4 — Unit test: Admin olmayan kullanıcı Tag oluşturamaz (BR-022) / duplicate name kontrolü (BR-021)

### User Story 4.2 — Task-Tag İlişkilendirme
- [x] T4.2.1 — `TodoItemTag` ara tablo migration (Composite PK: TaskId+TagId — BR-024)
- [x] T4.2.2 — `POST /api/todoitems/{taskId}/tags/{tagId}` — ilişkilendirme (BR-024)
- [x] T4.2.3 — `DELETE /api/todoitems/{taskId}/tags/{tagId}` — ilişkiyi kaldırma
- [x] T4.2.4 — Unit test: aynı Tag aynı Task'a iki kez eklenemiyor mu (BR-024)

---

## EPIC 4.5: Güvenlik Altyapısı (Temel İyileştirmeler) 🛡️

> **Neden şimdi?** Bu iyileştirmeler tüm yeni kodun üzerine inşa edileceği temel pattern'ları kurar.
> EPIC 5'ten önce yapılmazsa, sonra yazılacak her endpoint'in geri dönüp düzeltilmesi gerekir.
> Kaynak: `security_audit.md` — 1 Eylül 2026 güvenlik denetim raporu.

### User Story 4.5.1 — FluentValidation Entegrasyonu (Audit #6)
- [x] T4.5.1.1 — `FluentValidation.AspNetCore` NuGet paketini `TodoApp.Api` / `TodoApp.Application` projesine ekle
- [x] T4.5.1.2 — `Program.cs`'e FluentValidation pipeline entegrasyonu (`AddFluentValidationAutoValidation`)
- [x] T4.5.1.3 — Auth DTO Validator'ları: `RegisterRequestValidator` (`[Required]`, `[EmailAddress]`, şifre min 8, max 128 karakter — BCrypt DoS koruması), `LoginRequestValidator`, `ForgotPasswordRequestValidator`, `ResetPasswordRequestValidator`, `ChangePasswordRequestValidator`, `RefreshTokenRequestValidator`
- [x] T4.5.1.4 — TodoItem DTO Validator'ları: `CreateTodoItemRequestValidator` (Title required, max 200; Description max 2000), `UpdateTodoItemRequestValidator`
- [x] T4.5.1.5 — SubTask DTO Validator'ı: `CreateSubTaskRequestValidator` (Title required, max 200)
- [x] T4.5.1.6 — Tag DTO Validator'ı: `CreateTagRequestValidator` (Name required, max 50)
- [x] T4.5.1.7 — TaskShare & Transfer DTO Validator'ları: `ShareTaskRequestValidator`, `CreateTransferRequestDtoValidator`
- [x] T4.5.1.8 — Unit test: her validator için geçerli/geçersiz input senaryoları (35 yeni test)

### User Story 4.5.2 — Pagination Altyapısı (Audit #3)
- [x] T4.5.2.1 — `PaginatedRequest` DTO oluştur (Page, PageSize — max 100 sınırı)
- [x] T4.5.2.2 — `PaginatedResponse<T>` generic DTO oluştur (Items, TotalCount, Page, PageSize, TotalPages)
- [x] T4.5.2.3 — `TodoItemRepository.GetAccessibleByUserAsync` metodunu paginated hale getir (`.Skip().Take()`)
- [x] T4.5.2.4 — `GET /api/TodoItems` endpoint'ini `?page=1&pageSize=20` parametreleriyle güncelle
- [x] T4.5.2.5 — `GET /api/TodoItems/trash`, `GET /api/tags/{tagId}/todoitems` endpoint'lerini paginated yap
- [x] T4.5.2.6 — Unit test: sayfalama doğru çalışıyor mu, sınır değerler (page=0, pageSize=200) düzgün mü

### User Story 4.5.3 — Hata Mesajı Güvenliği (Audit #5)
- [x] T4.5.3.1 — `ExceptionHandlingMiddleware`: 500 hatalarında `exception.Message` yerine sabit genel mesaj dön (`"Beklenmeyen bir hata oluştu."`)
- [x] T4.5.3.2 — `UnauthorizedAccessException` → 401 Unauthorized mapping ekle
- [x] T4.5.3.3 — Development ortamında detaylı hata, production'da genel mesaj (ortam kontrolü)

### User Story 4.5.4 — XSS Düzeltmesi (Audit #1)
- [x] T4.5.4.1 — `AuthController.ResetPasswordPage`: `token` parametresini `System.Net.WebUtility.HtmlEncode()` ile encode et

### User Story 4.5.5 — Soft Delete Akışı Düzeltmesi (Audit #10)
- [x] T4.5.5.1 — `TodoItemService.DeleteAsync`: Owner'ın silme davranışını soft-delete olarak değiştir (`IsDeleted=true`, `DeletedByUserId`, `DeletedAt`)
- [x] T4.5.5.2 — Hard delete'i ayrı bir `DELETE /api/todoitems/{id}/permanent` endpoint'ine taşı (veya trash'ten ikinci silme ile tetikle)
- [x] T4.5.5.3 — `GetTrashAsync` ve `RestoreAsync`'in doğru çalıştığını unit test ile doğrula

---

## EPIC 5: Görev Paylaşımı (TaskShare) & Sahiplik Devri Onay Sistemi

### User Story 5.1 — Paylaşım Oluşturma/Kaldırma
- [x] T5.1.1 — `TaskShare` entity ve migration (PK: TaskId+UserId — BR-014)
- [x] T5.1.2 — `POST /api/todoitems/{taskId}/shares` — sadece owner çağırabilir (BR-013)
- [x] T5.1.3 — Kendi kendine paylaşım engeli (BR-004)
- [x] T5.1.4 — Var olmayan kullanıcıyla paylaşım → anlamlı validasyon hatası (BR-027)
- [x] T5.1.5 — Duplicate paylaşım → sessizce başarı dönmeli (BR-014)
- [x] T5.1.6 — `DELETE /api/todoitems/{taskId}/shares/{userId}` — owner tarafından kaldırma
- [x] T5.1.7 — `DELETE /api/todoitems/{taskId}/shares/me` — paylaşılan kullanıcının kendi isteğiyle çıkması (BR-028)
- [x] T5.1.8 — Paylaşılan kullanıcının görevi silme denemesinin engellenmesi (BR-008, BR-026, BR-029)
- [x] T5.1.9 — Unit test: BR-004, BR-014, BR-026, BR-027, BR-028 senaryoları

### User Story 5.2 — Görev Sahipliği Devri Onay Mekanizması (Transfer Requests)
- [x] T5.2.1 — `OwnershipTransferRequest` entity ve migration (Status: Pending, Accepted, Rejected, Cancelled — BR-030)
- [x] T5.2.2 — `POST /api/todoitems/{taskId}/transfer-requests` — Devir talebi başlatma (Yalnızca mevcut sahip)
- [x] T5.2.3 — `GET /api/transfer-requests/pending` — Kullanıcının onayını bekleyen devir taleplerini listeleme
- [x] T5.2.4 — `POST /api/transfer-requests/{requestId}/accept` — Devir talebini kabul etme & sahiplik devri & eski sahibin paylaşılanlara eklenmesi
- [x] T5.2.5 — `POST /api/transfer-requests/{requestId}/reject` — Devir talebini reddetme
- [x] T5.2.6 — `POST /api/transfer-requests/{requestId}/cancel` — Sahip tarafından devir talebini iptal etme
- [x] T5.2.7 — Unit testler: Talebi oluşturma, bekleyenleri listeleme, kabul, ret, iptal ve yetki doğrulama (10 test)

---

## EPIC 6: Yetkilendirme Altyapısı (Merkezi Yetki Servisi & Authorization Refactor)

### User Story 6.1 — Authorization Policy & Service Katmanı
- [x] T6.1.1 — `ITaskAuthorizationService` arayüzünün tasarlanması (`EnsureCanReadAsync`, `EnsureCanModifyAsync`, `EnsureCanCompleteAsync`, `EnsureCanDeleteAsync`, `EnsureOwnerAsync`, `EnsureCanManageSubTasksAsync`, `EnsureCanCompleteSubTaskAsync`, `EnsureCanDeleteSubTaskAsync`)
- [x] T6.1.2 — `TaskAuthorizationService` implementasyonu (BR-008, BR-010, BR-011, BR-012, BR-013, BR-018, BR-020, BR-025, BR-026, BR-029 merkezi kontrolü)
- [x] T6.1.3 — `Program.cs`'e `ITaskAuthorizationService` DI kaydının eklenmesi
- [x] T6.1.4 — `TodoItemService`, `SubTaskService` ve `TaskShareService` sınıflarının `ITaskAuthorizationService` kullanacak şekilde refactor edilmesi (SubTask silmenin yalnızca Owner'a ait kılınması)
- [x] T6.1.5 — Unit testler: `TaskAuthorizationServiceTests` (tüm yetki kombinasyonları) ve güncellenen servis testleri (20 yeni test)
- [x] T6.1.6 — Üst Görev Yetki Sıkılaştırması (Refactor): Üst görevi güncelleme (`PUT /api/todoitems/{id}`) ve tamamlama (`Complete`) yetkilerinin YALNIZCA görev sahibine (`Owner`) kısıtlanması; paylaşılan kullanıcıların yalnızca alt görevleri (`SubTasks`) ekleyip güncelleyebilmesi (BR-025 yetki ayrımı)

---

## EPIC 7: Test ve Kalite

### User Story 7.1 — Kapsamlı İş Kuralı Testleri
- [x] T7.1.1 — Tüm 29 BR kuralı için en az bir otomatik test olduğunu doğrulayan bir checklist/matrix oluştur (`test_matrix.md`)
- [x] T7.1.2 — Entegrasyon testleri: gerçek DB ile cascade/constraint davranışlarını doğrula (`DatabaseCascadeIntegrationTests.cs`)
- [x] T7.1.3 — Postman/Swagger ile manuel API smoke test senaryosu (`smoke_test_guide.md`)

---

## EPIC 8: Production-Ready & Güvenlik Altyapısı (Hardening & Quality)

### User Story 8.1 — Güvenlik Sıkılaştırma
- [x] T8.1.1 — Rate Limiting middleware entegrasyonu: `POST /api/Auth/login` (IP başına 5 deneme/dk), `POST /api/Auth/register` (IP başına 3/dk), `POST /api/Auth/forgot-password` (IP başına 2/dk) — Audit #4
- [x] T8.1.2 — CORS politikası yapılandırması: izin verilen origin'ler, HTTP metodları ve header'lar — Audit #8
- [x] T8.1.3 — Security Headers middleware: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Content-Security-Policy`, `Strict-Transport-Security` (HSTS) — Audit #16
- [x] T8.1.4 — Login Timing Attack düzeltmesi: kullanıcı bulunamadığında da dummy BCrypt.Verify çalıştır — Audit #7
- [x] T8.1.5 — Şifre politikası: FluentValidation ile min 8 karakter, en az 1 büyük harf, 1 küçük harf, 1 rakam kuralı — Audit #12
- [x] T8.1.6 — `ResetPasswordAsync`'te tüm aktif refresh token'ları iptal et — Audit #11
- [x] T8.1.7 — Refresh Token hash'leme: DB'de düz metin yerine SHA-256 hash sakla — Audit #9

### User Story 8.2 — Veritabanı ve Mimari İyileştirmeleri
- [x] T8.2.1 — EF Core Global Query Filter: `TodoItem` için `HasQueryFilter(t => !t.IsDeleted)` (`.IgnoreQueryFilters()` ile çöp kutusu yönetimi)
- [x] T8.2.2 — Strongly-Typed Options Pattern: `JwtSettings`, `SmtpSettings` ve `PasswordResetSettings` sınıfları (`IConfiguration` string-key okumalarını ortadan kaldırma)
- [x] T8.2.3 — `IPasswordHasher` abstraction: `BCrypt.Net-Next` somut bağımlılığının Application → Infrastructure'a taşınması
- [x] T8.2.4 — Hardcoded reset password URL'inin konfigürasyona (`PasswordReset:ResetUrl`) taşınması ve e-posta şablonunun dinamikleştirilmesi
- [x] T8.2.5 — Secrets Management: JWT Key, DB connection string ve SMTP şifreleri `dotnet user-secrets`'a taşındı, `appsettings.json` Git'ten ignore edildi ve `appsettings.Example.json` şablonu oluşturuldu — Audit #2
- [x] T8.2.6 — `nvarchar(max)` → MaxLength kısıtlamaları: Title (200), Description (2000), Email (256) — EF migration — Audit #15
- [x] T8.2.7 — Tag ve Email arama performansı: `ToLower()` sorgusunu kaldır, veri girişinde lowercase normalizasyonu ile DB-agnostic sargable Index Seek kullan — Audit #13
- [x] T8.2.8 — RFC 7807 (ProblemDetails) Standart Hata Formatı: Tüm hata yanıtlarının (`ExceptionHandlingMiddleware` ve `FluentValidation`) IETF standardı RFC 7807 `ProblemDetails` formatına eşitlenmesi (`type`, `title`, `status`, `detail`, `instance`, `errors`)
- [x] T8.2.9 — Düz Liste (Naked Array) Sarmalama: Doğrudan dizi `[...]` dönen tüm endpoint'lerin (`/subtasks`, `/tags`, `/shares`, `/transfer-requests/pending`) genişletilebilir generic `CollectionResponse<T>` (`{ items: [...] }`) formatına getirilmesi
    
### User Story 8.3 — Performans İyileştirmeleri
- [x] T8.3.1 — Read-only sorgulara `AsNoTracking()` ekle: `GetAccessibleByUserAsync`, `GetDeletedByOwnerAsync`, `GetAllAsync` (Tags), `GetByTaskIdAsync` (SubTasks) — Audit #14
- [x] T8.3.2 — Health Checks: `/health` (liveness) ve `/health/ready` (readiness - veritabanı bağlantı kontrolü) endpoint'leri ve yapılandırılmış JSON yanıtı
- [x] T8.3.3 — Yapılandırılmış Loglama: Serilog entegrasyonu ve önemli iş olaylarında yapılandırılmış log üretimi

---

## EPIC 9: İleri Seviye Özellikler & UX İyileştirmeleri (Post-MVP Enhancements)

### User Story 9.1 — Görev Yönetimi İleri Özellikler
- [x] T9.1.1 — Dinamik Filtreleme ve Arama (Query Object Pattern): `TodoItemFilterDto` ve `TaskFilterType` enum (`All`, `SharedByMe`, `SharedWithMe`, `OnlyMine`) oluşturulması; `GET /api/todoitems` endpoint'ine paylaşım tipi, arama (`searchTerm`), durum (`status`), tarih aralığı (`dueDateFrom`, `dueDateTo`) ve sıralama (`sortBy`, `sortOrder`) filtrelerinin tip güvenli ve dinamik `IQueryable` zinciri ile entegre edilmesi
- [x] T9.1.2 — Görev Önceliği: `TodoItemPriority` enum (`Low`, `Medium`, `High`, `Urgent`) eklenmesi ve önceliğe göre sıralama/filtreleme
- [x] T9.1.3 — Kategori / Proje / Liste Yapısı: Görevlerin "İş", "Kişisel", "Proje X" gibi listeler/klasörler altında gruplanabilmesi (`TodoList` entity & CRUD)

### User Story 9.2 — Otomasyon & Gerçek Zamanlı İletişim
- [x] T9.2.1 — Hatırlatıcı & Bildirim Arka Plan Servisi: `BackgroundService` veya Hangfire/Quartz ile `DueDate` yaklaşan veya geçen görevler için otomatik e-posta bildirimi gönderilmesi
- [x] T9.2.2 — Gerçek Zamanlı Güncellemeler (SignalR): Görev paylaşımında bir kullanıcı görevi güncellediğinde veya tamamladığında diğer kullanıcıların ekranının canlı senkronize olması
- [x] T9.2.3 — Aktivite & Denetim İzi (Audit Log): Görev üzerindeki tüm kritik değişikliklerin (oluşturuldu, paylaşıldı, tamamlandı, tarihi değiştirildi) geçmiş zaman çizelgesi olarak tutulması

---

## Öncelik Sırası Önerisi

1. **EPIC 0** (Setup) — ✅ Tamamlandı
2. **EPIC 1** (User/Auth) — ✅ Çekirdek tamamlandı, testler yeşil (6/6)
3. **EPIC 2** (Task CRUD) — ✅ Çekirdek tamamlandı, testler yeşil (8/8)
4. **EPIC 3** (SubTask) — ✅ Çekirdek tamamlandı, testler yeşil (9/9)
5. **EPIC 4** (Tag) — ✅ Çekirdek tamamlandı, testler yeşil (12/12)
6. **EPIC 4.5** (Güvenlik Altyapısı) — ✅ Tamamlandı
7. **EPIC 5** (TaskShare) — ✅ Tamamlandı (Paylaşım ve Devir onay sistemi)
8. **EPIC 6** (Authorization Refactor) — ✅ Tamamlandı (Merkezi yetkilendirme & SubTask silme kuralı)
9. **EPIC 7** (Kapsamlı Test & Kalite Matrisi) — ✅ Tamamlandı (test_matrix.md, Cascade testleri, smoke guide)
10. **EPIC 8** (Production Hardening) — ✅ Tamamlandı (Rate Limiting, CORS, Headers, Secrets, Options, MaxLength, Index Seek, RFC 7807, AsNoTracking, Health Checks, Serilog)
11. **EPIC 9** (İleri Seviye Özellikler & UX) — ✅ Tamamlandı
12. **EPIC 10** (Refactor & Hardening) — ✅ Tamamlandı
13. **EPIC 11** (Deployment & Production Hazırlığı) — ✅ Tamamlandı
14. **EPIC 12** (Architecture & Clean Code Refactoring) — 📋 Backlog'da bekliyor

---

## EPIC 10: Refactor & İleri Seviye İyileştirmeler (Production Hardening v2)

> **Kaynak:** 18 Eylül 2026 tarihli backlog analiz raporu. Bottleneck, güvenlik ve operasyon eksiklerinin giderilmesi.

### User Story 10.1 — Performans Darboğazlarının Giderilmesi (Bottleneck Fixes)
- [x] T10.1.1 — **Hatırlatıcı Servisi Batch Processing:** `TodoReminderService` içinde tüm görevleri tek seferde belleğe çekmek yerine sayfalama (batch) ile 100'erli gruplar halinde işlenmesi
- [x] T10.1.2 — **Alt Görev Sayım Optimizasyonu:** `SubTaskService.CreateAsync` içindeki limit kontrolünde `GetByTaskIdAsync` (tüm entity'leri yükler) yerine `CountByTaskIdAsync` (`SELECT COUNT(*)`) metodu eklenmesi
- [x] T10.1.3 — ~~**Aktivite Logu Asenkron Yazımı:** `_activityService.LogActivityAsync` çağrılarının ana iş akışından ayrılması — `Channel<T>` veya Background Queue ile fire-and-forget pattern uygulanması~~ (İptal: Mevcut ölçek için Over-engineering)
- [x] T10.1.4 — ~~**SignalR Bildirim Asenkronizasyonu:** `_notificationService.SendNotificationAsync` çağrılarının background queue'ya alınması~~ (İptal: Mevcut ölçek için Over-engineering)
- [x] T10.1.5 — ~~**SignalR Redis Backplane:** Çok sunuculu (scale-out) ortamda farklı sunuculardaki kullanıcıların birbirlerinin bildirimlerini alabilmesi için Redis backplane entegrasyonu~~ (İptal: Tek sunucu için Over-engineering)

### User Story 10.2 — Güvenlik İyileştirmeleri (Security Hardening v2)
- [x] T10.2.1 — **Hesap Kilitleme (Account Lockout):** `User` entity'sine `FailedLoginAttempts` (int) ve `LockoutEnd` (DateTime?) alanları eklenmesi; 5 ardışık yanlış denemede hesabın 15 dakika kilitlenmesi; başarılı girişte sayacın sıfırlanması
- [x] T10.2.2 — **JWT Token İptali (SecurityStamp):** `User` entity'sine `SecurityStamp` (Guid) eklenmesi; şifre değişikliği veya 2FA durumu değiştiğinde stamp yenilenmesi; JWT doğrulamada stamp kontrolü yapılması
- [x] T10.2.3 — **ForgotPassword SMTP Hata Sızıntısı Düzeltmesi:** Email gönderim hatasının try-catch ile sarılıp sadece loglanması; her koşulda 200 dönülmesi (user enumeration önleme)
- [x] T10.2.4 — **Refresh Token HttpOnly Cookie:** Refresh token'ın JSON body yerine `Set-Cookie: HttpOnly; Secure; SameSite=Strict` ile gönderilmesi; XSS durumunda token çalınmasının engellenmesi
- [x] T10.2.5 — **Hesap Silme Parola Doğrulaması:** `DELETE /users/me` endpoint'ine body'de `password` alanı eklenmesi; kritik işlem öncesi re-authentication zorunluluğu
- [x] T10.2.6 — **Global Authenticated Rate Limiting:** Giriş yapmış kullanıcılar için tüm endpoint'lere kullanıcı bazlı rate limit eklenmesi. Yazma (POST/PUT/PATCH/DELETE) için 30 istek/dk, Okuma (GET) için 60 istek/dk. Mevcut rate limit sadece 3 auth endpoint'inde (login, register, forgot-password) IP bazlı çalışıyor; geri kalan 30+ endpoint sınırsız — veri şişirme, DB yükü ve kaynak tüketimi riski mevcut.

### User Story 10.3 — Veri Bütünlüğü & Operasyon
- [x] T10.3.1 — **Kullanıcı Silme Transaction:** `UserRepository.Delete` içindeki birden fazla `ExecuteUpdate`/`ExecuteDelete` çağrısının tek bir `BeginTransactionAsync` bloğuna alınması
- [x] T10.3.2 — ~~**Otomatik Migration Pipeline:** CI/CD pipeline'ına `dotnet ef database update` adımının eklenmesi; deployment sırasında DB şemasının otomatik güncellenmesi~~ (İptal: Mevcut senaryo için Over-engineering)

---

## EPIC 11: Deployment & Production Hazırlığı (DevOps & Hosting)

> **Kaynak:** Canlıya alma öncesi güvenlik, ters proxy ve containerization gereksinimleri.
>
> 💡 **Ücretsiz Hosting Notu (Azure All-in-One):**
> Azure üzerinde hem Web API'yi hem veritabanını aynı veri merkezinde **sıfır maliyetle ($0)** çalıştırmak mümkündür:
> - **Backend API:** Azure App Service (**F1 Free Tier** — 1 GB RAM, 60 CPU dk/gün, otomatik HTTPS).
> - **Veritabanı:** Azure SQL Database (**Free Offer** — Her ay 100.000 vCore-saniye, 32 GB depolama).
> - **Avantaj:** Her iki servis de aynı Azure bölgesinde (örn. `West Europe / Batı Avrupa`) konumlandırıldığında harici ağ gecikmesi (latency) oluşmaz, bağlantı iç ağ hızında çalışır ve güvenlik duvarı ayarları pürüzsüz entegre olur.

### User Story 11.1 — Canlı Öncesi Kod İyileştirmeleri & DevOps
- [x] T11.1.1 — **`/test-signalr` Endpoint'inin Kısıtlanması:** `Program.cs` içindeki test HTML sayfasının yalnızca `app.Environment.IsDevelopment()` ortamında açılması; canlı ortamda dışarıya kapatılması
- [x] T11.1.2 — **Ters Proxy (Reverse Proxy) Desteği:** Nginx, Cloudflare, Traefik veya Cloud Load Balancer arkasında istemci IP ve HTTPS protokolünün doğru algılanabilmesi için `app.UseForwardedHeaders()` middleware entegrasyonu
- [x] T11.1.3 — **Otomatik Veritabanı Migration:** Canlı ortamda container başlatıldığında veritabanı şemasının otomatik güncellenmesi (`context.Database.MigrateAsync()`)
- [x] T11.1.4 — **Multi-stage Dockerfile:** .NET 10 Web API projesini derleyip optimize production image'ı üreten Dockerfile hazırlanması
- [x] T11.1.5 — **Production Environment Variables Şablonu:** Canlıda kullanılacak DB connection string, güçlü JWT Key, SMTP ve CORS domain ayarlarını içeren `.env.production.example` şablonunun oluşturulması

---

## EPIC 12: Architecture & Clean Code Refactoring (`Program.cs` Modülerleştirme)

> **Kaynak:** 25 Eylül 2026 tarihli kod kalitesi ve mimari analizi. `Program.cs` dosyasının "God File" olmaktan çıkarılıp kurumsal Extension Method Pattern ile modülerleştirilmesi (287 satırdan ~40 satıra indirilmesi).

### User Story 12.1 — Servis Kayıtlarının (Dependency Injection) Modülerleştirilmesi
- [x] T12.1.1 — **Katman Bazlı Servis Extension'ları:** `TodoApp.Application` içine `AddApplicationServices()` ve `TodoApp.Infrastructure` içine `AddInfrastructureServices()` extension metotlarının yazılarak `Program.cs`'teki 25+ satırlık `AddScoped` karmaşasının paketlenmesi
- [x] T12.1.2 — **Veritabanı Konfigürasyonunun İzolasyonu:** `Program.cs`'teki `AddDbContext` ve `EnableRetryOnFailure` bloğunun `AddDatabaseConfiguration(configuration)` extension metoduna taşınması

### User Story 12.2 — Güvenlik ve Kimlik Doğrulama Bloğunun İzolasyonu
- [ ] T12.2.1 — **JWT & SecurityStamp Extension'ı:** `AddJwtAuthentication(configuration, environment)` extension metodunun oluşturulması; options binding, fail-fast anahtar doğrulaması, SignalR query string token çözümleme (`OnMessageReceived`) ve veritabanı `SecurityStamp` doğrulama (`OnTokenValidated`) bloklarının `JwtAuthenticationExtensions.cs` içine taşınması
- [ ] T12.2.2 — **CORS ve Ters Proxy Yapılandırması:** `AddAppCors(configuration)` ve `AddAppForwardedHeaders()` extension metotları ile ağ yapılandırmalarının ayrıştırılması

### User Story 12.3 — API Davranışları, Swagger ve Pipeline Orkestrasyonu
- [ ] T12.3.1 — **Swagger & API Behavior İzolasyonu:** `AddSwaggerDocumentation()` ve RFC 7807 `InvalidModelStateResponseFactory` tanımlarının `ApiBehaviorExtensions.cs` içine taşınması
- [ ] T12.3.2 — **Veritabanı Migration & Startup Orkestrasyonu:** `Program.cs` sonundaki 20 satırlık scope ve `MigrateAsync` bloğunun `ApplyDatabaseMigrationsAsync()` extension metoduna dönüştürülmesi
- [ ] T12.3.3 — **`Program.cs` Sadeleştirmesi:** Tüm extension metotların `Program.cs` üzerinde çağrılarak ana dosyanın 287 satırdan 40-50 satırlık temiz bir orkestrasyona indirilmesi ve tüm testlerin (197 test) yeşil kaldığının doğrulanması

---

## Not: Kural Güncellemesi Gerekirse

Bu backlog'u uygularken yeni bir edge case veya çelişki fark edilirse, ilgili BR numarası burada ve `business-rules.md`'de güncellenmeli. Task'ı uygulayan kişi (sen, ekip veya AI) böyle bir durumla karşılaşırsa, kodlamaya devam etmeden önce kuralın netleştirilmesi gerekir.
