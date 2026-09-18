# To-Do App — Business Rules: Katman Ayrımı (DB vs Servis)

Bu doküman, `business-rules.md` içindeki 29 kuralın hangi katmanda (Veritabanı / Servis / Hibrit) uygulandığını gösterir. Kod yazarken veya AI'ya task devrederken referans olarak kullanılabilir.

**Katman tanımları:**
- **DB** — constraint, foreign key, unique index, cascade gibi yapısal kurallar. Veritabanının kendisi ihlali engeller.
- **Servis** — yetki kontrolü, koşullu mantık, dallanma. "Kim, ne zaman, hangi koşulda" gerektiren kararlar, kod tarafından kontrol edilir.
- **Hibrit** — DB fiziksel olarak engelliyor, ama servis katmanı bu durumu anlamlı bir mesaj/davranışa çevirmek zorunda.

---

## Tam Liste

| ID | Kural | Katman | Uygulama Detayı |
|---|---|---|---|
| BR-001 | E-posta benzersiz olmalı | **DB** | `User.Email` unique constraint |
| BR-002 | User silinirse sahip olduğu Task'lar da silinir | **DB** | FK `ON DELETE CASCADE` (User → Task via OwnerId). **Not:** Task'taki diğer User FK'ları (`CompletedByUserId`, `DeletedByUserId`) `ON DELETE SET NULL` olmalı — SQL Server multiple cascade paths kısıtlaması. |
| BR-003 | User silinirse TaskShare kayıtları da silinir | **DB** | FK `ON DELETE CASCADE` (User → TaskShare) |
| BR-004 | Kullanıcı kendi görevini kendisiyle paylaşamaz | **Servis** | `ShareTask`: `targetUserId == Task.OwnerId` ise reddet |
| BR-005 | User'ın Role alanı vardır (Admin/User) | **DB** | Kolon/enum tanımı — rolün etkisi servis kararı |
| BR-006 | Task bir owner'a aittir, NOT NULL | **DB** | FK NOT NULL constraint |
| BR-007 | Task sıfır veya daha fazla SubTask'a sahip olabilir | **DB** | FK ilişkisi, kardinalite |
| BR-008 | Yalnızca owner silebilir, paylaşılan kullanıcı silemez | **Servis** | `DeleteTask`: `currentUser == Task.OwnerId` kontrolü |
| BR-009 | Tamamlanmış Task da silinebilir | **Servis** | Silme öncesi Status kontrolü yapılmaz, izin verilir |
| BR-010 | Soft-delete Task'ı sadece owner restore edebilir | **Servis** | `RestoreTask`: `currentUser == OwnerId` kontrolü |
| BR-011 | Soft-delete Task, restore edilene kadar listelerde görünmez | **Servis** | Listeleme query'lerinde `WHERE IsDeleted = false` filtresi |
| BR-012 | Silinmiş Task'a yeni SubTask eklenemez | **Servis** | `AddSubTask`: önce `Task.IsDeleted` kontrolü |
| BR-013 | Sadece owner paylaşım yapabilir | **Servis** | `ShareTask`: `currentUser == Task.OwnerId` kontrolü |
| BR-014 | Aynı kullanıcıyla tekrar paylaşım → sessizce yok say | **Hibrit** | DB: PK `(TaskId, UserId)` duplicate'i engeller / Servis: DB hatasını yakalayıp sessizce başarı döner |
| BR-015 | Paylaşım kalksa bile Completed bilgisi korunur | **DB** | `CompletedByUserId`, `CompletedAt` Task tablosunda tutulur, TaskShare'e bağlı değil |
| BR-016 | SubTask mutlaka bir Task'a bağlıdır | **DB** | FK NOT NULL constraint |
| BR-017 | Üst görev tamamlanınca alt görevler değişmez | **Servis** | `CompleteTask`: SubTask'lara dokunulmaz (negatif kural — kod yazılmaz) |
| BR-018 | Üst görev soft-delete olunca alt görevler de erişilemez olur | **Servis** | SubTask query'lerinde parent `Task.IsDeleted` kontrolü |
| BR-019 | Üst görev hard silinirse alt görevler de silinir | **DB** | FK `ON DELETE CASCADE` (Task → SubTask) |
| BR-020 | SubTask'ın kendi yetki kaydı yok, erişim parent'tan gelir (Paylaşılanlar ekler/düzenler/tamamlar; silme SADECE Owner) | **Servis** | Yetki kontrolü `TaskAuthorizationService` üzerinden yapılır; silmede `currentUser == Task.OwnerId` şartı aranır |
| BR-021 | Tag'ler global, sahibi yok | **DB** | Şema tasarımı — Tag tablosunda OwnerId kolonu yok |
| BR-022 | Tag sadece Admin tarafından oluşturulabilir | **Servis** | `CreateTag`: `currentUser.Role == Admin` kontrolü |
| BR-023 | Kullanılmayan Tag silinmez | **Servis (pasif)** | Otomatik temizlik job'ı kasıtlı olarak yazılmaz |
| BR-024 | Bir Tag bir Task'a yalnızca bir kez eklenebilir | **DB** | PK `(TaskId, TagId)` |
| BR-025 | Paylaşılan kullanıcı Task'ı tamamlayabilir | **Servis** | `CompleteTask`: `currentUser == OwnerId OR currentUser IN TaskShare` |
| BR-026 | Paylaşılan kullanıcı Task'ı silemez | **Servis** | `DeleteTask`: paylaşılan kullanıcı silmeye çalışırsa 404 döner |
| BR-027 | Var olmayan kullanıcıyla paylaşım → validasyon hatası | **Hibrit** | DB: FK constraint referansı engeller / Servis: anlamlı hata mesajına çevrilir |
| BR-028 | Paylaşılan kullanıcı kendi isteğiyle paylaşımdan çıkabilir | **Servis** | `LeaveSharedTask`: sadece kendi TaskShare kaydını silebilir |
| BR-029 | Yetkisiz erişimde 404 dönmeli | **Servis** | Controller/Service: yetki yoksa 404 (403 değil) |
| BR-030 | Görev sahibi sahipliği devredebilir | **Servis** | `TransferOwnership`: `currentUser == Task.OwnerId`, yeni sahip atanır, eski sahip TaskShare'e geçer |

---

## Sayısal Özet

| Katman | Adet | Kurallar |
|---|---|---|
| **DB** | 10 | BR-001, 002, 003, 006, 007, 015, 016, 019, 021, 024 |
| **Servis** | 16 | BR-004, 008a, 008b, 009, 010, 011, 012, 013, 017, 018, 020, 022, 023, 025, 026, 028, 029 |
| **Hibrit** | 3 | BR-005, 014, 027 |

*(BR-005 sayaçta hem DB hem dolaylı servis etkisi olduğu için ayrı değerlendirilebilir; toplam 29 kural.)*

---

## Servis Katmanı Kurallarının Alt Kategorileri

Kod organizasyonu için servis kurallarını üçe ayırmak faydalı olur:

1. **Yetkilendirme (Authorization)** — BR-004, 010, 013, 020, 022, 025, 026, 028, 029
2. **Durum/Dallanma Mantığı** — BR-008a, 008b, 009, 017, 018
3. **Görünürlük/Filtreleme (Query)** — BR-011, 012, 018

Bu ayrım, kodda muhtemelen şu yapılara karşılık gelir: bir **Authorization/Policy katmanı**, bir **Domain/Business Service** katmanı, ve **Repository/Query filtreleri**.
