# 🚀 TodoApp — API Smoke Test Rehberi (Uçtan Uca Doğrulama)

Bu rehber, TodoApp API'sinin yeni bir ortama dağıtıldığında veya ana özellikler tamamlandığında hızlıca doğrulanabilmesi için hazırlanmış **10 adımlı uçtan uca manuel/otomatik smoke test senaryosudur**.

---

## 🛠️ Ön Hazırlık

1. **Veritabanını Başlat:**
   ```powershell
   docker compose up -d
   ```
2. **Migration'ları Uygula:**
   ```powershell
   dotnet ef database update --project src/TodoApp.Infrastructure --startup-project src/TodoApp.Api
   ```
3. **API'yi Başlat:**
   ```powershell
   dotnet run --project src/TodoApp.Api
   ```
4. **Swagger Arayüzünü Aç:** `https://localhost:5240/swagger` (veya canlı ortam: `https://todoapp-api-gudhgje6bvfqg3ev.centralus-01.azurewebsites.net/swagger`)

---

## 📋 10 Adımlı Smoke Test Senaryosu

### Adım 1: Kullanıcı Kaydı (Register)
* **İstek:** `POST /api/Auth/register`
* **Gövde:**
  ```json
  {
    "email": "owner@example.com",
    "password": "Password123!"
  }
  ```
* **Beklenen Yanıt:** `200 OK` (İçerisinde `token` ve `refreshToken` döner).

---

### Adım 2: Giriş Yapma & Token Alma (Login)
* **İstek:** `POST /api/Auth/login`
* **Gövde:**
  ```json
  {
    "email": "owner@example.com",
    "password": "Password123!"
  }
  ```
* **Beklenen Yanıt:** `200 OK` (Swagger'da `Authorize` butonuna tıklayıp token'ı girin).

---

### Adım 3: Görev Oluşturma (Create TodoItem)
* **İstek:** `POST /api/TodoItems`
* **Header:** `Authorization: Bearer <TOKEN>`
* **Gövde:**
  ```json
  {
    "title": "Smoke Test Ana Görevi",
    "description": "Uçtan uca sistem testi",
    "dueDate": "2026-12-31T23:59:59Z"
  }
  ```
* **Beklenen Yanıt:** `201 Created` (Dönen `id` değerini `TASK_ID` olarak not edin).

---

### Adım 4: Alt Görev Ekleme (Create SubTask)
* **İstek:** `POST /api/todoitems/{TASK_ID}/subtasks`
* **Gövde:**
  ```json
  {
    "title": "Veritabanı kontrolü yap"
  }
  ```
* **Beklenen Yanıt:** `201 Created` (Dönen alt görev `id` değerini `SUBTASK_ID` olarak not edin).

---

### Adım 5: Alt Görevi Tamamlama (Complete SubTask)
* **İstek:** `PATCH /api/subtasks/{SUBTASK_ID}/complete`
* **Beklenen Yanıt:** `200 OK` (Status: `"Completed"` olmalıdır).

---

### Adım 6: İkinci Bir Kullanıcı Aç & Görevi Paylaş (Task Share)
1. `POST /api/Auth/register` ile `collab@example.com` kullanıcısı oluşturun.
2. İlk kullanıcı (`owner@example.com`) token'ı ile:
   * **İstek:** `POST /api/todoitems/{TASK_ID}/shares`
   * **Gövde:**
     ```json
     {
       "email": "collab@example.com"
     }
     ```
   * **Beklenen Yanıt:** `200 OK` (BR-013).

---

### Adım 7: Paylaşılan Kullanıcının Yetki Kontrolü
1. `collab@example.com` token'ı ile:
   * `GET /api/TodoItems/{TASK_ID}` → `200 OK` (Görebilmeli)
   * `PATCH /api/subtasks/{SUBTASK_ID}/complete` → `200 OK` (Alt görevi tamamlayabilmeli)
   * `DELETE /api/subtasks/{SUBTASK_ID}` → `404 Not Found` (BR-020, BR-026: **Alt görevi silememeli!**)
   * `PATCH /api/TodoItems/{TASK_ID}/complete` → `404 Not Found` (BR-025: **Ana görevi tamamlayamaz, yalnızca sahip tamamlayabilir!**)

---

### Adım 8: Sahiplik Devir Akışı (Ownership Transfer)
1. `owner@example.com` token'ı ile:
   * **İstek:** `POST /api/todoitems/{TASK_ID}/transfer-requests`
   * **Gövde:** `{ "targetUserEmail": "collab@example.com" }`
   * **Beklenen Yanıt:** `200 OK` (Dönen `id` değerini `REQUEST_ID` olarak not edin).
2. `collab@example.com` token'ı ile:
   * **İstek:** `GET /api/transfer-requests/pending` → Listede talebi görür.
   * **İstek:** `POST /api/transfer-requests/{REQUEST_ID}/accept` → `200 OK` (Sahiplik devredilir).

---

### Adım 9: Soft Delete & Çöp Kutusu (Trash)
1. Yeni sahip (`collab@example.com`) token'ı ile:
   * **İstek:** `DELETE /api/TodoItems/{TASK_ID}` → `204 NoContent` (BR-008: Sahip olduğu için silebilir).
   * **İstek:** `GET /api/TodoItems/{TASK_ID}` → `404 Not Found` (Aktif listeden düştü).
   * **İstek:** `GET /api/TodoItems/trash?page=1&pageSize=10` → `200 OK` (Çöp kutusunda görünür).

---

### Adım 10: Geri Yükleme (Restore) & Kalıcı Silme (Permanent Delete)
1. **Geri Yükle:**
   * **İstek:** `POST /api/TodoItems/{TASK_ID}/restore`
   * **Beklenen Yanıt:** `200 OK` (Tekrar aktif listeye döner).
2. **Kalıcı Sil (Permanent Delete):**
   * Önce tekrar soft delete: `DELETE /api/TodoItems/{TASK_ID}` → `204 NoContent`
   * Kalıcı sil: `DELETE /api/TodoItems/{TASK_ID}/permanent` → `204 NoContent`
   * `GET /api/TodoItems/trash` → Artık çöpte de bulunmaz.

---

## 🎯 Test Otomasyonu
Tüm bu adımlar `TodoApp.IntegrationTests` projesinde SQLite in-memory ile otomatik olarak doğrulanmaktadır:
```powershell
dotnet test
```

