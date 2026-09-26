# 📱 TodoApp — Flutter Mobil Geliştirici El Kitabı (API Reference & Architecture)

Bu doküman, **TodoApp** projesinin Flutter mobil uygulamasını geliştirecek yazılımcı için eksiksiz bir rehber ve referanstır. API mimarisi, kimlik doğrulama, tüm endpoint'ler (istek/yanıt gövdeleri), enum değerleri, iş kuralları ve hata yönetimi ayrıntılı olarak yer almaktadır.

---

## 🌐 1. Sunucu ve Bağlantı Bilgileri

| Kaynak | URL | Açıklama |
|---|---|---|
| **Canlı API Base URL** | `https://your-api.azurewebsites.net` | Tüm HTTP istekleri için kök adres |
| **SignalR WebSocket Hub** | `https://your-api.azurewebsites.net/hubs/todo` | Canlı bildirimler ve senkronizasyon |
| **Health Check (Liveness)**| `https://your-api.azurewebsites.net/health` | API'nin çalışıp çalışmadığını denetler |
| **Health Check (Readiness)**| `https://your-api.azurewebsites.net/health/ready` | DB bağlantısını doğrular |

> **Header Bilgileri:**
> * Tüm JSON isteklerinde: `Content-Type: application/json`
> * Korumalı (Authorize) endpoint'lerde: `Authorization: Bearer <access_token>`

---

## 🔐 2. Kimlik Doğrulama (Auth Flow) ve Token Mimarisi

Mobil uygulamada oturum yönetimi **JWT (Access Token) + Refresh Token** mekanizması ile çalışır. Cookie kullanılmaz, token'lar doğrudan JSON gövdesinde iletilir.

### Token Akışı (Adım Adım):
1. Kullanıcı `POST /api/Auth/login` veya `POST /api/Auth/register` yapar.
2. Dönen yanıttan `token` (Access Token - 60 dk geçerli) ve `refreshToken` (7 gün geçerli) alınır.
3. Bu iki değer cihazın güvenli hafızasında (`flutter_secure_storage`) saklanır.
4. Sonraki tüm korumalı isteklere HTTP header olarak `Authorization: Bearer <token>` eklenir.
5. Bir istek **`401 Unauthorized`** dönerse:
   * Kullanıcıya hissettirmeden (silent) arka planda `POST /api/Auth/refresh` endpoint'ine `{ "refreshToken": "<mevcut_refresh_token>" }` atılır.
   * Dönen yeni `token` ve `refreshToken` saklanır, başarısız olan istek yeni token ile **otomatik tekrarlanır**.
   * Eğer refresh isteği de başarısız olursa (`400` / `401`), kullanıcının oturumu düşürülür ve Login ekranına yönlendirilir.
6. Çıkış yaparken `POST /api/Auth/logout` ile mevcut `refreshToken` sunucuya bildirilir ve cihaz hafızasından token'lar temizlenir.

### Güvenlik Önlemleri (Mobil Geliştiricinin Bilmesi Gerekenler):
* **Rate Limiting:** Login endpoint'ine aynı IP'den 1 dakikada en fazla **5 istek** atılabilir. Aşılırsa `429 Too Many Requests` döner.
* **Account Lockout:** Arka arkaya 5 yanlış şifre denemesinde hesap **15 dakika** kilitlenir.
* **Security Stamp:** Kullanıcı şifresini değiştirdiğinde tüm eski token'lar sunucuda anında iptal edilir (Access token süresi dolmamış olsa bile bir sonraki istekte 401 döner).

---

## 📦 3. Standart Yanıt Sarmalayıcıları (Response Envelopes)

API'de tutarlı veri yönetimi için 4 temel yanıt formatı vardır:

