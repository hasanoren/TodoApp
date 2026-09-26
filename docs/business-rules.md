# To-Do App — Business Rules & Domain Model

## Entity Listesi
- **User** — sistem kullanıcısı (Role: Admin / User)
- **TodoItem (Task)** — ana görev (BaseAuditableEntity: CreatedAt, UpdatedAt)
- **SubTask** — alt görev (BaseAuditableEntity: CreatedAt, UpdatedAt)
- **TodoList** — görev listesi / kategori (BaseAuditableEntity: CreatedAt, UpdatedAt)
- **Tag** — global etiket
- **TodoItemTag** — görev-etiket ara tablosu (Composite PK: TodoItemId, TagId)
- **TaskShare** — görev paylaşım kaydı (Composite PK: TaskId, UserId)
- **OwnershipTransferRequest** — görev sahiplik devri talep ve onay akışı
- **TodoItemActivity** — görev denetim izi / zaman çizelgesi kaydı
- **RefreshToken** — güvenli oturum yenileme kaydı (SHA-256 hash)

---

## Tam İş Kuralları Listesi (BR-001 → BR-030)

### User
| ID | Kural |
|---|---|
| BR-001 | Bir kullanıcının e-postası benzersiz olmalı |
| BR-002 | Bir kullanıcı silinirse, sahibi olduğu tüm Task'lar da silinir (cascade hard delete) |
| BR-003 | Bir kullanıcı silinirse, ona ait tüm TaskShare kayıtları da silinir |
| BR-004 | Bir kullanıcı kendi görevini kendisiyle paylaşamaz (owner == share hedefi engellenir) |
| BR-005 | Bir kullanıcının Role alanı vardır: **Admin** veya **User** |

### Task
| ID | Kural |
|---|---|
| BR-006 | Bir görev bir sahibe (owner) aittir, owner NOT NULL |
| BR-007 | Bir görev sıfır veya daha fazla alt göreve (SubTask) sahip olabilir |
| BR-008 | Bir görevi YALNIZCA görev sahibi silebilir. Paylaşılan kullanıcılar görevi silemez (ne soft ne hard delete) |
| BR-009 | Tamamlanmış (Completed) bir görev de silinebilir (sadece owner tarafından) |
| BR-010 | Soft-delete edilmiş bir görevi sadece owner geri getirebilir (restore) |
| BR-011 | Soft-delete edilmiş görev, restore edilene kadar hem owner'ın hem paylaşılan kullanıcıların aktif listelerinde görünmez |
| BR-012 | Silinmiş (soft-delete) bir Task'a yeni SubTask eklenemez |
| BR-013 | Sadece owner paylaşım yapabilir |
| BR-014 | Aynı kullanıcıyla aynı Task tekrar paylaşılmaya çalışılırsa → sessizce yok sayılır (hata dönmez, idempotent) |
| BR-015 | Paylaşım tamamen kaldırılsa bile (tüm TaskShare kayıtları silinse bile), daha önce paylaşılan kullanıcının Task üzerindeki geçmişi (Completed durumu, kim tamamladığı) korunur — bu bilgi Task'ın kendi alanlarında tutulur, TaskShare'e bağlı değildir |

