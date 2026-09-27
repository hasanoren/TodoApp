# 📮 TodoApp — Postman ile Uçtan Uca Manuel Test Akış Rehberi
### (Complete API Feature Verification & Testing Flow)

**Doküman Versiyonu:** 1.0  
**Tarih:** 26 Eylül 2026  
**Hedef Canlı URL:** `https://your-api.azurewebsites.net` (veya Azure App Service URL'niz)  
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
| `baseUrl` | `https://your-api.azurewebsites.net` | Canlı veya local API adresi |
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
* **İşlem:** Dönen yanıttaki yeni `token` ve `refreshToken` değerleriyle Postman değişkenlerinizi güncelleyin (`user1_token`, `user1_refreshToken`).

#### 2.4.1 [Güvenlik Testi] Eski Refresh Token'ın Tekrar Kullanımı (Reuse Detection)
* **Açıklama:** Refresh token'lar tek kullanımlıktır (rotasyon). Az önce harcadığınız eski refresh token ile tekrar istek atmayı deneyin.
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/refresh`
* **Body (Raw JSON):** *(2.4 adımında az önce yenilediğiniz eski refresh token)*
* **Beklenen Durum:** **`400 Bad Request`**
* **Beklenen Yanıt:** `"Geçersiz veya süresi dolmuş refresh token."`
* **Sonuç:** Token çalınsa dahi saldırganın eski refresh token'ı tekrar kullanamayacağı kanıtlanır.

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

---

### 2.6 [Güvenlik Testleri] Şifre Değişikliği Sonrası Token İptal Kontrolleri

Şifre değiştirildiği anda sistemimiz iki katmanlı oturum iptal mekanizmasını tetikler:
1. `user.SecurityStamp = Guid.NewGuid()` -> Mevcut tüm JWT access token'ları derhal geçersiz kılınır.
2. `refreshToken.IsRevoked = true` -> Tüm açık cihazlardaki refresh token'lar kalıcı olarak iptal edilir.

Aşağıdaki adımlarla bu güvenlik mekanizmalarını bizzat doğrulayabilirsiniz:

#### 2.6.1 Eski Access Token İptal Kontrolü (SecurityStamp Doğrulaması)
* **Açıklama:** Şifre değişmeden önce aldığınız `{{user1_token}}`'ın süresi (15 dk) henüz dolmamış olsa bile anında reddedildiğini doğrulayın.
* **Metot & URL:** `GET {{baseUrl}}/api/TodoLists`
* **Headers:** `Authorization: Bearer {{user1_token}}` *(Şifre değiştirmeden önceki eski token)*
* **Beklenen Durum:** **`401 Unauthorized`**
* **Beklenen Yanıt Başlığı:** `WWW-Authenticate: Bearer error="invalid_token", error_description="Oturum süresi doldu veya güvenlik bilgileri değişti."`
* **Sonuç:** Eski JWT'nin anında geçersiz kılındığı ve korumalı endpoint'lere erişilemediği kanıtlanır.

#### 2.6.2 Eski Refresh Token İptal Kontrolü (Revocation Doğrulaması)
* **Açıklama:** Şifre değişimi öncesindeki `{{user1_refreshToken}}` ile yeni token üretilemeyeceğini test edin.
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/refresh`
* **Body (Raw JSON):**
  ```json
  {
    "refreshToken": "{{user1_refreshToken}}"
  }
  ```
* **Beklenen Durum:** **`400 Bad Request`**
* **Beklenen Yanıt:**
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "Doğrulama Hatası",
    "status": 400,
    "detail": "Geçersiz veya süresi dolmuş refresh token."
  }
  ```

#### 2.6.3 Eski Şifreyle Girişin Engellenmesi
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/login`
* **Body (Raw JSON):**
  ```json
  {
    "email": "testuser1@example.com",
    "password": "Password123!"
  }
  ```
* **Beklenen Durum:** **`400 Bad Request`** (`"E-posta veya şifre hatalı."`)