### 3.1. Sayfalanmış Listeler (`PaginatedResponse<T>`)
Görev listelerinde (`GET /api/TodoItems`, `GET /api/TodoItems/trash`, `GET /api/tags/{id}/todoitems`) kullanılır:
```json
{
  "items": [ /* T tipinde nesneler */ ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 45,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

### 3.2. Düz Listeler (`CollectionResponse<T>`)
Alt görevler, etiketler, paylaşılan kullanıcılar, listeler ve aktivite geçmişinde kullanılır:
```json
{
  "items": [ /* T tipinde nesneler */ ]
}
```

### 3.3. Aksiyon ve Onay Mesajları (`MessageResponse`)
Veri dönmeyen ancak kullanıcıya gösterilecek onay mesajı içeren isteklerde kullanılır:
```json
{
  "message": "Görev başarıyla paylaşıldı."
}
```

### 3.4. Hata Yanıtları (IETF RFC 7807 `ProblemDetails`)
İstisnasız tüm `4xx` ve `5xx` hata yanıtlarında bu format döner:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Geçersiz İstek",
  "status": 400,
  "detail": "Bir veya daha fazla alanda doğrulama hatası oluştu.",
  "instance": "/api/TodoItems",
  "traceId": "00-4b8c...",
  "errors": {
    "Title": ["Görev başlığı boş bırakılamaz.", "En fazla 200 karakter olabilir."]
  }
}
```

---

## 🏷️ 4. Enum Tanımları ve Değerleri

Dart modellerini oluştururken bu enumları birebir kullanın:

```dart
// Görev Önceliği
enum TodoItemPriority {
  low(1),
  medium(2),
  high(3);

  final int value;
  const TodoItemPriority(this.value);
}

// Görev Durumu
enum TodoItemStatus {
  open(0),
  completed(1);

  final int value;
  const TodoItemStatus(this.value);
}

// Alt Görev Durumu
enum SubTaskStatus {
  open(0),
  completed(1);

  final int value;
  const SubTaskStatus(this.value);
}

// Devir Talebi Durumu
enum TransferRequestStatus {
  pending(0),
  accepted(1),
  rejected(2),
  cancelled(3);

  final int value;
  const TransferRequestStatus(this.value);
}

// Görev Filtreleme Tipi (GET /api/TodoItems?filterType=...)
enum TaskFilterType {
  all(0),          // Kendi görevleri + Paylaşılanlar (Varsayılan)
  onlyMine(1),     // Yalnızca kullanıcının sahip olduğu görevler
  sharedWithMe(2), // Kendisiyle paylaşılan görevler
  sharedByMe(3);   // Sahip olduğu ve en az biriyle paylaştığı görevler

  final int value;
  const TaskFilterType(this.value);
}
```

---

## 🛠️ 5. Tüm API Endpoint'leri ve Detayları

---

### 5.1. Authentication (Kimlik Doğrulama)

#### `POST /api/Auth/register`
Yeni kullanıcı kaydı oluşturur.
* **Body:**
  ```json
  {
    "email": "user@example.com",
    "password": "Password123!" // min 8, max 128 karakter, en az 1 büyük, 1 küçük, 1 rakam
  }
  ```
* **Response (200 OK):**
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "token": "eyJhbGciOi...",
    "refreshToken": "4a7f9b2c...",
    "requiresTwoFactor": false
  }
  ```

#### `POST /api/Auth/login`
Giriş yapar.
* **Body:**
  ```json
  {
    "email": "user@example.com",
    "password": "Password123!"
  }
  ```
* **Response (200 OK):**
  * Eğer 2FA kapalıysa: `requiresTwoFactor: false`, `token` ve `refreshToken` döner.
  * Eğer 2FA açıksa: `requiresTwoFactor: true`, `userId` döner, `token` boş gelir. Bu durumda kullanıcıyı 2FA kod ekranına yönlendirip `POST /api/Auth/login-2fa` çağırmalısınız.

#### `POST /api/Auth/login-2fa`
İki adımlı doğrulama koduyla giriş tamamlar.
* **Body:**
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "code": "123456" // 6 haneli TOTP kodu (Google Authenticator vb.)
  }
  ```
* **Response (200 OK):** `AuthResponse` (`token` ve `refreshToken`).

#### `POST /api/Auth/refresh`
Access Token yeniler.
* **Body:**
  ```json
  {
    "refreshToken": "4a7f9b2c..."
  }
  ```
