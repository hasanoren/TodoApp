# 📚 To-Do App — REST API Endpoint Dokümantasyonu

Bu doküman, To-Do uygulamasında bulunan tüm aktif REST API endpoint'lerinin listesini, kullanım amaçlarını, yetkilendirme gereksinimlerini ve istek/yanıt formatlarını içerir.

**Temel Bilgiler:**
- **Kök Adres:** `http://localhost:5240`
- **Swagger UI:** `http://localhost:5240/swagger`
- **Kimlik Doğrulama:** `Authorization: Bearer <JWT_TOKEN>`

---

## 🔐 1. Kimlik Doğrulama & Kullanıcı Modülü (`/api/Auth`)

| # | Metot | Route | Yetki | Açıklama | İlgili Kural |
|:---:|:---:|---|:---:|---|:---:|
| 1 | `POST` | `/api/Auth/register` | Anonim | Yeni kullanıcı kaydı oluşturur; JWT access token ve refresh token döner. | `BR-001`, `BR-005` |
| 2 | `POST` | `/api/Auth/login` | Anonim | E-posta ve şifre doğrulaması yaparak yeni oturum token'ları üretir. | - |
| 3 | `POST` | `/api/Auth/refresh` | Anonim | Mevcut refresh token ile yeni bir access token üretir (Token rotasyonu). | - |
| 4 | `POST` | `/api/Auth/logout` | `[Authorize]` | Kullanıcının aktif refresh token'ını iptal ederek oturumu sonlandırır. | - |
| 5 | `POST` | `/api/Auth/forgot-password` | Anonim | Şifre sıfırlama talebi alır; e-postaya süreli sıfırlama linki gönderir. | `BR-001` |
| 6 | `GET` | `/api/Auth/reset-password` | Anonim | Şifre sıfırlama token'ını doğrular ve HTML form arayüzü sunar. | - |
| 7 | `POST` | `/api/Auth/reset-password` | Anonim | Sıfırlama token'ı ile kullanıcının yeni şifresini kaydeder. | - |
| 8 | `PUT` | `/api/Auth/change-password` | `[Authorize]` | Giriş yapmış kullanıcının eski şifresini doğrulayarak yeni şifre belirlemesini sağlar. | - |

---

## 📝 2. Ana Görevler Modülü (`/api/TodoItems`)

| # | Metot | Route | Yetki | Açıklama | İlgili Kural |
|:---:|:---:|---|:---:|---|:---:|
| 9 | `POST` | `/api/TodoItems` | `[Authorize]` | Giriş yapan kullanıcı için yeni bir ana görev oluşturur. | `BR-006` |
| 10 | `GET` | `/api/TodoItems` | `[Authorize]` | Kullanıcının tüm aktif (silinmemiş) görevlerini etiketleriyle birlikte listeler (Hafif liste görünümü). | `BR-011` |
| 11 | `GET` | `/api/TodoItems/{id}` | `[Authorize]` | Belirtilen görevin detaylarını, alt görevlerini (`subTasks`) ve etiketlerini (`tags`) birleşik getirir. | `BR-029` |
| 12 | `PUT` | `/api/TodoItems/{id}` | `[Authorize]` | Görevin başlık, açıklama ve son teslim tarihi (`dueDate`) bilgilerini günceller. | `BR-029` |
| 13 | `PATCH` | `/api/TodoItems/{id}/complete` | `[Authorize]` | Görevi tamamlandı olarak işaretler; tamamlayan kişi ve tarih bilgisini mühürler. | `BR-015` |
| 14 | `DELETE` | `/api/TodoItems/{id}` | `[Authorize]` | Görev sahibi çağırdığında görevi ve alt görevlerini veritabanından kalıcı olarak (`hard delete`) siler. | `BR-008a`, `BR-019` |
| 15 | `GET` | `/api/TodoItems/trash` | `[Authorize]` | Görev sahibinin çöp kutusundaki (`soft-deleted`) görevlerini listeler. | `BR-011` |
| 16 | `POST` | `/api/TodoItems/{id}/restore` | `[Authorize]` | Çöp kutusundaki bir görevi tekrar aktif görevler listesine geri döndürür. | `BR-010` |

---

## 📌 3. Alt Görevler Modülü (`/api/subtasks` & `/api/todoitems/{taskId}/subtasks`)

