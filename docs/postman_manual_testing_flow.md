# 📮 TodoApp — Postman ile Uçtan Uca Manuel Test Akış Rehberi
### (Complete API Feature Verification & Testing Flow)

**Doküman Versiyonu:** 1.0  
**Tarih:** 26 Eylül 2026  
**Hedef Canlı URL:** `https://todoapp-api-gudhgje6bvfqg3ev.centralus-01.azurewebsites.net`  
**Yerel (Local) URL:** `http://localhost:5240`

---

## 📌 İçindekiler
1. [Postman Ortam Değişkenleri (Variables) Kurulumu](#1-postman-ortam-değişkenleri-variables-kurulumu)
2. [Adım 1: Sistem & Sağlık Kontrolleri (Health Checks)](#adım-1-sistem--sağlık-kontrolleri-health-checks)
3. [Adım 2: Kullanıcı Kaydı & Temel Oturum (Auth Lifecycle)](#adım-2-kullanıcı-kaydı--temel-oturum-auth-lifecycle)
4. [Adım 3: İki Adımlı Doğrulama (2FA) & Güvenli Bilet Mekanizması](#adım-3-iki-adımlı-doğrulama-2fa--güvenli-bilet-mekanizması)
5. [Adım 4: Hız Sınırı (Rate Limiting) Güvenlik Doğrulaması](#adım-4-hız-sınırı-rate-limiting-güvenlik-doğrulaması)
6. [Adım 5: Görev Listeleri Yönetimi (TodoLists CRUD)](#adım-5-görev-listeleri-yönetimi-todolists-crud)
7. [Adım 6: Görevler (TodoItems) & IDOR Güvenlik Doğrulaması](#adım-6-görevler-todoitems--idor-güvenlik-doğrulaması)
8. [Adım 7: Alt Görevler (SubTasks CRUD)](#adım-7-alt-görevler-subtasks-crud)
9. [Adım 8: Görev Paylaşımı & İşbirliği (Task Sharing)](#adım-8-görev-paylaşımı--işbirliği-task-sharing)
10. [Adım 9: Görev Sahiplik Devri (Ownership Transfer)](#adım-9-görev-sahiplik-devri-ownership-transfer)
11. [Adım 10: Çöp Kutusu (Trash), Geri Yükleme & Kalıcı Silme](#adım-10-çöp-kutusu-trash-geri-yükleme--kalıcı-silme)
12. [Adım 11: Hesap Silme & Soft-Delete SQL Bütünlüğü](#adım-11-hesap-silme--soft-delete-sql-bütünlüğü)

---

## 1. Postman Ortam Değişkenleri (Variables) Kurulumu

Postman'de bir **Environment** oluşturup şu değişkenleri tanımlayın. İstekler arasında dönen ID ve Token değerlerini buraya kaydederek hızlıca ilerleyebilirsiniz:

| Değişken Adı | Varsayılan Değer | Açıklama |
|---|---|---|
| `baseUrl` | `https://todoapp-api-gudhgje6bvfqg3ev.centralus-01.azurewebsites.net` | Canlı veya local API adresi |
| `user1_email` | `testuser1@example.com` | 1. Kullanıcı e-postası |
| `user1_password` | `Password123!` | 1. Kullanıcı parolası |
| `user1_token` | *(Boş)* | User 1'in Bearer JWT Token'ı |
| `user1_refreshToken`| *(Boş)* | User 1'in Refresh Token'ı |
| `user2_email` | `testuser2@example.com` | 2. Kullanıcı e-postası (İşbirliği/Devir için) |
| `user2_token` | *(Boş)* | User 2'nin Bearer JWT Token'ı |
| `twoFactorToken` | *(Boş)* | 2FA girişinde üretilen geçici bilet |
| `totpSecret` | *(Boş)* | 2FA kurulum secret key'i |
| `listId` | *(Boş)* | Oluşturulan listenin GUID'i |
| `taskId` | *(Boş)* | Oluşturulan görevin GUID'i |
| `subTaskId` | *(Boş)* | Oluşturulan alt görevin GUID'i |
| `transferRequestId` | *(Boş)* | Devir talebinin GUID'i |

---

## Adım 1: Sistem & Sağlık Kontrolleri (Health Checks)

### 1.1 Liveness Kontrolü
* **Metot & URL:** `GET {{baseUrl}}/health`
* **Headers:** Yok
* **Beklenen Durum:** `200 OK`
* **Örnek Yanıt:**
  ```json
  {
    "status": "Healthy",
    "totalDuration": "00:00:00.001...",
    "entries": {}
  }
  ```

### 1.2 Readiness (Veritabanı Hazırlık) Kontrolü
* **Metot & URL:** `GET {{baseUrl}}/health/ready`
* **Headers:** Yok
* **Beklenen Durum:** `200 OK`
* **Örnek Yanıt:**
  ```json
  {
    "status": "Healthy",
    "totalDuration": "00:00:00.015...",
    "entries": {
      "database": {
        "status": "Healthy",
        "description": "Veritabanı bağlantısı başarılı.",
        "duration": "00:00:00.014..."
      }
    }
  }
  ```

---

## Adım 2: Kullanıcı Kaydı & Temel Oturum (Auth Lifecycle)

### 2.1 User 1 Kaydı (Register)
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/register`
* **Headers:** `Content-Type: application/json`
* **Body (Raw JSON):**
  ```json
  {
    "email": "testuser1@example.com",
    "password": "Password123!"
  }
  ```
* **Beklenen Durum:** `200 OK`
* **İşlem:** Dönen yanıttaki `token` değerini `user1_token`, `refreshToken` değerini `user1_refreshToken` olarak kaydedin.

### 2.2 User 2 Kaydı (İşbirliği ve Devir Testleri İçin)
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/register`
* **Headers:** `Content-Type: application/json`
* **Body (Raw JSON):**
  ```json
  {
    "email": "testuser2@example.com",
    "password": "Password123!"
  }
  ```
* **Beklenen Durum:** `200 OK`
* **İşlem:** Dönen yanıttaki `token` değerini `user2_token` olarak kaydedin.

### 2.3 User 1 Girişi (Login)
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/login`
* **Body (Raw JSON):**
  ```json
  {
    "email": "testuser1@example.com",
    "password": "Password123!"
  }
  ```
* **Beklenen Durum:** `200 OK` (Henüz 2FA açılmadığı için doğrudan tam yetkili `token` döner).

### 2.4 Token Yenileme (Refresh Token Rotasyonu)
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/refresh`
* **Body (Raw JSON):**
  ```json
  {
    "refreshToken": "{{user1_refreshToken}}"
  }
  ```
* **Beklenen Durum:** `200 OK`
* **Kontrol:** Yeni bir `token` ve farklı bir `refreshToken` döner (Eski refresh token tek kullanımlık olduğu için geçersiz kalır).

### 2.5 Şifre Değiştirme (Change Password)
* **Metot & URL:** `PUT {{baseUrl}}/api/Auth/change-password`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "currentPassword": "Password123!",
    "newPassword": "NewPassword123!"
  }
  ```
* **Beklenen Durum:** `204 NoContent`
* *(Not: Teste devam edebilmek için aynı istekle parolayı tekrar `Password123!` yapabilirsiniz).*

---

## Adım 3: İki Adımlı Doğrulama (2FA) & Güvenli Bilet Mekanizması

### 3.1 2FA Etkinleştirme İsteği
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/2fa/enable`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK`
* **Örnek Yanıt:**
  ```json
  {
    "secret": "JBSWY3DPEHPK3PXP",
    "qrCodeUri": "otpauth://totp/TodoApp:testuser1%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=TodoApp"
  }
  ```
* **İşlem:** Dönen `secret` değerini telefonunuzdaki **Google Authenticator** uygulamasına ("Anahtar girin / Enter setup key") ekleyin veya online bir TOTP üreticisine yapıştırın. `totpSecret` olarak kaydedin.

### 3.2 2FA Kurulumunu Tamamlama (Verify Setup)
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/2fa/verify`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "code": "123456" // Authenticator'daki o anki 6 haneli kod
  }
  ```
* **Beklenen Durum:** `200 OK` (`"İki adımlı doğrulama başarıyla aktifleştirildi."`)

### 3.3 2FA'lı Kullanıcı Girişi (Adım 1: Parola Kontrolü)
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/login`
* **Body (Raw JSON):**
  ```json
  {
    "email": "testuser1@example.com",
    "password": "Password123!"
  }
  ```
* **Beklenen Durum:** `200 OK`
* **Beklenen Yanıt Yapısı:**
  ```json
  {
    "requiresTwoFactor": true,
    "userId": "...",
    "twoFactorToken": "eyJhbGciOiJIUzI1NiIs..."
  }
  ```
* **Güvenlik Doğrulaması:** Parola sonrası `token` alanı **boştur**. Sadece 5 dakika geçerli `twoFactorToken` döner! `twoFactorToken` değerini kaydedin.

### 3.4 [Güvenlik Testi] 2FA Geçici Biletinin İzolasyon Kontrolü
* **Metot & URL:** `GET {{baseUrl}}/api/TodoLists`
* **Headers:** `Authorization: Bearer {{twoFactorToken}}`
* **Beklenen Durum:** `401 Unauthorized`  
  *(Geçici 2FA bileti API endpoint'lerinde yetkisizdir; token confusion engellenmiştir).*

### 3.5 2FA Kodunu Girerek Tam Oturum Açma (Adım 2: 2FA Doğrulama)
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/login-2fa`
* **Body (Raw JSON):**
  ```json
  {
    "twoFactorToken": "{{twoFactorToken}}",
    "code": "123456" // Authenticator'daki güncel 6 haneli kod
  }
  ```
* **Beklenen Durum:** `200 OK`
* **Beklenen Yanıt:** Tam yetkili `token` ve `refreshToken` döner. Yeni `user1_token` olarak kaydedin.

### 3.6 2FA'yı Devre Dışı Bırakma (İsteğe Bağlı)
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/2fa/disable`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):** `{ "code": "123456" }`
* **Beklenen Durum:** `200 OK`

---

## Adım 4: Hız Sınırı (Rate Limiting) Güvenlik Doğrulaması

* **Metot & URL:** `POST {{baseUrl}}/api/Auth/login`
* **Body (Raw JSON):** Hatalı şifreyle 6 kez art arda hızlıca istek atın:
  ```json
  {
    "email": "testuser1@example.com",
    "password": "WrongPassword999!"
  }
  ```
* **Beklenen Durum:** İlk 5 istek `400 Bad Request` döner. **6. istekte API `429 Too Many Requests` döner.**
* **Örnek ProblemDetails Yanıtı:**
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc6585#section-4",
    "title": "Çok Fazla İstek Yapıldı",
    "status": 429,
    "detail": "İstek limiti aşıldı. Lütfen daha sonra tekrar deneyin.",
    "instance": "/api/Auth/login"
  }
  ```

---

## Adım 5: Görev Listeleri Yönetimi (TodoLists CRUD)

### 5.1 Yeni Liste Oluşturma
* **Metot & URL:** `POST {{baseUrl}}/api/TodoLists`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "name": "İş Projeleri",
    "colorCode": "#3498db"
  }
  ```
* **Beklenen Durum:** `201 Created`
* **İşlem:** Dönen `id` değerini `listId` olarak kaydedin.

### 5.2 Listeleri Listeleme
* **Metot & URL:** `GET {{baseUrl}}/api/TodoLists`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK` (Oluşturulan liste dizide görünür).

### 5.3 Liste Güncelleme
* **Metot & URL:** `PUT {{baseUrl}}/api/TodoLists/{{listId}}`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "name": "Yazılım & Ar-Ge",
    "colorCode": "#2ecc71"
  }
  ```
* **Beklenen Durum:** `200 OK`

---

## Adım 6: Görevler (TodoItems) & IDOR Güvenlik Doğrulaması

### 6.1 Kendi Listesine Bağlı Görev Oluşturma
* **Metot & URL:** `POST {{baseUrl}}/api/TodoItems`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "title": "Siber Güvenlik İncelemesi Yap",
    "description": "Pentest bulgularını kontrol et",
    "dueDate": "2026-10-01T18:00:00Z",
    "priority": 2, // High (0: Low, 1: Medium, 2: High, 3: Critical)
    "todoListId": "{{listId}}"
  }
  ```
* **Beklenen Durum:** `201 Created`
* **İşlem:** Dönen `id` değerini `taskId` olarak kaydedin.

### 6.2 [IDOR Testi] User 2'nin User 1'e Ait Listeye Görev Bağlama Girişimi
* **Metot & URL:** `POST {{baseUrl}}/api/TodoItems`
* **Headers:** `Authorization: Bearer {{user2_token}}`  *(User 2 token'ı!)*
* **Body (Raw JSON):**
  ```json
  {
    "title": "Kötü Niyetli Görev Girişimi",
    "todoListId": "{{listId}}" // User 1'in listesi!
  }
  ```
* **Beklenen Durum:** **`400 Bad Request`**  
* **Yanıt Mesajı:** `"Belirtilen görev listesi bulunamadı veya erişim yetkiniz yok."` *(IDOR engellendi!)*

### 6.3 Görevleri Filtreleme & Sayfalama
* **Metot & URL:** `GET {{baseUrl}}/api/TodoItems?page=1&pageSize=10&status=0&priority=2&todoListId={{listId}}`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK` (Sayfalanmış formatta göreviniz döner).

### 6.4 Görevi Güncelleme
* **Metot & URL:** `PUT {{baseUrl}}/api/TodoItems/{{taskId}}`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "title": "Siber Güvenlik İncelemesi (Tamamlandı Aşamasında)",
    "description": "Rapor hazırlandı",
    "dueDate": "2026-10-02T12:00:00Z",
    "priority": 3, // Critical
    "todoListId": "{{listId}}"
  }
  ```
* **Beklenen Durum:** `200 OK`

### 6.5 Görevi Tamamlama / Tekrar Açma (Toggle)
* **Metot & URL:** `PATCH {{baseUrl}}/api/TodoItems/{{taskId}}/complete`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK` (`status: 1` Completed olur).  
*(Tekrar gönderirseniz `status: 0` Open olur).*

### 6.6 Görev Aktivite Günlüğünü İnceleme
* **Metot & URL:** `GET {{baseUrl}}/api/TodoItems/{{taskId}}/activities`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK` (Oluşturuldu, Güncellendi, Tamamlandı kayıtları kronolojik görünür).

---

## Adım 7: Alt Görevler (SubTasks CRUD)

### 7.1 Alt Görev Ekleme
* **Metot & URL:** `POST {{baseUrl}}/api/todoitems/{{taskId}}/subtasks`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "title": "SSL/TLS sertifika süresini doğrula"
  }
  ```
* **Beklenen Durum:** `201 Created`
* **İşlem:** Dönen `id` değerini `subTaskId` olarak kaydedin.

### 7.2 Görevin Alt Görevlerini Listeleme
* **Metot & URL:** `GET {{baseUrl}}/api/todoitems/{{taskId}}/subtasks`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK`

### 7.3 Alt Görevi Tamamlama
* **Metot & URL:** `PATCH {{baseUrl}}/api/subtasks/{{subTaskId}}/complete`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK` (`isCompleted: true`)

---

## Adım 8: Görev Paylaşımı & İşbirliği (Task Sharing)

### 8.1 Görevi User 2 ile Paylaşma
* **Metot & URL:** `POST {{baseUrl}}/api/todoitems/{{taskId}}/shares`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "email": "testuser2@example.com"
  }
  ```
* **Beklenen Durum:** `200 OK` (`"Görev başarıyla paylaşıldı."`)

### 8.2 Paylaşılan Kullanıcıları Listeleme
* **Metot & URL:** `GET {{baseUrl}}/api/todoitems/{{taskId}}/shares`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK` (Listede `testuser2@example.com` görünür).

### 8.3 User 2'nin Paylaşılan Görevi Okuması
* **Metot & URL:** `GET {{baseUrl}}/api/TodoItems/{{taskId}}`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** `200 OK` (User 2 görevi görebilir).

### 8.4 [Yetki Kuralı] User 2'nin Görevi Tamamlamayı Denemesi
* **Metot & URL:** `PATCH {{baseUrl}}/api/TodoItems/{{taskId}}/complete`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** **`404 Not Found`**  
  *(BR-025 gereği paylaşılan kullanıcı ana görevi tamamlayamaz, yalnızca sahip tamamlayabilir).*

---

## Adım 9: Görev Sahiplik Devri (Ownership Transfer)

### 9.1 Devir Talebi Başlatma (User 1 -> User 2)
* **Metot & URL:** `POST {{baseUrl}}/api/todoitems/{{taskId}}/transfer-requests`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "targetUserEmail": "testuser2@example.com"
  }
  ```
* **Beklenen Durum:** `200 OK`
* **İşlem:** Dönen `id` değerini `transferRequestId` olarak kaydedin.

### 9.2 Bekleyen Devir Talebini İnceleme (User 2)
* **Metot & URL:** `GET {{baseUrl}}/api/transfer-requests/pending`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** `200 OK` (Bekleyen devir talebi listede görünür).

### 9.3 Devir Talebini Kabul Etme (User 2)
* **Metot & URL:** `POST {{baseUrl}}/api/transfer-requests/{{transferRequestId}}/accept`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** `200 OK` (`"Görev devir talebi başarıyla kabul edildi ve sahiplik aktarıldı."`)
* **Doğrulama:** Artık görevin yeni sahibi User 2'dir. User 2 görevi tamamlayabilir ve silebilir.

---

## Adım 10: Çöp Kutusu (Trash), Geri Yükleme & Kalıcı Silme

### 10.1 Görevi Silme (Soft Delete)
* **Metot & URL:** `DELETE {{baseUrl}}/api/TodoItems/{{taskId}}`
* **Headers:** `Authorization: Bearer {{user2_token}}` *(Yeni sahip User 2)*
* **Beklenen Durum:** `204 NoContent`

### 10.2 Çöp Kutusunu Görüntüleme
* **Metot & URL:** `GET {{baseUrl}}/api/TodoItems/trash?page=1&pageSize=10`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** `200 OK` (Silinen görev çöp kutusundadır).

### 10.3 Çöpten Geri Yükleme (Restore)
* **Metot & URL:** `POST {{baseUrl}}/api/TodoItems/{{taskId}}/restore`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** `200 OK` (Görev aktif listeye döner).

### 10.4 Kalıcı Silme (Permanent Delete)
1. Tekrar soft delete yapın: `DELETE {{baseUrl}}/api/TodoItems/{{taskId}}` -> `204 NoContent`
2. Kalıcı silin:
   * **Metot & URL:** `DELETE {{baseUrl}}/api/TodoItems/{{taskId}}/permanent`
   * **Headers:** `Authorization: Bearer {{user2_token}}`
   * **Beklenen Durum:** `204 NoContent`
3. Çöp kutusunu kontrol edin: `GET {{baseUrl}}/api/TodoItems/trash` -> Boş döner.

---

## Adım 11: Hesap Silme & Soft-Delete SQL Bütünlüğü

Bu adım, **Aşama 2'de düzelttiğimiz SQL 547 Foreign Key çökmesinin** canlı doğrulamasını yapar.

### 11.1 Ön Hazırlık: Soft-Delete Edilmiş Görev Bırakma
1. User 1 ile yeni bir görev oluşturun: `POST /api/TodoItems` -> `taskId_temp`
2. Görevi tamamlayın: `PATCH /api/TodoItems/{{taskId_temp}}/complete` (`CompletedByUserId = User1`)
3. Görevi silin: `DELETE /api/TodoItems/{{taskId_temp}}` (`DeletedByUserId = User1`, `IsDeleted = true`)

### 11.2 Hesabı Silme (Delete Account)
* **Metot & URL:** `DELETE {{baseUrl}}/api/Users/me`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "password": "Password123!"
  }
  ```
* **Beklenen Durum:** **`204 NoContent`**
* **Kritik Doğrulama:**
  - Eski kodda bu istek `500 Internal Server Error (SQL 547 constraint violation)` verip çöküyordu.
  - Artık `.IgnoreQueryFilters()` sayesinde soft-delete edilmiş görevlerin `CompletedByUserId` ve `DeletedByUserId` referansları başarıyla temizlenir ve hesap **hatasız silinir.**