* **Response (200 OK):** `AuthResponse` (Yeni access token ve yenilenmiş refresh token döner).

#### `POST /api/Auth/logout`
Oturumu kapatır, refresh token'ı sunucuda iptal eder.
* **Body:**
  ```json
  {
    "refreshToken": "4a7f9b2c..."
  }
  ```
* **Response:** `204 NoContent`

#### `POST /api/Auth/forgot-password`
Şifre sıfırlama bağlantısı gönderir.
* **Body:**
  ```json
  {
    "email": "user@example.com"
  }
  ```
* **Response (200 OK):**
  ```json
  {
    "message": "Eğer bu e-posta adresi kayıtlıysa, şifre sıfırlama bağlantısı gönderildi."
  }
  ```

#### `POST /api/Auth/reset-password`
E-postaya gelen token ile yeni şifre belirler.
* **Body:**
  ```json
  {
    "token": "reset_token_string",
    "newPassword": "NewPassword123!"
  }
  ```
* **Response (200 OK):** `{ "message": "Şifreniz başarıyla değiştirildi." }`

#### `PUT /api/Auth/change-password` *(Korumalı)*
Giriş yapmış kullanıcının şifresini değiştirir.
* **Body:**
  ```json
  {
    "currentPassword": "OldPassword123!",
    "newPassword": "NewPassword123!"
  }
  ```
* **Response:** `204 NoContent`

#### `POST /api/Auth/2fa/enable` *(Korumalı)*
2FA kurulumunu başlatır, QR kod URI'si ve gizli anahtar üretir.
* **Response (200 OK):**
  ```json
  {
    "secret": "JBSWY3DPEHPK3PXP",
    "qrCodeUri": "otpauth://totp/TodoApp:user@example.com?secret=JBSWY3DPEHPK3PXP&issuer=TodoApp"
  }
  ```

#### `POST /api/Auth/2fa/verify` *(Korumalı)*
Kurulan 2FA'yı ilk kod ile doğrular ve aktifleştirir.
* **Body:** `{ "code": "123456" }`
* **Response (200 OK):** `{ "message": "İki adımlı doğrulama başarıyla aktifleştirildi." }`

#### `POST /api/Auth/2fa/disable` *(Korumalı)*
2FA'yı devre dışı bırakır.
* **Body:** `{ "code": "123456" }`
* **Response (200 OK):** `{ "message": "İki adımlı doğrulama devre dışı bırakıldı." }`

---

### 5.2. Todo Items (Görev Yönetimi) — Tüm Uç Noktalar Korumalı

#### `GET /api/TodoItems`
Filtrelenmiş ve sayfalanmış görev listesini getirir.
* **Query Parametreleri:**
  * `page` (int, varsayılan 1)
  * `pageSize` (int, varsayılan 20, maks 100)
  * `filterType` (int enum: 0=All, 1=OnlyMine, 2=SharedWithMe, 3=SharedByMe)
  * `search` (string? - Başlık veya açıklamada arar)
  * `status` (int enum? 0=Open, 1=Completed)
  * `priority` (int enum? 1=Low, 2=Medium, 3=High)
  * `todoListId` (guid? - Belirli bir liste/kategoriye ait olanlar)
  * `dueDateFrom` (datetime? ISO 8601)
  * `dueDateTo` (datetime? ISO 8601)
  * `sortBy` (string? "createdAt", "dueDate", "priority", "title" - varsayılan "createdAt")
  * `sortOrder` (string? "asc" veya "desc" - varsayılan "desc")