### SubTask
| ID | Kural |
|---|---|
| BR-016 | Bir alt görev mutlaka bir üst göreve bağlıdır (NOT NULL FK) |
| BR-017 | Üst görev tamamlanınca alt görevlerin durumu değişmez (olduğu gibi kalır) |
| BR-018 | Üst görev soft-delete edilince, tüm alt görevler de aynı şekilde erişilemez hale gelir (görünürlük parent'tan miras alınır) |
| BR-019 | Üst görev (hard) silinirse, tüm alt görevler de silinir (cascade delete) |
| BR-020 | SubTask'ın kendine ait bir yetki/paylaşım kaydı yoktur — erişim kontrolü her zaman üst Task üzerinden yapılır. Paylaşılan kullanıcılar alt görevleri listeleyebilir, ekleyebilir ve tamamlayabilir; ancak alt görevleri YALNIZCA ana görevin sahibi (Owner) silebilir. Paylaşılan veya yabancı kullanıcı silmeye kalkarsa 404 döner (BR-026, BR-029). |

### Tag
| ID | Kural |
|---|---|
| BR-021 | Etiketler global bir havuzdan gelir, sahibi yoktur |
| BR-022 | Yeni bir Tag sadece **Admin** rolündeki kullanıcılar tarafından oluşturulabilir |
| BR-023 | Bir Tag hiçbir Task'ta kullanılmasa da silinmez, kalıcıdır |
| BR-024 | Bir Tag, bir Task'a yalnızca bir kez eklenebilir (aynı Tag-Task ikilisi tekrar eklenemez) |

### TaskShare / Yetkilendirme
| ID | Kural |
|---|---|
| BR-025 | Ana görevin alanlarını (başlık, açıklama, bitiş tarihi) güncelleme ve görevi tamamlama yetkisi YALNIZCA görev sahibine (Owner) aittir. Paylaşılan kullanıcılar ana görevi güncelleyemez, silemez ve tamamlayamaz; yalnızca alt görevleri (SubTasks) listeleyebilir, ekleyebilir ve alt görevleri tamamlayabilir (BR-020). Paylaşılan veya yabancı kullanıcı ana görevi güncellemeye veya tamamlamaya kalkarsa 404 döner (BR-029). |
| BR-026 | Paylaşılan kullanıcılar görevi kesinlikle silemez (ne soft ne hard delete). Silme yetkisi sadece sahibe aittir; yetkisiz silme denemesinde 404 döner |
| BR-027 | Var olmayan bir kullanıcıyla paylaşım yapılmaya çalışılırsa → validasyon hatası döner |
| BR-028 | Paylaşılan kullanıcı, kendi isteğiyle paylaşımdan çıkabilir (kendi TaskShare kaydını silebilir) |
| BR-029 | Owner veya TaskShare'de kayıtlı olmayan bir kullanıcı, Task'a ID ile doğrudan erişmeye çalışırsa → **404** döner (403 değil, var olduğu bilgisi bile sızdırılmaz) |
| BR-030 | Görev sahibi, görevin sahipliğini başka bir kayıtlı kullanıcıya devredebilir (Transfer Ownership). Devir süreci onay/ret/iptal adımlarından oluşur. Devir tamamlandığında eski sahip otomatik olarak paylaşılan kullanıcı listesine geçer, yeni sahip TaskShare'den çıkarılır |

---

## Güncel Veritabanı Şeması

```
User
  Id (PK, Guid)
  Email (unique, nvarchar(256))
  PasswordHash (nvarchar(256))
  Role (enum: Admin, User)
  SecurityStamp (nvarchar(64))
  TwoFactorEnabled (boolean)
  TwoFactorSecret (nvarchar(256), nullable)
  CreatedAt (DateTime, UTC)

TodoList (BaseAuditableEntity)
  Id (PK, Guid)
  Title (nvarchar(100))
  Color (nvarchar(20))
  OwnerId (FK → User.Id, NOT NULL, ON DELETE CASCADE)
  IsDeleted (boolean, default false)
  DeletedAt (DateTime, nullable)
  CreatedAt (DateTime, UTC)
  UpdatedAt (DateTime, nullable, UTC)

TodoItem (BaseAuditableEntity)
  Id (PK, Guid)
  OwnerId (FK → User.Id, NOT NULL, ON DELETE CASCADE)
  TodoListId (FK → TodoList.Id, nullable, ON DELETE SET NULL)
  Title (nvarchar(200))
  Description (nvarchar(2000), nullable)
  DueDate (DateTime, nullable)
  Status (enum: Open, Completed)
  Priority (enum: Low, Medium, High, Urgent)
  CompletedByUserId (FK → User.Id, nullable, ON DELETE NO ACTION)
  CompletedAt (DateTime, nullable)
  IsDeleted (boolean, default false, Global Query Filter)
  DeletedByUserId (FK → User.Id, nullable, ON DELETE NO ACTION)
  DeletedAt (DateTime, nullable)
  CreatedAt (DateTime, UTC)
  UpdatedAt (DateTime, nullable, UTC)

SubTask (BaseAuditableEntity)
  Id (PK, Guid)
  TaskId (FK → TodoItem.Id, NOT NULL, ON DELETE CASCADE)
  Title (nvarchar(200))
  Status (enum: Open, Completed)
  CreatedAt (DateTime, UTC)
  UpdatedAt (DateTime, nullable, UTC)

Tag (global)
  Id (PK, Guid)
  Name (nvarchar(50), unique, case-insensitive)
  CreatedByUserId (FK → User.Id, nullable, ON DELETE SET NULL)
  CreatedAt (DateTime, UTC)

TodoItemTag (ara tablo, N-N)
  TodoItemId (FK → TodoItem.Id, ON DELETE CASCADE)
  TagId (FK → Tag.Id, ON DELETE CASCADE)
  AssignedAt (DateTime, UTC)
  PK: (TodoItemId, TagId)

TaskShare (ara tablo, N-N)
  TaskId (FK → TodoItem.Id, ON DELETE CASCADE)
  UserId (FK → User.Id, ON DELETE CASCADE)
  SharedAt (DateTime, UTC)
  PK: (TaskId, UserId)

OwnershipTransferRequest
  Id (PK, Guid)
  TaskId (FK → TodoItem.Id, ON DELETE CASCADE)
  FromUserId (FK → User.Id, ON DELETE NO ACTION)
  ToUserId (FK → User.Id, ON DELETE NO ACTION)
  Status (enum: Pending, Accepted, Rejected, Cancelled)
  CreatedAt (DateTime, UTC)
  RespondedAt (DateTime, nullable, UTC)

TodoItemActivity (Audit Log)
  Id (PK, Guid)
  TaskId (FK → TodoItem.Id, ON DELETE CASCADE)
  UserId (FK → User.Id, ON DELETE CASCADE)
  Action (nvarchar(100))
  Details (nvarchar(1000), nullable)
  CreatedAt (DateTime, UTC)

RefreshToken
  Id (PK, Guid)
  UserId (FK → User.Id, ON DELETE CASCADE)
  TokenHash (nvarchar(256), SHA-256 hash)
  ExpiresAt (DateTime, UTC)
  CreatedAt (DateTime, UTC)
  RevokedAt (DateTime, nullable, UTC)
```

---

## Kural → Servis Katmanı Mantığı (Özet)

| Kural | Nerede Kontrol Edilir |
|---|---|
| BR-001 (kendine e-posta benzersizliği) | DB: Unique Index / Service: `_userRepository.GetByEmailAsync` kontrolü |
| BR-004 (kendine paylaşamama) | Service: `ShareTask` çağrısında `targetUserId == Task.OwnerId` ise hata |
| BR-008 / BR-026 (silme yetkisi) | Service: `TaskAuthorizationService.EnsureCanDeleteAsync` (yalnızca owner silebilir, aksi halde 404) |
| BR-010 (restore yetkisi) | Service: `TaskAuthorizationService.EnsureOwnerAsync` (yalnızca owner restore edebilir) |
| BR-011 (soft-delete filtreleme) | EF Core: `HasQueryFilter(t => !t.IsDeleted)` ile otomatik; çöp kutusunda `IgnoreQueryFilters()` |
| BR-013 / BR-014 (paylaşım yetkisi + duplicate) | Service: sadece owner çağırabilir; duplicate eklemede hata fırlatılmaz, sessizce yok sayılır |
| BR-020 (SubTask yetkisi) | Service: Paylaşılanlar ekleyebilir/tamamlayabilir; silme YALNIZCA ana görevin sahibine aittir |
| BR-022 (Tag oluşturma yetkisi) | Service / Controller: `[Authorize(Roles = "Admin")]` ve rol kontrolü |
| BR-025 (güncelleme/tamamlama) | Service: `TaskAuthorizationService.EnsureCanModifyAsync` ve `EnsureCanCompleteAsync` |
| BR-029 (yetkisiz erişim) | Controller / Service: yetkisiz isteklerde 403 yerine bilgi sızdırmayan 404 döner |
| BR-030 (sahiplik devri) | Service: `TaskTransferService` ile talep oluşturma, onay, ret ve iptal durum makineleri |
| BR-002 / 003 / 019 (cascade silmeler) | DB: `ON DELETE CASCADE` yapılandırmaları |

---

## Backlog İçin Not

Bu doküman artık kod yazımına (migration, endpoint, servis metodları, unit testler) doğrudan referans olarak kullanılabilir. Her BR-XXX kodu, ilgili task/PR açıklamasında referans olarak kullanılmalı (örn. "Implements BR-008a, BR-008b, BR-010").