#### 2.6.4 Yeni Şifreyle Giriş & Yeni Oturum Başlatma
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/login`
* **Body (Raw JSON):**
  ```json
  {
    "email": "testuser1@example.com",
    "password": "NewPassword123!"
  }
  ```
* **Beklenen Durum:** `200 OK`
* **İşlem:** Dönen yanıttaki yeni `token` değerini `user1_token`, yeni `refreshToken` değerini `user1_refreshToken` ortam değişkenlerine kaydedin.
* **Şifre Güncellemesi:** Postman ortam değişkeninizdeki `user1_password` değerini `NewPassword123!` olarak güncelleyin. *(Sonraki adımlarda bu yeni token ve güncel şifre kullanılacaktır).*

---

### 2.7 Oturumu Kapatma (Logout) & Refresh Token İptal Kontrolü
* **Açıklama:** Kullanıcı "Çıkış Yap" dediğinde o oturuma ait refresh token kalıcı olarak iptal edilir.
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/logout`
* **Body (Raw JSON):**
  ```json
  {
    "refreshToken": "{{user1_refreshToken}}"
  }
  ```
* **Beklenen Durum:** `204 NoContent`
* **Doğrulama (Revoke Kontrolü):** Aynı `{{user1_refreshToken}}` ile tekrar `POST {{baseUrl}}/api/Auth/refresh` çağrıldığında **`400 Bad Request`** ("Geçersiz veya süresi dolmuş refresh token.") döner.
* **Hazırlık:** Test akışının sonraki adımlarına devam edebilmek için `POST {{baseUrl}}/api/Auth/login` ile (şifre: `NewPassword123!`) tekrar giriş yapıp güncel `user1_token` ve `user1_refreshToken` değerlerinizi Postman ortamınıza kaydedin.

---

### 2.8 [Güvenlik Testi] Kullanıcı Varlığını Sızdırmama Kontrolü (User Enumeration Protection)
* **Açıklama:** Saldırganların sistemde hangi e-postaların kayıtlı olduğunu tespit edememesi (User Enumeration) için, sistemde kayıtlı olmayan hayalet bir e-posta adresiyle şifre sıfırlama talebinde bulunun.
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/forgot-password`
* **Body (Raw JSON):**
  ```json
  {
    "email": "nonexistent_ghost_999@example.com"
  }
  ```
* **Beklenen Durum:** **`200 OK`**
* **Beklenen Yanıt:** `{"message": "Eğer bu e-posta adresi kayıtlıysa, şifre sıfırlama bağlantısı gönderildi."}`
* **Sonuç:** Kullanıcı olsa da olmasa da sistem aynı yanıtı döner, kullanıcı varlığı sızdırılmaz.

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

#### 3.2.1 [Güvenlik Testi] 2FA Aktifleşince Eski 1FA JWT'nin Anında İptal Edilmesi
* **Açıklama:** 2FA aktifleştirildiği anda sistem `SecurityStamp` değerini yeniler. Henüz 2FA olmadan alınmış olan `{{user1_token}}` derhal geçersiz kılınmalıdır.
* **Metot & URL:** `GET {{baseUrl}}/api/TodoLists`
* **Headers:** `Authorization: Bearer {{user1_token}}` *(2FA öncesi eski token)*
* **Beklenen Durum:** **`401 Unauthorized`**
* **Sonuç:** 2FA açıldığı an, daha önce çalınmış olabilecek tek faktörlü eski JWT'ler anında çöp olur!

### 3.3 2FA'lı Kullanıcı Girişi (Adım 1: Parola Kontrolü)
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/login`
* **Body (Raw JSON):**
  ```json
  {
    "email": "testuser1@example.com",
    "password": "{{user1_password}}" // Güncel şifre (NewPassword123!)
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

### 3.5 [Güvenlik Testi] Hatalı 2FA Kodu & Hız Sınırı (Brute-Force Koruması)
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/login-2fa`
* **Body (Raw JSON):**
  ```json
  {
    "twoFactorToken": "{{twoFactorToken}}",
    "code": "000000" // Kasıtlı yanlış kod
  }
  ```
* **Beklenen Durum:** **`400 Bad Request`** (`"Geçersiz doğrulama kodu."`)
* **Not:** Eğer 5 kez üst üste yanlış kod gönderirseniz, `auth-2fa-verify` kuralı devreye girer ve **`429 Too Many Requests`** döner.