* **Response (200 OK):** `PaginatedResponse<TodoItemResponse>`
  ```json
  {
    "items": [
      {
        "id": "c1f7b7e5-...",
        "title": "Flutter UI tasarımı yapılacak",
        "description": "Figma prototiplerine göre sayfalar kodlanacak.",
        "dueDate": "2026-10-01T18:00:00Z",
        "status": "Open",
        "priority": "High",
        "todoListId": "a5d8...",
        "ownerId": "3fa8...",
        "isOwner": true,
        "completedByUserId": null,
        "completedAt": null,
        "createdAt": "2026-09-24T06:00:00Z",
        "updatedAt": null,
        "isDeleted": false,
        "deletedAt": null,
        "subTasks": [
          {
            "id": "e4b2...",
            "taskId": "c1f7...",
            "title": "Login sayfası",
            "status": "Completed",
            "createdAt": "2026-09-24T06:10:00Z",
            "updatedAt": null
          }
        ],
        "tags": [
          {
            "id": "7f1a...",
            "name": "Mobil",
            "createdAt": "2026-09-20T10:00:00Z"
          }
        ],
        "sharedWith": [
          {
            "userId": "9b1c...",
            "email": "developer@example.com",
            "sharedAt": "2026-09-24T06:15:00Z"
          }
        ]
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  }
  ```

#### `GET /api/TodoItems/{id}`
Tek bir görevin detayını getirir.
* **Response (200 OK):** `TodoItemResponse`

#### `POST /api/TodoItems`
Yeni görev oluşturur.
* **Body:**
  ```json
  {
    "title": "Yeni Görev Başlığı", // Zorunlu, maks 200 karakter
    "description": "Opsiyonel detaylı açıklama", // maks 2000 karakter
    "dueDate": "2026-10-15T12:00:00Z", // Opsiyonel
    "priority": 2, // 1=Low, 2=Medium, 3=High (varsayılan 2)
    "todoListId": "3fa85f64-..." // Opsiyonel (Kategori/Liste ID)
  }
  ```
