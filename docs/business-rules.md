# To-Do App — Business Rules & Domain Model

## Entity Listesi
- **User** — sistem kullanıcısı (Role: Admin / User)
- **Task** — ana görev
- **SubTask** — alt görev
- **Tag** — global etiket
- **TaskShare** — paylaşım kaydı (ara tablo, N-N)

---

## Tam İş Kuralları Listesi (BR-001 → BR-029)

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
| BR-014 | Aynı kullanıcıyla aynı Task tekrar paylaşılmaya çalışılırsa → sessizce yok sayılır (hata dönmez) |
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
| BR-030 | Görev sahibi, görevin sahipliğini başka bir kayıtlı kullanıcıya devredebilir (Transfer Ownership). Devir sonrası eski sahip otomatik olarak paylaşılan kullanıcı olur, yeni sahip TaskShare'den çıkarılır |

---

## Güncel Veritabanı Şeması

```
User
  Id (PK)
  Email (unique)
  PasswordHash
  Role (enum: Admin, User)
  CreatedAt

Task
  Id (PK)
  OwnerId (FK → User.Id, NOT NULL, ON DELETE CASCADE)
  Title
  Description
  DueDate (nullable)
  Status (enum: Open, Completed)
  CompletedByUserId (FK → User.Id, nullable, ON DELETE SET NULL)   -- kim tamamladı, paylaşım kalksa da korunur
  CompletedAt (nullable)
  IsDeleted (boolean, default false)            -- sadece paylaşılan kullanıcı sildiğinde true olur
  DeletedByUserId (FK → User.Id, nullable, ON DELETE SET NULL)
  DeletedAt (nullable)
  CreatedAt

SubTask
  Id (PK)
  TaskId (FK → Task.Id, NOT NULL, ON DELETE CASCADE)
  Title
  Status (enum: Open, Completed)

Tag  (global)
  Id (PK)
  Name (unique, case-insensitive)
  CreatedByUserId (FK → User.Id, nullable, ON DELETE SET NULL — Admin silinse de Tag kalır, BR-023)

TaskTag (ara tablo, N-N)
  TaskId (FK → Task.Id)
  TagId (FK → Tag.Id)
  PK: (TaskId, TagId)

TaskShare (ara tablo, N-N)
  TaskId (FK → Task.Id)
  UserId (FK → User.Id)
  SharedAt
  PK: (TaskId, UserId)
```

---

## Kural → Servis Katmanı Mantığı (Özet)

| Kural | Nerede Kontrol Edilir |
|---|---|
| BR-004 (kendine paylaşamama) | Service: `ShareTask` çağrısında `targetUserId == Task.OwnerId` ise hata |
| BR-008a/b (silme davranışı) | Service: `DeleteTask` çağıran kullanıcı owner mı değil mi kontrol edilir, davranış dallanır |
| BR-013/014 (paylaşım yetkisi + duplicate) | Service: sadece owner çağırabilir; TaskShare PK zaten duplicate'i DB seviyesinde engeller, service bunu yakalayıp sessizce 200 döner |
| BR-022 (Tag oluşturma yetkisi) | Service: `CreateTag` çağıran kullanıcının Role'ü Admin değilse 403 |
| BR-025/026 (tamamlama/silme yetkisi) | Service: `currentUser == Task.OwnerId OR currentUser IN TaskShare(TaskId)` |
| BR-029 (yetkisiz erişim) | Controller/Service: yetki yoksa 404 döndürülür, 403 değil |
| BR-002/003/019 (cascade silmeler) | DB: `ON DELETE CASCADE` (User→Task, Task→SubTask); TaskShare için de User silinince cascade |

---

## Backlog İçin Not

Bu doküman artık kod yazımına (migration, endpoint, servis metodları, unit testler) doğrudan referans olarak kullanılabilir. Her BR-XXX kodu, ilgili task/PR açıklamasında referans olarak kullanılmalı (örn. "Implements BR-008a, BR-008b, BR-010").