| # | Metot | Route | Yetki | Açıklama | İlgili Kural |
|:---:|:---:|---|:---:|---|:---:|
| 17 | `POST` | `/api/todoitems/{taskId}/subtasks` | `[Authorize]` | Belirtilen ana görevin altına yeni bir alt görev (`SubTask`) ekler. | `BR-012`, `BR-016`, `BR-020` |
| 18 | `GET` | `/api/todoitems/{taskId}/subtasks` | `[Authorize]` | Belirtilen ana göreve bağlı tüm alt görevleri yalın dizi olarak listeler. | `BR-018`, `BR-029` |
| 19 | `PATCH` | `/api/subtasks/{id}/complete` | `[Authorize]` | Alt görevin durumunu `Open` ⮂ `Completed` olarak değiştirir (Üst görevi etkilemez). | `BR-017` |
| 20 | `DELETE` | `/api/subtasks/{id}` | `[Authorize]` | Belirtilen alt görevi veritabanından kalıcı olarak siler. | `BR-020` |

---

## 🏷️ 4. Etiketler Modülü (`/api/tags` & `/api/todoitems/{taskId}/tags`)

| # | Metot | Route | Yetki | Açıklama | İlgili Kural |
|:---:|:---:|---|:---:|---|:---:|
| 21 | `POST` | `/api/tags` | `Admin` | Sadece `Admin` rolündeki kullanıcıların yeni bir global etiket oluşturmasını sağlar. | `BR-021`, `BR-022`, `BR-023` |
| 22 | `GET` | `/api/tags` | `[Authorize]` | Sistemdeki tüm global etiketleri alfabetik sırada listeler. | `BR-021` |
| 23 | `GET` | `/api/tags/{tagId}/todoitems` | `[Authorize]` | Belirli bir etikete sahip olan ve kullanıcıya ait tüm aktif görevleri listeler. | `BR-011`, `BR-029` |
| 24 | `GET` | `/api/todoitems/{taskId}/tags` | `[Authorize]` | Belirtilen göreve atanmış tüm etiketleri listeler. | `BR-029` |
| 25 | `POST` | `/api/todoitems/{taskId}/tags/{tagId}` | `[Authorize]` | Belirtilen ana göreve seçilen etiketi bağlar (Aynı etiket iki kez bağlanamaz). | `BR-012`, `BR-024` |
| 26 | `DELETE` | `/api/todoitems/{taskId}/tags/{tagId}` | `[Authorize]` | Belirtilen görevden etiketin ilişkisini kaldırır (Etiketin kendisi silinmez). | `BR-023`, `BR-029` |

---

## 📊 Örnek İstek & Yanıt Modelleri

### 1. Ana Görev Oluşturma (`POST /api/TodoItems`)
```json
// Request Body:
{
  "title": "Backend Mimarisi Geliştirme",
  "description": "Clean Architecture, SubTask ve Tag implementasyonu",
  "dueDate": "2026-12-31T23:59:59Z"
}

// Response (201 Created):
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Backend Mimarisi Geliştirme",
  "description": "Clean Architecture, SubTask ve Tag implementasyonu",
  "dueDate": "2026-12-31T23:59:59Z",
  "status": "Open",
  "ownerId": "4e8d5ea2-62ab-4dda-8f92-aa11c72c4d70",
  "completedByUserId": null,
  "completedAt": null,
  "createdAt": "2026-09-01T07:15:00Z",
  "subTasks": [],
  "tags": []
}
```

### 2. Görev Detayı Getirme (`GET /api/TodoItems/{id}`)
```json
// Response (200 OK):
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Backend Mimarisi Geliştirme",
  "description": "Clean Architecture, SubTask ve Tag implementasyonu",
  "dueDate": "2026-12-31T23:59:59Z",
  "status": "Open",
  "ownerId": "4e8d5ea2-62ab-4dda-8f92-aa11c72c4d70",
  "completedByUserId": null,
  "completedAt": null,
  "createdAt": "2026-09-01T07:15:00Z",
  "subTasks": [
    {
      "id": "e4b98765-a123-4c56-b789-d0123456789a",
      "taskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "title": "Veritabanı migration hazırla",
      "status": "Completed",
      "createdAt": "2026-09-01T07:16:00Z"
    }
  ],
  "tags": [
    {
      "id": "7b8a1234-c567-4e89-9abc-d0123456789a",
      "name": "Backend",
      "createdAt": "2026-09-01T07:15:00Z"
    }
  ]
}
```