* **Response (201 Created):** `TodoItemResponse` (Header'da `Location` döner).

#### `PUT /api/TodoItems/{id}`
Görevi günceller. *(Yalnızca görev sahibi - Owner güncelleyebilir!)*
* **Body:** `UpdateTodoItemRequest` (Oluşturma ile aynı alanlar).
* **Response (200 OK):** `TodoItemResponse`

#### `PATCH /api/TodoItems/{id}/complete`
Görevi tamamlandı veya açık durumuna getirir (toggle). Görev sahibi ve görevin paylaşıldığı kullanıcılar yapabilir.
* **Response (200 OK):** `TodoItemResponse`

#### `DELETE /api/TodoItems/{id}`
Görevi çöp kutusuna taşır (Soft Delete).
* **Response:** `204 NoContent`

#### `GET /api/TodoItems/trash`
Çöp kutusundaki görevleri listeler.
* **Query:** `page=1&pageSize=20`
* **Response (200 OK):** `PaginatedResponse<TodoItemResponse>`

#### `POST /api/TodoItems/{id}/restore`
Çöp kutusundaki görevi geri yükler.
* **Response (200 OK):** `TodoItemResponse`

#### `DELETE /api/TodoItems/{id}/permanent`
Görevi ve bağlı tüm verilerini veritabanından kalıcı olarak siler. *(Yalnızca sahip yapabilir).*
* **Response:** `204 NoContent`

#### `GET /api/TodoItems/{id}/activities`
Görevin geçmiş zaman çizelgesini (audit log) listeler.
* **Response (200 OK):** `CollectionResponse<TodoItemActivityResponse>`
  ```json
  {
    "items": [
      {
        "id": "...",
        "userId": "...",
        "userEmail": "ali@example.com",
        "action": "Completed",
        "details": "Görev tamamlandı olarak işaretlendi.",
        "createdAt": "2026-09-24T07:00:00Z"
      }
    ]
  }
  ```

---

### 5.3. SubTasks (Alt Görevler)

*Bir görevde en fazla **20 adet** alt görev bulunabilir.*

#### `GET /api/todoitems/{taskId}/subtasks`
Görevin alt görevlerini listeler.
* **Response (200 OK):** `CollectionResponse<SubTaskResponse>`

#### `POST /api/todoitems/{taskId}/subtasks`
Alt görev ekler. (Sahip ve paylaşılan kullanıcılar ekleyebilir).
* **Body:**
  ```json
  {
    "title": "Alt görev başlığı" // Zorunlu, maks 200 karakter
  }
  ```
* **Response (201 Created):** `SubTaskResponse`

#### `PATCH /api/subtasks/{id}/complete`
Alt görevi tamamlar veya tekrar açar.
* **Response (200 OK):** `SubTaskResponse`

#### `DELETE /api/subtasks/{id}`
Alt görevi siler. *(İş Kuralı: Yalnızca üst görevin SAHİBİ alt görev silebilir!)*
* **Response:** `204 NoContent`

---

### 5.4. Tags (Etiketler)

#### `GET /api/tags`
Sistemdeki tüm global etiketleri listeler.
* **Response (200 OK):** `CollectionResponse<TagResponse>`

#### `POST /api/tags`
Yeni global etiket oluşturur. *(Yalnızca Admin rolü oluşturabilir).*
* **Body:** `{ "name": "Mobil" }`
* **Response (201 Created):** `TagResponse`

#### `GET /api/todoitems/{taskId}/tags`
Bir göreve atanmış etiketleri getirir.
* **Response (200 OK):** `CollectionResponse<TagResponse>`

#### `POST /api/todoitems/{taskId}/tags/{tagId}`
Görevin üzerine etiket iliştirir.
* **Response:** `204 NoContent`

#### `DELETE /api/todoitems/{taskId}/tags/{tagId}`
Görevin üzerinden etiketi kaldırır.
* **Response:** `204 NoContent`

#### `GET /api/tags/{tagId}/todoitems`
Belirli bir etikete sahip tüm aktif görevleri sayfalanmış getirir.
* **Query:** `page=1&pageSize=20`
* **Response (200 OK):** `PaginatedResponse<TodoItemResponse>`

---

### 5.5. Task Shares (Görev Paylaşımı)

#### `POST /api/todoitems/{taskId}/shares`
Görevi başka bir kullanıcıyla e-posta adresi üzerinden paylaşır. *(Yalnızca görev sahibi paylaşabilir).*
* **Body:**
  ```json
  {
    "email": "arkadas@example.com"
  }
  ```
* **Response (200 OK):** `{ "message": "Görev başarıyla paylaşıldı." }`

#### `GET /api/todoitems/{taskId}/shares`
Görevin paylaşıldığı kullanıcıları listeler.
* **Response (200 OK):** `CollectionResponse<SharedUserResponse>`

#### `DELETE /api/todoitems/{taskId}/shares/{userId}`
Görev sahibi bir kullanıcının yetkisini kaldırır.
* **Response:** `204 NoContent`

#### `DELETE /api/todoitems/{taskId}/shares/me`
Paylaşılan kullanıcının kendi isteğiyle paylaşımdan ayrılması.
* **Response:** `204 NoContent`

---

### 5.6. Task Transfer (Sahiplik Devir Talepleri)

*Bir görevin sahipliği doğrudan aktarılmaz; önce talep oluşturulur, karşı taraf onaylarsa sahiplik geçer.*

#### `POST /api/todoitems/{taskId}/transfer-requests`
Devir talebi başlatır.
* **Body:** `{ "newOwnerEmail": "yeni_sahip@example.com" }`
* **Response (200 OK):** `TransferRequestResponse`

#### `GET /api/transfer-requests/pending`
Giriş yapmış kullanıcının onayını bekleyen devir taleplerini listeler.
* **Response (200 OK):** `CollectionResponse<TransferRequestResponse>`

#### `POST /api/transfer-requests/{requestId}/accept`
Devir talebini kabul eder. Görevin yeni sahibi bu kullanıcı olur.
* **Response (200 OK):** `{ "message": "Görev devir talebi başarıyla kabul edildi ve sahiplik aktarıldı." }`

#### `POST /api/transfer-requests/{requestId}/reject`
Talebi reddeder.
* **Response (200 OK):** `{ "message": "Görev devir talebi reddedildi." }`

#### `POST /api/transfer-requests/{requestId}/cancel`
Talep sahibi (mevcut sahip) talebi geri çeker.
* **Response (200 OK):** `{ "message": "Görev devir talebi başarıyla iptal edildi." }`

---

### 5.7. Todo Lists (Kategoriler / Proje Klasörleri)

#### `GET /api/TodoLists`
Kullanıcının oluşturduğu tüm listeleri getirir.
* **Response (200 OK):** `CollectionResponse<TodoListResponse>`
  ```json
  {
    "items": [
      {
        "id": "...",
        "name": "İş Projeleri",
        "colorCode": "#FF5733",
        "ownerId": "...",
        "createdAt": "2026-09-20T12:00:00Z",
        "updatedAt": null
      }
    ]
  }
  ```

#### `POST /api/TodoLists`
Yeni liste oluşturur.
* **Body:**
  ```json
  {
    "name": "Kişisel", // Zorunlu, maks 100 karakter
    "colorCode": "#33B5E5" // Opsiyonel, hex formatı
  }
  ```
* **Response (201 Created):** `TodoListResponse`

#### `GET /api/TodoLists/{id}`
Liste detayını döner.
* **Response (200 OK):** `TodoListResponse`

#### `PUT /api/TodoLists/{id}`
Listeyi günceller.
* **Body:** `UpdateTodoListRequest`
* **Response (200 OK):** `TodoListResponse`

#### `DELETE /api/TodoLists/{id}`
Listeyi siler. *(Bağlı görevler silinmez, görevlerin `todoListId` alanı null olur).*
* **Response:** `204 NoContent`

---

### 5.8. Users (Kullanıcı İşlemleri)

#### `DELETE /api/Users/me`
Kullanıcının hesabını ve sahip olduğu tüm verileri kalıcı olarak siler. Güvenlik için şifre tekrarı zorunludur.
* **Body:**
  ```json
  {
    "password": "Password123!"
  }
  ```
* **Response:** `204 NoContent`

---

## ⚡ 6. SignalR Canlı Bildirimler (WebSocket)

Uygulamada SignalR üzerinden anlık senkronizasyon sağlanır.

* **Hub Adresi:** `/hubs/todo`
* **Bağlantı Token'ı:** Query parametresi olarak iletilir: `/hubs/todo?access_token=<JWT_TOKEN>`

### Dinlenecek Olaylar (Client Events):
1. **`TaskUpdated`**: Görev tamamlandığında, güncellendiğinde veya silindiğinde tetiklenir. Argüman olarak güncellenen `taskId` gelir.
2. **`ReceiveNotification`**: Kullanıcıya görev paylaşıldığında, devir talebi geldiğinde veya hatırlatma oluştuğunda mesaj iletir.

---

## 🛑 7. Mobil Geliştirici İçin Altın İş Kuralları (Business Rules)

1. **Yetki Ayrımı:**
   * Görevin başlığını, açıklamasını, tarihini ve önceliğini **YALNIZCA görev sahibi (`isOwner == true`)** güncelleyebilir (`PUT`).
   * Görevi çöp kutusuna taşımayı (`DELETE`) veya kalıcı silmeyi (`permanent`) **YALNIZCA görev sahibi** yapabilir.
   * Alt görevleri silmeyi (`DELETE /api/subtasks/{id}`) **YALNIZCA üst görevin sahibi** yapabilir.
   * Görevi tamamlamayı (`PATCH complete`) ve alt görev ekleyip tamamlamayı **hem sahip hem paylaşılan kullanıcılar** yapabilir.
2. **Alt Görev Limiti:** Bir göreve en fazla **20 alt görev** eklenebilir. UI'da 20'ye ulaşıldığında "Ekle" butonunu devre dışı bırakabilirsiniz.
3. **Şifre Politikası (UI Doğrulaması İçin):**
   * En az 8 karakter, en fazla 128 karakter.
   * En az 1 büyük harf (`A-Z`), en az 1 küçük harf (`a-z`), en az 1 rakam (`0-9`).
4. **Çöp Kutusu:** Görev silindiğinde doğrudan yok olmaz; `/trash` ekranından geri yüklenebilir (`restore`) veya kalıcı olarak silinebilir.

