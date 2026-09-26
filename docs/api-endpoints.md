# 📚 TodoApp — REST API Endpoint Dokümantasyonu

**Son Güncelleme:** 26 Eylül 2026  
**Toplam Endpoint:** 48 (REST Endpoints + Realtime WebSocket Hub)  
**Canlı Base URL (Azure):** `https://todoapp-api-gudhgje6bvfqg3ev.centralus-01.azurewebsites.net`  
**Yerel Base URL:** `https://localhost:5240`  
**Swagger UI:** [https://localhost:5240/swagger](https://localhost:5240/swagger)  
**Kimlik Doğrulama:** Korumalı tüm endpoint'ler `Authorization: Bearer <JWT_TOKEN>` HTTP başlığı gerektirir.

---

## 📑 İçindekiler
1. [Kimlik Doğrulama & Oturum (`AuthController`)](#-1-kimlik-doğrulama--oturum-authcontroller)
2. [Görev Yönetimi (`TodoItemsController`)](#-2-görev-yönetimi-todoitemscontroller)
3. [Alt Görevler (`SubTasksController`)](#-3-alt-görevler-subtaskscontroller)
4. [Etiketler (`TagsController`)](#-4-etiketler-tagscontroller)
5. [Görev Listeleri (`TodoListsController`)](#-5-görev-listeleri-todolistscontroller)
6. [Görev Paylaşımı & İşbirliği (`TaskSharesController`)](#-6-görev-paylaşımı--işbirliği-tasksharescontroller)
7. [Sahiplik Devri (`TransferRequestsController`)](#-7-sahiplik-devri-transferrequestscontroller)
8. [Kullanıcı Hesabı (`UsersController`)](#-8-kullanıcı-hesabı-userscontroller)
9. [SignalR Gerçek Zamanlı Bildirimler (`/hubs/todo`)](#-9-signalr-gerçek-zamanlı-hub)
10. [Sistem Sağlığı (`HealthChecks`)](#-10-sistem-sağlığı-health-checks)
11. [Standart Yanıt & Hata Formatları (RFC 7807)](#-11-standart-yanıt--hata-formatları)

---

## 🔐 1. Kimlik Doğrulama & Oturum (`AuthController`)

### 1.1 Kullanıcı Kaydı
* **Endpoint:** `POST /api/Auth/register`
* **Yetki:** Anonim
* **Rate Limit:** 3 istek / dakika (`auth-register`)
* **İş Kuralı:** `BR-001` (Email unique), `BR-005` (Varsayılan rol: User)
* **İstek:**
  ```json
  {
    "email": "kullanici@ornek.com",
    "password": "Password123!"
  }
  ```
* **Yanıt (200 OK):**
  ```json
  {
    "userId": "4e8d5ea2-3c12-4f89-8d7b-123456789abc",
    "email": "kullanici@ornek.com",
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "a1b2c3d4e5..."
  }
  ```

### 1.2 Giriş Yapma
* **Endpoint:** `POST /api/Auth/login`
* **Yetki:** Anonim
* **Rate Limit:** 5 istek / dakika (`auth-login`)
* **İstek:**
  ```json
  {
    "email": "kullanici@ornek.com",
    "password": "Password123!"
  }
  ```
* **Yanıt (200 OK — Standart):**
  ```json
  {
    "userId": "4e8d5ea2-3c12-4f89-8d7b-123456789abc",
    "email": "kullanici@ornek.com",
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "x9y8z7w6v5..."
  }
  ```
* **Yanıt (200 OK — 2FA Aktif ise):**
  ```json
  {
    "requiresTwoFactor": true,
    "twoFactorToken": "temp-auth-token-for-totp-verification"
  }
  ```

### 1.3 İki Adımlı Giriş (2FA Verification)
* **Endpoint:** `POST /api/Auth/login-2fa`
* **Yetki:** Anonim
* **İstek:**
  ```json
  {
    "twoFactorToken": "temp-auth-token-for-totp-verification",
    "code": "123456"
  }
  ```
* **Yanıt (200 OK):** `AuthResponse` (`userId`, `email`, `token`, `refreshToken`).

### 1.4 Token Yenileme
* **Endpoint:** `POST /api/Auth/refresh`
* **Yetki:** Anonim
* **İstek:**
  ```json
  {
    "refreshToken": "x9y8z7w6v5..."
  }
  ```
* **Yanıt (200 OK):** Yeni `token` ve rotasyona uğramış yeni `refreshToken`.

### 1.5 Çıkış Yapma
* **Endpoint:** `POST /api/Auth/logout`
* **Yetki:** Anonim (veya Authenticated)
* **İstek:**
  ```json
  {
    "refreshToken": "x9y8z7w6v5..."
  }
  ```
* **Yanıt:** `204 No Content` (İlgili refresh token DB'de `RevokedAt` ile iptal edilir).

### 1.6 Şifremi Unuttum
* **Endpoint:** `POST /api/Auth/forgot-password`
* **Yetki:** Anonim
* **Rate Limit:** 2 istek / dakika (`auth-forgot-password`)
* **İstek:** `{ "email": "kullanici@ornek.com" }`
* **Yanıt (200 OK):** `{ "message": "Eğer bu e-posta adresi kayıtlıysa, şifre sıfırlama bağlantısı gönderildi." }`

### 1.7 Şifre Sıfırlama
* **Endpoint:** `POST /api/Auth/reset-password`
* **Yetki:** Anonim
* **İstek:**
  ```json
  {
    "email": "kullanici@ornek.com",
    "token": "base64-url-encoded-reset-token",
    "newPassword": "NewPassword123!"
  }
  ```
* **Yanıt (200 OK):** `{ "message": "Şifreniz başarıyla değiştirildi." }` (Tüm aktif refresh token'lar iptal edilir).

### 1.8 Şifre Değiştirme (Oturum Açıkken)
* **Endpoint:** `PUT /api/Auth/change-password`
* **Yetki:** `[Authorize]`
* **İstek:**
  ```json
  {
    "currentPassword": "OldPassword123!",
    "newPassword": "NewPassword123!"
  }
  ```
* **Yanıt:** `204 No Content`

### 1.9 İki Adımlı Doğrulamayı Aktifleştirme İsteği
* **Endpoint:** `POST /api/Auth/2fa/enable`
* **Yetki:** `[Authorize]`
* **Yanıt (200 OK):**
  ```json
  {
    "sharedKey": "JBSWY3DPEHPK3PXP",
    "authenticatorUri": "otpauth://totp/TodoApp:kullanici@ornek.com?secret=JBSWY3DPEHPK3PXP&issuer=TodoApp"
  }
  ```

### 1.10 İki Adımlı Doğrulamayı Onaylama & Tamamlama
* **Endpoint:** `POST /api/Auth/2fa/verify`
* **Yetki:** `[Authorize]`
* **İstek:** `{ "code": "123456" }`
* **Yanıt (200 OK):** `{ "message": "İki adımlı doğrulama başarıyla aktifleştirildi." }`

### 1.11 İki Adımlı Doğrulamayı Devre Dışı Bırakma
* **Endpoint:** `POST /api/Auth/2fa/disable`
* **Yetki:** `[Authorize]`
* **İstek:** `{ "code": "123456" }`
* **Yanıt (200 OK):** `{ "message": "İki adımlı doğrulama devre dışı bırakıldı." }`

---

## 📋 2. Görev Yönetimi (`TodoItemsController`)

### 2.1 Görev Oluşturma
* **Endpoint:** `POST /api/TodoItems`
* **Yetki:** `[Authorize]`
* **İş Kuralı:** `BR-006` (Owner zorunlu), `BR-011` (Varsayılan durum: Open)
* **İstek:**
  ```json
  {
    "title": "Backend Mimarisi Raporu",
    "description": "Clean Architecture dokümantasyonunu hazırla",
    "dueDate": "2026-10-01T18:00:00Z",
    "priority": 2,
    "todoListId": "b1a2c3d4-e5f6-7a8b-9c0d-112233445566"
  }
  ```
* **Yanıt (201 Created):** `TodoItemResponse`

### 2.2 Görevleri Listeleme (Dinamik Filtreleme, Sıralama, Sayfalama)
* **Endpoint:** `GET /api/TodoItems`
* **Yetki:** `[Authorize]`
* **Query Parametreleri:**
  * `FilterType`: `0` (All - Sahip veya Paylaşılan), `1` (OnlyMine), `2` (SharedWithMe), `3` (SharedByMe)
  * `Search`: Metin arama (Title veya Description içinde)
  * `Status`: `0` (Open), `1` (Completed)
  * `Priority`: `0` (Low), `1` (Medium), `2` (High), `3` (Urgent)
  * `TodoListId`: Belirli bir listeye göre filtreleme
  * `DueDateFrom` / `DueDateTo`: Tarih aralığı
  * `SortBy`: `createdAt`, `dueDate`, `title`, `priority`
  * `SortOrder`: `asc`, `desc`
  * `Page`: Sayfa numarası (varsayılan: 1)
  * `PageSize`: Sayfa boyutu (varsayılan: 20, maks: 100)
* **Yanıt (200 OK):** `PaginatedResponse<TodoItemResponse>`

### 2.3 Görev Detayı Getirme
* **Endpoint:** `GET /api/TodoItems/{id}`
* **Yetki:** `[Authorize]` (Sahip veya Paylaşılan, aksi halde `404`)
* **Yanıt (200 OK):** `TodoItemResponse` (Alt görevler, etiketler ve paylaşılan kullanıcılar dahil)

### 2.4 Görev Güncelleme
* **Endpoint:** `PUT /api/TodoItems/{id}`
* **Yetki:** `[Authorize]` (`BR-025`: Yalnızca Görev Sahibi güncelleyebilir)
* **Yanıt (200 OK):** Güncellenmiş `TodoItemResponse`

### 2.5 Görev Tamamlama / Geri Açma (Toggle)
* **Endpoint:** `PATCH /api/TodoItems/{id}/complete`
* **Yetki:** `[Authorize]` (`BR-025`: Yalnızca Görev Sahibi tamamlayabilir)
* **Yanıt (200 OK):** `TodoItemResponse`

### 2.6 Görevi Çöp Kutusuna Taşıma (Soft Delete)
* **Endpoint:** `DELETE /api/TodoItems/{id}`
* **Yetki:** `[Authorize]` (`BR-008`: Yalnızca Görev Sahibi silebilir)
* **Yanıt:** `204 No Content`

### 2.7 Görevi Kalıcı Olarak Silme (Hard Delete)
* **Endpoint:** `DELETE /api/TodoItems/{id}/permanent`
* **Yetki:** `[Authorize]` (`BR-010`: Yalnızca çöp kutusundaki görevler sahip tarafından kalıcı silinebilir)
* **Yanıt:** `204 No Content`

### 2.8 Görevi Çöp Kutusundan Geri Yükleme (Restore)
* **Endpoint:** `POST /api/TodoItems/{id}/restore`
* **Yetki:** `[Authorize]` (`BR-010`: Yalnızca sahip restore edebilir)
* **Yanıt (200 OK):** `TodoItemResponse`

### 2.9 Çöp Kutusunu Listeleme
* **Endpoint:** `GET /api/TodoItems/trash`
* **Yetki:** `[Authorize]` (Kullanıcının soft-delete edilmiş görevleri)
* **Yanıt (200 OK):** `PaginatedResponse<TodoItemResponse>`

### 2.10 Görevin Aktivite Geçmişini (Audit Log) Getirme
* **Endpoint:** `GET /api/TodoItems/{id}/activities`
* **Yetki:** `[Authorize]` (Sahip veya paylaşılan kullanıcı)
* **Yanıt (200 OK):** `CollectionResponse<TodoItemActivityResponse>`

---

## 🧩 3. Alt Görevler (`SubTasksController`)

### 3.1 Alt Görev Ekleme
* **Endpoint:** `POST /api/todoitems/{taskId}/subtasks`
* **Yetki:** `[Authorize]` (`BR-012`: Üst görev silinmemiş olmalı; Sahip veya Paylaşılan ekleyebilir)
* **İstek:** `{ "title": "Unit testleri tamamla" }`
* **Yanıt (201 Created):** `SubTaskResponse`

### 3.2 Göreve Ait Alt Görevleri Listeleme
* **Endpoint:** `GET /api/todoitems/{taskId}/subtasks`
* **Yetki:** `[Authorize]` (`BR-018`: Üst görev silinmemiş olmalı)
* **Yanıt (200 OK):** `CollectionResponse<SubTaskResponse>`

### 3.3 Alt Görevi Tamamlama / Geri Açma
* **Endpoint:** `PATCH /api/subtasks/{id}/complete`
* **Yetki:** `[Authorize]` (`BR-020`: Sahip veya Paylaşılan tamamlayabilir; `BR-017`: Üst görevi etkilemez)
* **Yanıt (200 OK):** `SubTaskResponse`

### 3.4 Alt Görevi Silme
* **Endpoint:** `DELETE /api/subtasks/{id}`
* **Yetki:** `[Authorize]` (`BR-020`: YALNIZCA ana görevin sahibi silebilir, aksi halde 404)
* **Yanıt:** `204 No Content`

---

## 🏷️ 4. Etiketler (`TagsController`)

### 4.1 Global Etiket Oluşturma
* **Endpoint:** `POST /api/tags`
* **Yetki:** `[Authorize(Roles = "Admin")]` (`BR-022`: Sadece Admin rolü etiket oluşturabilir)
* **İstek:** `{ "name": "Frontend" }`
* **Yanıt (201 Created):** `TagResponse`

### 4.2 Tüm Etiketleri Listeleme
* **Endpoint:** `GET /api/tags`
* **Yetki:** `[Authorize]`
* **Yanıt (200 OK):** `CollectionResponse<TagResponse>`

### 4.3 Etikete Göre Görevleri Listeleme
* **Endpoint:** `GET /api/tags/{tagId}/todoitems`
* **Yetki:** `[Authorize]` (Kullanıcının o etikete sahip aktif görevleri)
* **Yanıt (200 OK):** `PaginatedResponse<TodoItemResponse>`

### 4.4 Göreve Atanmış Etiketleri Listeleme
* **Endpoint:** `GET /api/todoitems/{taskId}/tags`
* **Yetki:** `[Authorize]`
* **Yanıt (200 OK):** `CollectionResponse<TagResponse>`

### 4.5 Göreve Etiket Bağlama
* **Endpoint:** `POST /api/todoitems/{taskId}/tags/{tagId}`
* **Yetki:** `[Authorize]` (`BR-024`: Aynı etiket aynı göreve yalnızca 1 kez eklenebilir)
* **Yanıt (200 OK):** `{ "message": "Etiket göreve başarıyla bağlandı." }`

### 4.6 Görevden Etiket Kaldırma
* **Endpoint:** `DELETE /api/todoitems/{taskId}/tags/{tagId}`
* **Yetki:** `[Authorize]` (`BR-023`: Etiket sistemden silinmez, sadece bağ kopar)
* **Yanıt:** `204 No Content`

---

## 📁 5. Görev Listeleri (`TodoListsController`)

### 5.1 Liste Oluşturma
* **Endpoint:** `POST /api/TodoLists`
* **Yetki:** `[Authorize]`
* **İstek:** `{ "name": "İş Projeleri", "colorCode": "#FF5733" }`
* **Yanıt (201 Created):** `TodoListResponse`

### 5.2 Kullanıcının Listelerini Getirme
* **Endpoint:** `GET /api/TodoLists`
* **Yetki:** `[Authorize]`
* **Yanıt (200 OK):** `CollectionResponse<TodoListResponse>`

### 5.3 Liste Detayı
* **Endpoint:** `GET /api/TodoLists/{id}`
* **Yetki:** `[Authorize]`
* **Yanıt (200 OK):** `TodoListResponse`

### 5.4 Liste Güncelleme
* **Endpoint:** `PUT /api/TodoLists/{id}`
* **Yetki:** `[Authorize]`
* **İstek:** `{ "name": "Kişisel Gelişim", "colorCode": "#33C1FF" }`
* **Yanıt (200 OK):** `TodoListResponse`

### 5.5 Liste Silme
* **Endpoint:** `DELETE /api/TodoLists/{id}`
* **Yetki:** `[Authorize]`
* **Yanıt:** `204 No Content`

---

## 👥 6. Görev Paylaşımı & İşbirliği (`TaskSharesController`)

### 6.1 Görevi Başka Bir Kullanıcıyla Paylaşma
* **Endpoint:** `POST /api/todoitems/{taskId}/shares`
* **Yetki:** `[Authorize]` (`BR-013`: Sadece Owner paylaşabilir, `BR-004`: Kendisiyle paylaşamaz, `BR-014`: Idempotent)
* **İstek:** `{ "email": "arkadas@ornek.com" }`
* **Yanıt (200 OK):** `{ "message": "Görev başarıyla paylaşıldı." }`

### 6.2 Görevin Paylaşıldığı Kullanıcıları Listeleme
* **Endpoint:** `GET /api/todoitems/{taskId}/shares`
* **Yetki:** `[Authorize]` (Sahip veya Paylaşılan kullanıcı)
* **Yanıt (200 OK):** `CollectionResponse<SharedUserResponse>`

### 6.3 Görev Sahibinin Paylaşımı İptal Etmesi
* **Endpoint:** `DELETE /api/todoitems/{taskId}/shares/{userId}`
* **Yetki:** `[Authorize]` (`BR-013`: Yalnızca Görev Sahibi kaldırabilir)
* **Yanıt:** `204 No Content`

### 6.4 Paylaşılan Kullanıcının Kendi İsteğiyle Ayrılması
* **Endpoint:** `DELETE /api/todoitems/{taskId}/shares/me`
* **Yetki:** `[Authorize]` (`BR-028`: Paylaşılan kullanıcı kendi isteğiyle ayrılır)
* **Yanıt:** `204 No Content`

---

## 🔄 7. Sahiplik Devri (`TransferRequestsController`)

### 7.1 Sahiplik Devir Talebi Başlatma
* **Endpoint:** `POST /api/todoitems/{taskId}/transfer-requests`
* **Yetki:** `[Authorize]` (`BR-030`: Yalnızca Görev Sahibi başlatabilir)
* **İstek:** `{ "targetUserEmail": "yeni_sahip@ornek.com" }`
* **Yanıt (200 OK):** `TransferRequestResponse`

### 7.2 Onay Bekleyen Devir Taleplerini Listeleme
* **Endpoint:** `GET /api/transfer-requests/pending`
* **Yetki:** `[Authorize]` (Giriş yapan kullanıcının onayını bekleyen talepler)
* **Yanıt (200 OK):** `CollectionResponse<TransferRequestResponse>`

### 7.3 Devir Talebini Kabul Etme
* **Endpoint:** `POST /api/transfer-requests/{requestId}/accept`
* **Yetki:** `[Authorize]` (Yalnızca hedef kullanıcı kabul edebilir)
* **Yanıt (200 OK):** `{ "message": "Görev devir talebi başarıyla kabul edildi ve sahiplik aktarıldı." }`

### 7.4 Devir Talebini Reddetme
* **Endpoint:** `POST /api/transfer-requests/{requestId}/reject`
* **Yetki:** `[Authorize]` (Hedef kullanıcı)
* **Yanıt (200 OK):** `{ "message": "Görev devir talebi reddedildi." }`

### 7.5 Devir Talebini İptal Etme
* **Endpoint:** `POST /api/transfer-requests/{requestId}/cancel`
* **Yetki:** `[Authorize]` (Devir başlatan sahip)
* **Yanıt (200 OK):** `{ "message": "Görev devir talebi başarıyla iptal edildi." }`

---

## 👤 8. Kullanıcı Hesabı (`UsersController`)

### 8.1 Hesabı ve İlgili Tüm Verileri Kalıcı Olarak Silme
* **Endpoint:** `DELETE /api/users/me`
* **Yetki:** `[Authorize]`
* **İş Kuralı:** `BR-002` (Kullanıcıya ait görevler cascade silinir), `BR-003` (Paylaşımlar silinir)
* **İstek:** `{ "password": "Password123!" }`
* **Yanıt:** `204 No Content`

---

## ⚡ 9. SignalR Gerçek Zamanlı Hub

* **Hub URL:** `/hubs/todo`
* **Protokol:** WebSockets (fallback: Long Polling)
* **Kimlik Doğrulama:** Query parametresi `?access_token=<JWT_TOKEN>` üzerinden taşınır.
* **İstemcinin Dinleyebileceği Olaylar (Events):**
  * `ReceiveNotification(string title, string message)`: Kullanıcıya özel anlık bildirim.
  * `TaskShared(Guid taskId, string taskTitle)`: Kullanıcıyla yeni bir görev paylaşıldığında.
  * `TaskUpdated(Guid taskId)`: Paylaşılan görev güncellendiğinde veya tamamlandığında.
  * `TransferRequested(Guid requestId, string taskTitle)`: Kullanıcıya sahiplik devri teklifi geldiğinde.

---

## 🩺 10. Sistem Sağlığı (Health Checks)

* **Liveness:** `GET /health` ➔ `200 OK` (API sunucusunun çalıştığını doğrular).
* **Readiness:** `GET /health/ready` ➔ `200 OK` (SQL Server DB bağlantısını doğrular).

---

## 📐 11. Standart Yanıt & Hata Formatları

### 11.1 Sayfalanmış Yanıt (`PaginatedResponse<T>`)
```json
{
  "items": [ /* DTO nesneleri */ ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 45,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

### 11.2 Dizi Yanıt (`CollectionResponse<T>`)
```json
{
  "items": [ /* DTO nesneleri */ ],
  "count": 5
}
```

### 11.3 RFC 7807 Standart Hata Yanıtı (`ProblemDetails`)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "Bir veya daha fazla doğrulama hatası oluştu.",
  "errors": {
    "Email": ["Geçerli bir e-posta adresi girilmelidir."],
    "Password": ["Şifre en az 8 karakter olmalıdır."]
  }
}
```