### 3.6 2FA Kodunu Girerek Tam Oturum Açma (Adım 2: 2FA Doğrulama)
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/login-2fa`
* **Body (Raw JSON):**
  ```json
  {
    "twoFactorToken": "{{twoFactorToken}}",
    "code": "123456" // Authenticator'daki güncel 6 haneli kod
  }
  ```
* **Beklenen Durum:** `200 OK`
* **Beklenen Yanıt:** Tam yetkili `token` ve `refreshToken` döner. Yeni `user1_token` ve `user1_refreshToken` olarak kaydedin.

### 3.7 [Güvenlik Testi] 2FA'yı Devre Dışı Bırakma (Disable 2FA) & Token İptal Kontrolü
* **Açıklama:** 2FA devre dışı bırakılırken de kod doğrulaması zorunludur ve güvenlik profili değiştiği için mevcut JWT oturumu yine sonlandırılır.
* **Metot & URL:** `POST {{baseUrl}}/api/Auth/2fa/disable`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "code": "123456" // Güncel 6 haneli kod
  }
  ```
* **Beklenen Durum:** `200 OK`
* **Güvenlik Doğrulaması:** Eski token ile tekrar `GET /api/TodoLists` atın -> **`401 Unauthorized`** döner (`SecurityStamp` yenilendi).
* **Sonraki Adımlara Hazırlık:** Testlere devam edebilmek için `POST /api/Auth/login` ile doğrudan tek adımlı giriş yapın (çünkü 2FA kapandı) ve yeni `user1_token`'ınızı kaydedin.

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

