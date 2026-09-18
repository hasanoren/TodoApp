# 📚 TodoApp — REST API Endpoint Dokümantasyonu

**Son Güncelleme:** 1 Eylül 2026  
**Toplam Endpoint:** 26  
**Base URL:** `http://localhost:5240`  
**Swagger UI:** [http://localhost:5240/swagger](http://localhost:5240/swagger)  
**Kimlik Doğrulama:** Tüm korumalı endpoint'ler `Authorization: Bearer <JWT_TOKEN>` header'ı gerektirir.

---

## 🔐 1. Kimlik Doğrulama & Kullanıcı (`AuthController`)

### 1.1 Kullanıcı Kaydı
| | |
|---|---|
| **Endpoint** | `POST /api/Auth/register` |
| **Yetki** | Anonim |
| **Açıklama** | Yeni kullanıcı kaydı oluşturur. JWT access token ve refresh token döner. |
| **İş Kuralı** | `BR-001` (Email unique), `BR-005` (Varsayılan rol: User) |

```json
// İstek:
{ "email": "kullanici@ornek.com", "password": "Password123!" }

// Yanıt (200 OK):
{
  "userId": "4e8d5ea2-...",
  "email": "kullanici@ornek.com",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "a1b2c3d4e5..."
}
```

---

### 1.2 Giriş Yapma
| | |
|---|---|
| **Endpoint** | `POST /api/Auth/login` |
| **Yetki** | Anonim |
| **Açıklama** | E-posta ve şifre doğrulaması yaparak yeni oturum token'ları üretir. |

```json
// İstek:
{ "email": "kullanici@ornek.com", "password": "Password123!" }

// Yanıt (200 OK):
{
  "userId": "4e8d5ea2-...",
  "email": "kullanici@ornek.com",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "x9y8z7w6v5..."
}
```

---

### 1.3 Token Yenileme
| | |
|---|---|
| **Endpoint** | `POST /api/Auth/refresh` |
| **Yetki** | Anonim |
| **Açıklama** | Mevcut refresh token ile yeni access token üretir. Eski refresh token iptal edilir (token rotasyonu). |

```json
// İstek:
{ "refreshToken": "a1b2c3d4e5..." }

// Yanıt (200 OK):
{ "userId": "...", "email": "...", "token": "yeni_jwt...", "refreshToken": "yeni_refresh..." }
```

---

### 1.4 Çıkış Yapma
| | |
|---|---|
| **Endpoint** | `POST /api/Auth/logout` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Kullanıcının aktif refresh token'ını iptal ederek oturumu sonlandırır. |

```json
// İstek:
{ "refreshToken": "a1b2c3d4e5..." }

// Yanıt: 204 No Content
```

---

### 1.5 Şifremi Unuttum
| | |
|---|---|
| **Endpoint** | `POST /api/Auth/forgot-password` |
| **Yetki** | Anonim |
| **Açıklama** | E-posta adresine süreli şifre sıfırlama linki gönderir. Kayıtlı olmasa bile aynı mesajı döner (güvenlik). |

```json
// İstek:
{ "email": "kullanici@ornek.com" }

// Yanıt (200 OK):
{ "message": "Eğer bu e-posta adresi kayıtlıysa, şifre sıfırlama bağlantısı gönderildi." }
```

---

### 1.6 Şifre Sıfırlama Sayfası
| | |
|---|---|
| **Endpoint** | `GET /api/Auth/reset-password?token={token}` |
| **Yetki** | Anonim |
| **Açıklama** | E-postadaki sıfırlama token'ını doğrular ve HTML şifre yenileme formu sunar. |

---

### 1.7 Şifre Sıfırlama (Form Submit)
| | |
|---|---|
| **Endpoint** | `POST /api/Auth/reset-password` |
| **Yetki** | Anonim |
| **Content-Type** | `application/x-www-form-urlencoded` |
| **Açıklama** | Geçerli sıfırlama token'ı ile kullanıcının yeni şifresini kaydeder. |

---

### 1.8 Şifre Değiştirme
| | |
|---|---|
| **Endpoint** | `PUT /api/Auth/change-password` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Giriş yapmış kullanıcının eski şifresini doğrulayarak yeni şifre belirlemesini sağlar. Tüm aktif refresh token'ları iptal eder. |

```json
// İstek:
{ "currentPassword": "EskiSifre123!", "newPassword": "YeniSifre456!" }

// Yanıt: 204 No Content
```

---

## 📝 2. Ana Görevler (`TodoItemsController`)

### 2.1 Görev Oluşturma
| | |
|---|---|
| **Endpoint** | `POST /api/TodoItems` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Giriş yapan kullanıcı için yeni bir ana görev oluşturur. |
| **İş Kuralı** | `BR-006` (OwnerId otomatik atanır) |

```json
// İstek:
{
  "title": "Backend Geliştirme",
  "description": "Clean Architecture implementasyonu",
  "dueDate": "2026-12-31T23:59:59Z"
}

// Yanıt (201 Created):
{
  "id": "3fa85f64-...",
  "title": "Backend Geliştirme",
  "description": "Clean Architecture implementasyonu",
  "dueDate": "2026-12-31T23:59:59Z",
  "status": "Open",
  "ownerId": "4e8d5ea2-...",
  "completedByUserId": null,
  "completedAt": null,
  "createdAt": "2026-09-01T07:15:00Z",
  "subTasks": [],
  "tags": []
}
```

---

### 2.2 Tüm Aktif Görevleri Listeleme
| | |
|---|---|
| **Endpoint** | `GET /api/TodoItems` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Kullanıcının tüm aktif (silinmemiş) görevlerini etiketleriyle birlikte listeler. Alt görevler (SubTasks) bu listede **dahil edilmez** (hafif payload). |
| **İş Kuralı** | `BR-011` (Soft-delete edilmişler hariç) |

---

### 2.3 Görev Detayı Getirme
| | |
|---|---|
| **Endpoint** | `GET /api/TodoItems/{id}` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Belirtilen görevin detaylarını, **alt görevlerini (`subTasks`)** ve **etiketlerini (`tags`)** birleşik getirir. Yetkisiz erişimde 404. |
| **İş Kuralı** | `BR-029` (Yetkisizse 404, 403 değil) |

```json
// Yanıt (200 OK):
{
  "id": "3fa85f64-...",
  "title": "Backend Geliştirme",
  "status": "Open",
  "subTasks": [
    { "id": "e4b9...", "title": "Migration hazırla", "status": "Completed" }
  ],
  "tags": [
    { "id": "7b8a...", "name": "Backend", "createdAt": "..." }
  ]
}
```

---

### 2.4 Görev Güncelleme
| | |
|---|---|
| **Endpoint** | `PUT /api/TodoItems/{id}` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Görevin başlık, açıklama ve son teslim tarihini günceller. |

```json
// İstek:
{
  "title": "Güncellenmiş Başlık",
  "description": "Yeni açıklama",
  "dueDate": "2027-01-15T00:00:00Z"
}
```

---

### 2.5 Görev Tamamlama
| | |
|---|---|
| **Endpoint** | `PATCH /api/TodoItems/{id}/complete` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Görevi tamamlandı olarak işaretler. `CompletedByUserId` ve `CompletedAt` otomatik mühürlenir. |
| **İş Kuralı** | `BR-015` |

---

### 2.6 Görev Silme
| | |
|---|---|
| **Endpoint** | `DELETE /api/TodoItems/{id}` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Görev sahibi çağırdığında görevi ve alt görevlerini kalıcı olarak (hard delete) siler. |
| **İş Kuralı** | `BR-008a`, `BR-009`, `BR-019` |
| **⚠️ Bilinen Sorun** | Soft-delete akışı kırık (EPIC 4.5.5'te düzeltilecek). Şu an tüm silmeler hard delete. |

---

### 2.7 Çöp Kutusunu Listeleme
| | |
|---|---|
| **Endpoint** | `GET /api/TodoItems/trash` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Görev sahibinin çöp kutusundaki (soft-deleted) görevlerini listeler. |
| **⚠️ Bilinen Sorun** | Soft-delete akışı kırık olduğu için şu an her zaman boş döner (EPIC 4.5.5). |

---

### 2.8 Görev Geri Yükleme
| | |
|---|---|
| **Endpoint** | `POST /api/TodoItems/{id}/restore` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Çöp kutusundaki bir görevi tekrar aktif görevler listesine geri döndürür. |
| **İş Kuralı** | `BR-010` |

---

## 📌 3. Alt Görevler (`SubTasksController`)

### 3.1 Alt Görev Oluşturma
| | |
|---|---|
| **Endpoint** | `POST /api/todoitems/{taskId}/subtasks` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Belirtilen ana görevin altına yeni bir alt görev ekler. Silinmiş göreve eklenemez. |
| **İş Kuralı** | `BR-012`, `BR-016`, `BR-020` |

```json
// İstek:
{ "title": "Veritabanı migration hazırla" }

// Yanıt (201 Created):
{
  "id": "e4b98765-...",
  "taskId": "3fa85f64-...",
  "title": "Veritabanı migration hazırla",
  "status": "Open",
  "createdAt": "2026-09-01T07:16:00Z"
}
```

---

### 3.2 Alt Görevleri Listeleme
| | |
|---|---|
| **Endpoint** | `GET /api/todoitems/{taskId}/subtasks` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Belirtilen ana göreve bağlı tüm alt görevleri yalın dizi olarak listeler. |
| **İş Kuralı** | `BR-018`, `BR-029` |

---

### 3.3 Alt Görev Tamamlama / Geri Açma
| | |
|---|---|
| **Endpoint** | `PATCH /api/subtasks/{id}/complete` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Alt görevin durumunu `Open ↔ Completed` arasında değiştirir. Üst görevi etkilemez. |
| **İş Kuralı** | `BR-017` |

---

### 3.4 Alt Görev Silme
| | |
|---|---|
| **Endpoint** | `DELETE /api/subtasks/{id}` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Belirtilen alt görevi veritabanından kalıcı olarak siler. |
| **İş Kuralı** | `BR-020` |

---

## 🏷️ 4. Etiketler (`TagsController`)

### 4.1 Etiket Oluşturma (Sadece Admin)
| | |
|---|---|
| **Endpoint** | `POST /api/tags` |
| **Yetki** | `[Authorize(Roles = "Admin")]` |
| **Açıklama** | Sadece Admin rolündeki kullanıcıların yeni bir global etiket oluşturmasını sağlar. İsim case-insensitive unique. |
| **İş Kuralı** | `BR-021`, `BR-022`, `BR-023` |

```json
// İstek:
{ "name": "Backend" }

// Yanıt (201 Created):
{ "id": "7b8a1234-...", "name": "Backend", "createdAt": "2026-09-01T07:15:00Z" }
```

---

### 4.2 Tüm Etiketleri Listeleme
| | |
|---|---|
| **Endpoint** | `GET /api/tags` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Sistemdeki tüm global etiketleri alfabetik sırada listeler. |
| **İş Kuralı** | `BR-021` |

---

### 4.3 Etikete Göre Görevleri Listeleme
| | |
|---|---|
| **Endpoint** | `GET /api/tags/{tagId}/todoitems` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Belirli bir etikete sahip olan ve **yalnızca giriş yapan kullanıcıya ait** tüm aktif görevleri listeler. Başka kullanıcıların görevleri görüntülenemez. |
| **İş Kuralı** | `BR-011`, `BR-029` |

---

### 4.4 Göreve Atanmış Etiketleri Listeleme
| | |
|---|---|
| **Endpoint** | `GET /api/todoitems/{taskId}/tags` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Belirtilen göreve atanmış tüm etiketleri listeler. |
| **İş Kuralı** | `BR-029` |

---

### 4.5 Göreve Etiket Bağlama
| | |
|---|---|
| **Endpoint** | `POST /api/todoitems/{taskId}/tags/{tagId}` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Belirtilen ana göreve seçilen etiketi bağlar. Aynı etiket aynı göreve iki kez bağlanamaz. Silinmiş göreve etiket eklenemez. |
| **İş Kuralı** | `BR-012`, `BR-024` |

---

### 4.6 Görevden Etiket Kaldırma
| | |
|---|---|
| **Endpoint** | `DELETE /api/todoitems/{taskId}/tags/{tagId}` |
| **Yetki** | `[Authorize]` |
| **Açıklama** | Belirtilen görevden etiketin ilişkisini kaldırır. Etiketin kendisi silinmez, sadece görev-etiket bağlantısı kopar. |
| **İş Kuralı** | `BR-023`, `BR-029` |

---

## 📊 HTTP Durum Kodları Referansı

| Kod | Anlam | Ne Zaman Döner? |
|:---:|---|---|
| `200` | OK | Başarılı GET, PUT, PATCH işlemleri |
| `201` | Created | Başarılı POST (yeni kayıt oluşturma) |
| `204` | No Content | Başarılı DELETE veya Logout |
| `400` | Bad Request | Geçersiz input (ValidationException) |
| `401` | Unauthorized | JWT token eksik veya geçersiz |
| `403` | Forbidden | Yetersiz rol (ör: User rolü Admin endpoint'ine erişim) |
| `404` | Not Found | Kayıt bulunamadı veya yetkisiz erişim (BR-029) |
| `409` | Conflict | Çakışma (duplicate email, duplicate tag name, duplicate tag assignment) |
| `500` | Internal Server Error | Beklenmeyen sunucu hatası |

---

## 🧪 Test Kullanıcıları

| Rol | E-posta | Şifre |
|---|---|---|
| **Admin** | `hasan@gmail.com` | `Password123!` |
| **User** | `test@example.com` | `Password123!` |