#### 5.4 [IDOR Testi] User 2'nin User 1'e Ait Listeyi Okuma ve Güncelleme Engeli
* **Açıklama:** Kullanıcıların sadece kendi oluşturdukları listeleri yönetebildiğini, başkasının liste ID'si ile veri çekemeyeceğini test edin.
* **Metot & URL:** `GET {{baseUrl}}/api/TodoLists/{{listId}}`
* **Headers:** `Authorization: Bearer {{user2_token}}` *(User 2 token'ı!)*
* **Beklenen Durum:** **`400 Bad Request`**
* **Beklenen Yanıt:** `"Liste bulunamadı veya erişim yetkiniz yok."`
* *(Aynı şekilde `PUT {{baseUrl}}/api/TodoLists/{{listId}}` isteği de User 2 için `400 Bad Request` döner).*

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

### 6.3 [Varlık Sızdırmama Testi] User 2'nin User 1'in Görevini Okuma/Değiştirme Girişimi
* **Açıklama:** Saldırgan sistemde var olan bir görevin GUID'sini tahmin etse veya bilse bile, sistem `403 Forbidden` yerine **`404 Not Found`** döner. Böylece saldırgan o görevin sistemde var olup olmadığını dahi anlayamaz (Resource Enumeration koruması).
* **Metot & URL:** `GET {{baseUrl}}/api/TodoItems/{{taskId}}`
* **Headers:** `Authorization: Bearer {{user2_token}}`  *(User 2 token'ı!)*
* **Beklenen Durum:** **`404 Not Found`** (`"Görev bulunamadı."`)
* *(Aynı şekilde `PUT {{baseUrl}}/api/TodoItems/{{taskId}}` güncellemesi de `404 Not Found` döner).*

### 6.4 Görevleri Filtreleme & Sayfalama
* **Metot & URL:** `GET {{baseUrl}}/api/TodoItems?page=1&pageSize=10&status=0&priority=2&todoListId={{listId}}`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK` (Sayfalanmış formatta göreviniz döner).

### 6.5 Görevi Güncelleme
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

### 6.6 Görevi Tamamlama / Tekrar Açma (Toggle)
* **Metot & URL:** `PATCH {{baseUrl}}/api/TodoItems/{{taskId}}/complete`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK` (`status: 1` Completed olur).  
*(Tekrar gönderirseniz `status: 0` Open olur).*

### 6.7 Görev Aktivite Günlüğünü İnceleme
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

#### 7.2 [Yetki Kuralı] User 2'nin User 1'in Görevine Alt Görev Ekleyememesi
* **Metot & URL:** `POST {{baseUrl}}/api/todoitems/{{taskId}}/subtasks`
* **Headers:** `Authorization: Bearer {{user2_token}}` *(User 2 token'ı)*
* **Body (Raw JSON):**
  ```json
  {
    "title": "İzinsiz Alt Görev Denemesi"
  }
  ```
* **Beklenen Durum:** **`404 Not Found`** (`"Görev bulunamadı."`)

### 7.3 Görevin Alt Görevlerini Listeleme
* **Metot & URL:** `GET {{baseUrl}}/api/todoitems/{{taskId}}/subtasks`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK`

### 7.4 Alt Görevi Tamamlama (Toggle)
* **Metot & URL:** `PATCH {{baseUrl}}/api/subtasks/{{subTaskId}}/complete`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK` (`status: 1` Completed)

### 7.5 İkinci Bir Alt Görev Oluşturup Silme (SubTask Delete)
1. Yeni bir alt görev ekleyin: `POST {{baseUrl}}/api/todoitems/{{taskId}}/subtasks` (Title: "Geçici Alt Görev") -> `subTaskId_temp`
2. Alt görevi silin:
   * **Metot & URL:** `DELETE {{baseUrl}}/api/subtasks/{{subTaskId_temp}}`
   * **Headers:** `Authorization: Bearer {{user1_token}}`
   * **Beklenen Durum:** `204 NoContent`

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

#### 8.1.1 [Validasyon Testi] Görevi Kendisiyle Paylaşma Engeli
* **Metot & URL:** `POST {{baseUrl}}/api/todoitems/{{taskId}}/shares`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):** `{ "email": "testuser1@example.com" }`
* **Beklenen Durum:** **`400 Bad Request`** (`"Kullanıcı görevi kendisiyle paylaşamaz."`)

#### 8.1.2 [Validasyon Testi] Var Olmayan Kullanıcıyla Paylaşım
* **Metot & URL:** `POST {{baseUrl}}/api/todoitems/{{taskId}}/shares`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):** `{ "email": "ghost_nonexistent@example.com" }`
* **Beklenen Durum:** **`404 Not Found`** (`"Paylaşılmak istenen kullanıcı bulunamadı."`)

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

### 8.5 [Yetki Kuralı] User 2'nin Paylaşılan Görevi Silememesi
* **Metot & URL:** `DELETE {{baseUrl}}/api/TodoItems/{{taskId}}`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** **`404 Not Found`**  
  *(BR-008 & BR-026 gereği yalnızca görev sahibi silebilir; paylaşılan kullanıcı 404 alır).*

### 8.6 [Yetki Kuralı] User 2'nin Görevi Başkasıyla Paylaşamaması (Re-share Engeli)
* **Metot & URL:** `POST {{baseUrl}}/api/todoitems/{{taskId}}/shares`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Body (Raw JSON):** `{ "email": "testuser3@example.com" }`
* **Beklenen Durum:** **`404 Not Found`**  
  *(BR-013 gereği yalnızca görev sahibi paylaşım yapabilir).*

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

#### 9.1.1 [Validasyon Testi] Görevi Kendine Devretme Engeli
* **Metot & URL:** `POST {{baseUrl}}/api/todoitems/{{taskId}}/transfer-requests`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):** `{ "targetUserEmail": "testuser1@example.com" }`
* **Beklenen Durum:** **`400 Bad Request`** (`"Görevin sahipliğini zaten elinizde bulunduruyorsunuz."`)

#### 9.1.2 [Çakışma Testi] Aynı Göreve İkinci Devir Talebi Engeli (Conflict)
* **Açıklama:** Bekleyen bir talep varken aynı görev için tekrar devir talebi açmayı deneyin.
* **Metot & URL:** `POST {{baseUrl}}/api/todoitems/{{taskId}}/transfer-requests`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):** `{ "targetUserEmail": "testuser2@example.com" }`
* **Beklenen Durum:** **`409 Conflict`** (`"Bu görev için zaten bekleyen bir devir talebi bulunmaktadır."`)

### 9.2 Bekleyen Devir Talebini İnceleme (User 2)
* **Metot & URL:** `GET {{baseUrl}}/api/transfer-requests/pending`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** `200 OK` (Bekleyen devir talebi listede görünür).

### 9.3 Devir Talebini Kabul Etme (User 2)
* **Metot & URL:** `POST {{baseUrl}}/api/transfer-requests/{{transferRequestId}}/accept`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** `200 OK` (`"Görev devir talebi başarıyla kabul edildi ve sahiplik aktarıldı."`)
* **Doğrulama:** Artık görevin yeni sahibi User 2'dir. User 1 ise otomatik olarak paylaşılanlar listesine alınmıştır.

#### 9.4 [Sahiplik Geçişi Doğrulaması] Eski Sahip User 1'in Görevi Silememesi
* **Açıklama:** Sahiplik devredildikten sonra User 1'in görev silme yetkisinin düştüğünü doğrulayın.
* **Metot & URL:** `DELETE {{baseUrl}}/api/TodoItems/{{taskId}}`
* **Headers:** `Authorization: Bearer {{user1_token}}` *(Eski sahip!)*
* **Beklenen Durum:** **`404 Not Found`**

#### 9.5 [Tekrar İşlem Engeli] Zaten Kabul Edilmiş Talebin Tekrar Yanıtlanamaması
* **Metot & URL:** `POST {{baseUrl}}/api/transfer-requests/{{transferRequestId}}/accept`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** **`400 Bad Request`** (`"Bu devir talebi zaten yanıtlanmış veya iptal edilmiş."`)

---

## Adım 10: Çöp Kutusu (Trash), Geri Yükleme & Kalıcı Silme

#### 10.1 [Güvenlik Kuralı] Aktif Görevi Doğrudan Kalıcı Silme Engeli
* **Açıklama:** Çöp kutusuna taşınmamış (aktif) bir görevin doğrudan kalıcı olarak silinmesi engellenmelidir.
* **Metot & URL:** `DELETE {{baseUrl}}/api/TodoItems/{{taskId}}/permanent`
* **Headers:** `Authorization: Bearer {{user2_token}}` *(Yeni sahip User 2)*
* **Beklenen Durum:** **`400 Bad Request`** (`"Yalnızca çöp kutusundaki görevler kalıcı olarak silinebilir."`)

#### 10.2 [Mantık Kuralı] Çöp Kutusunda Olmayan Aktif Görevi Geri Yükleme Engeli
* **Metot & URL:** `POST {{baseUrl}}/api/TodoItems/{{taskId}}/restore`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** **`400 Bad Request`** (`"Bu görev zaten aktif durumda."`)

### 10.3 Görevi Çöp Kutusuna Taşıma (Soft Delete)
* **Metot & URL:** `DELETE {{baseUrl}}/api/TodoItems/{{taskId}}`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** **`204 NoContent`**
* **Doğrulama:** `GET {{baseUrl}}/api/TodoItems` çağrıldığında bu görev aktif listeden kaybolur.

### 10.4 Çöp Kutusunu Görüntüleme
* **Metot & URL:** `GET {{baseUrl}}/api/TodoItems/trash?page=1&pageSize=10`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** `200 OK` (Silinen görev çöp kutusunda listelenir).

### 10.5 Çöpten Geri Yükleme (Restore)
* **Metot & URL:** `POST {{baseUrl}}/api/TodoItems/{{taskId}}/restore`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** `200 OK` (Görev tekrar aktif listeye döner).

### 10.6 Kalıcı Silme (Permanent Delete)
1. Tekrar soft delete yapın: `DELETE {{baseUrl}}/api/TodoItems/{{taskId}}` (Headers: User 2) -> `204 NoContent`
2. Kalıcı olarak silin:
   * **Metot & URL:** `DELETE {{baseUrl}}/api/TodoItems/{{taskId}}/permanent`
   * **Headers:** `Authorization: Bearer {{user2_token}}`
   * **Beklenen Durum:** **`204 NoContent`**
3. Çöp kutusunu kontrol edin: `GET {{baseUrl}}/api/TodoItems/trash` -> Boş döner.

#### 10.7 [Kalıcı Silme Doğrulaması] Silinen Görevin Geri Yüklenememesi
* **Metot & URL:** `POST {{baseUrl}}/api/TodoItems/{{taskId}}/restore`
* **Headers:** `Authorization: Bearer {{user2_token}}`
* **Beklenen Durum:** **`404 Not Found`**

---

## Adım 11: Etiketler (Tags) & Rol Bazlı Yetkilendirme (RBAC)

### 11.1 [RBAC Yetki Testi] Standart Kullanıcının Etiket Oluşturma Engeli
* **Açıklama:** Sistemimizde etiket oluşturma yetkisi yalnızca `Admin` rolündedir (BR-022). Standart kullanıcının etiket oluşturamadığını test edin.
* **Metot & URL:** `POST {{baseUrl}}/api/tags`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "name": "Kritik Güvenlik",
    "color": "#e74c3c"
  }
  ```
* **Beklenen Durum:** **`403 Forbidden`** *(Standart kullanıcıya etiket oluşturma kapalıdır).*

### 11.2 Genel Etiketleri Listeleme
* **Metot & URL:** `GET {{baseUrl}}/api/tags`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Beklenen Durum:** `200 OK` (Mevcut global etiketler listelenir).

---

## Adım 12: Hesap Silme & Soft-Delete SQL Bütünlüğü

Bu adım, **Aşama 2'de düzelttiğimiz SQL 547 Foreign Key çökmesinin** ve kritik işlem öncesi parola doğrulama (Re-authentication) kuralının canlı doğrulamasını yapar.

### 12.1 Ön Hazırlık: Soft-Delete Edilmiş Görev Bırakma
1. User 1 ile yeni bir görev oluşturun: `POST /api/TodoItems` -> `taskId_temp`
2. Görevi tamamlayın: `PATCH /api/TodoItems/{{taskId_temp}}/complete` (`CompletedByUserId = User1`)
3. Görevi silin: `DELETE /api/TodoItems/{{taskId_temp}}` (`DeletedByUserId = User1`, `IsDeleted = true`)

#### 12.2 [Güvenlik Testi] Hatalı Parolayla Hesap Silme Reddi (Re-Authentication Koruması)
* **Açıklama:** Hesap silme gibi geri dönülemez işlemlerde parola teyidi zorunludur. Yanlış şifreyle silme girişimini test edin.
* **Metot & URL:** `DELETE {{baseUrl}}/api/Users/me`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):** `{ "password": "WrongPassword999!" }`
* **Beklenen Durum:** **`400 Bad Request`** (`"Mevcut şifre hatalı."`)

### 12.3 Hesabı Başarıyla Silme (Delete Account)
* **Metot & URL:** `DELETE {{baseUrl}}/api/Users/me`
* **Headers:** `Authorization: Bearer {{user1_token}}`
* **Body (Raw JSON):**
  ```json
  {
    "password": "{{user1_password}}"
  }
  ```
* **Beklenen Durum:** **`204 NoContent`**
* **Kritik Doğrulama:**
  - Eski kodda bu istek `500 Internal Server Error (SQL 547 constraint violation)` verip çöküyordu.
  - Artık `.IgnoreQueryFilters()` sayesinde soft-delete edilmiş görevlerin `CompletedByUserId` ve `DeletedByUserId` referansları başarıyla temizlenir ve hesap **hatasız silinir.**

#### 12.4 [Hesap Silme Sonrası Kontroller]
1. Silinen hesabın eski token'ı ile korumalı bir istek atın:  
   * `GET {{baseUrl}}/api/TodoLists` (Headers: `Bearer {{user1_token}}`)  
   * 👉 **Beklenen Durum:** **`401 Unauthorized`**
2. Silinen hesapla tekrar giriş yapmayı deneyin:  
   * `POST {{baseUrl}}/api/Auth/login` (email: `testuser1@example.com`)  
   * 👉 **Beklenen Durum:** **`400 Bad Request`** (`"E-posta veya şifre hatalı."`)
