# 🛡️ TodoApp — Mimari İnceleme & Karşılaştırma Raporu

**Karşılaştırma Kapsamı:** `feature/subtask-crud` (`adf14c9` — 31 Ağustos 2026) ➔ `feature/tag-management` (`a9fc3fe` — 2 Eylül 2026)  
**İncelenen EPIC:** **EPIC 4 — Etiket Yönetimi & Çoktan Çoğa (N-N) İlişki Mimarisi**  
**Doküman Türü:** Mimari Gelişim, Kod Karşılaştırması ve Öğrenim Rehberi  

---

## 1. Genel Bakış ve Yönetici Özeti

Bu rapor, **TodoApp** projesinde alt görevlerin (SubTask) tamamlandığı aşamadan, etiket yönetiminin (Tag) ve gelişmiş ilişki yapısının eklendiği aşamaya geçiş sürecinde yapılan tüm mimari iyileştirmeleri, veritabanı şeması genişletmelerini, kod refactor'larını ve test stratejilerini detaylandırmaktadır.

### 📊 Metrik ve Özellik Karşılaştırma Tablosu

| Metrik / Alan | `feature/subtask-crud` (Önceki) | `feature/tag-management` (Sonraki) | Değişim / Kazanım |
|---|---|---|---|
| **Toplam Entity Sayısı** | 3 (`User`, `TodoItem`, `SubTask`) | 5 (+ `Tag`, + `TodoItemTag`) | Çoktan çoğa (N-N) model eklendi |
| **İlişki Mimarisi** | 1-N (`TodoItem` ➔ `SubTask`) | 1-N ve N-N (Composite PK) | Ara tablo ile esnek etiketleme |
| **Sorgu Yükleme (Query)** | Temel sorgular (Lazy / Null collections) | **Eager Loading** (`Include` / `ThenInclude`) | N+1 sorgu problemi önlendi |
| **Yetkilendirme (Auth)** | Owner bazlı (Sadece `User` rolü) | **Admin & User Rol Ayrımı** (BR-022) | Rol bazlı API kısıtlaması |
| **Payload Optimizasyonu** | Statik alanlar | **Liste vs Detay ayrımı** | Listelerde hafif, detayda tam veri |
| **Birim Testleri** | 9 SubTask Testi + Temel Testler | +12 TagService Testi + Eager Testleri | %100 İş kuralı kapsamı |
| **RESTful Endpoint Sayısı** | 11 Endpoint | 16 Endpoint (+5 yeni Tag endpoint'i) | Kapsamlı etiketleme API'si |

---

## 2. Domain Katmanı Değişiklikleri (Entity & İlişki Tasarımı)

`feature/subtask-crud` dalında görevler yalnızca alt görevlere (`SubTask`) sahipti. `feature/tag-management` dalı ile birlikte etiketlerin global bir havuzda yönetilmesi (BR-021) ve bir göreve birden fazla etiket bağlanabilmesi (BR-024) sağlandı.

### 🆕 1. `Tag.cs` (Yeni Eklendi — Global Etiket Havuzu)
```csharp
namespace TodoApp.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; }

    // BR-021 & BR-023: Global etiket adı (Unique, case-insensitive)
    public string Name { get; set; } = string.Empty;

    // BR-023: Admin silinse bile Tag kalır (CreatedByUserId ON DELETE SET NULL)
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TodoItemTag> TodoItemTags { get; set; } = new List<TodoItemTag>();
}
```

### 🆕 2. `TodoItemTag.cs` (Yeni Eklendi — Composite PK Ara Tablo)
```csharp
namespace TodoApp.Domain.Entities;

public class TodoItemTag
{
    // BR-024: Composite PK (TodoItemId + TagId) — Aynı Tag aynı Task'a iki kez eklenemez!
    public Guid TodoItemId { get; set; }
    public TodoItem TodoItem { get; set; } = null!;

    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
```

### 🔄 3. `TodoItem.cs` Karşılaştırması (Güncellendi)

#### ❌ Eski Hali (`subtask-crud`):
```csharp
public class TodoItem
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public TodoItemStatus Status { get; set; } = TodoItemStatus.Open;
    public ICollection<SubTask> SubTasks { get; set; } = new List<SubTask>();
}
```

#### ✅ Yeni Hali (`tag-management`):
```csharp
public class TodoItem
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public TodoItemStatus Status { get; set; } = TodoItemStatus.Open;
    public ICollection<SubTask> SubTasks { get; set; } = new List<SubTask>();

    // BR-024: Bir görev sıfır veya daha fazla etikete (Tag) sahip olabilir (YENİ EKLENDİ)
    public ICollection<TodoItemTag> TodoItemTags { get; set; } = new List<TodoItemTag>();
}
```

---

## 3. Veritabanı & EF Core Fluent API Yapılandırması

İş kurallarının veritabanı seviyesinde garanti altına alınması için `ApplicationDbContext.cs` üzerinde kritik Fluent API tanımlamaları yapıldı:

```csharp
// ApplicationDbContext.cs (ModelBuilder konfigürasyonları)

// 1. Tag yapılandırması
// BR-021: Global etiket adı (Unique)
modelBuilder.Entity<Tag>()
    .HasIndex(t => t.Name)
    .IsUnique();

// BR-023: Admin silinse bile Tag kalır (CreatedByUserId ON DELETE SET NULL)
modelBuilder.Entity<Tag>()
    .HasOne(t => t.CreatedByUser)
    .WithMany()
    .HasForeignKey(t => t.CreatedByUserId)
    .OnDelete(DeleteBehavior.SetNull);

// 2. TodoItemTag Composite Key ve İlişki yapılandırması
// BR-024: Composite PK (TodoItemId + TagId) — Aynı Tag aynı Task'a iki kez eklenemez
modelBuilder.Entity<TodoItemTag>()
    .HasKey(tit => new { tit.TodoItemId, tit.TagId });

modelBuilder.Entity<TodoItemTag>()
    .HasOne(tit => tit.TodoItem)
    .WithMany(t => t.TodoItemTags)
    .HasForeignKey(tit => tit.TodoItemId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<TodoItemTag>()
    .HasOne(tit => tit.Tag)
    .WithMany(t => t.TodoItemTags)
    .HasForeignKey(tit => tit.TagId)
    .OnDelete(DeleteBehavior.Cascade);
```

> [!IMPORTANT]
> **Veritabanı Seviyesinde Güvenceler:**
> 1. **Composite PK (`HasKey`):** `TodoItemId` ve `TagId` çifti birleşik anahtar yapılarak aynı etiketin aynı göreve mükerrer eklenmesi DB seviyesinde imkansız hale getirildi.
> 2. **`SetNull` Davranışı:** Etiketi oluşturan Admin hesabı silinse bile etiketler sistemde kalmaya devam eder.
> 3. **`Cascade` Davranışı:** Görev silindiğinde ara tablodaki ilişki kayıtları otomatik silinir; ancak global `Tags` tablosuna dokunulmaz.

---

## 4. Repository Katmanı & Eager Loading (N+1 Önleme) İyileştirmesi

### 🔍 Sorun (`subtask-crud` Dalında Ne Eksikti?):
`subtask-crud` dalında `TodoItemRepository.GetByIdAsync` çağrıldığında ilişkili koleksiyonlar `Include` edilmiyordu. Bu durum, detay servisinde `SubTasks` koleksiyonunun `null` kalmasına ya da erişilmek istendiğinde N+1 sorgusu tetiklenmesine yol açıyordu.

### 💡 Çözüm (`tag-management` Dalındaki İyileştirme):
EF Core **Eager Loading** (`Include` / `ThenInclude`) mekanizması devreye alındı.

```csharp
// TodoItemRepository.cs — Karşılaştırma

// ❌ ESKİ (subtask-crud):
public async Task<TodoItem?> GetByIdAsync(Guid id)
{
    return await _context.TodoItems
        .FirstOrDefaultAsync(t => t.Id == id);
}

// ✅ YENİ (tag-management):
public async Task<TodoItem?> GetByIdAsync(Guid id)
{
    return await _context.TodoItems
        .Include(t => t.SubTasks)
        .Include(t => t.TodoItemTags)
            .ThenInclude(tit => tit.Tag)
        .FirstOrDefaultAsync(t => t.Id == id);
}
```

### ⚡ Payload & Performans Optimizasyonu Stratejisi:
* **Liste Sorguları (`GetAccessibleByUserAsync`):** Kullanıcı yüzlerce görev listelerken her görevin tüm alt görevlerini RAM'e yüklemek performansı düşürür. Bu nedenle liste sorgusunda **yalnızca `TodoItemTags` include edildi**, `SubTasks` hariç tutuldu.
* **Tekil Detay Sorgusu (`GetByIdAsync`):** Kullanıcı tek bir görevi detaylı incelediği için hem `SubTasks` hem de `TodoItemTags` eksiksiz yüklendi.

---

## 5. Application / Servis Katmanı & İş Kuralları Uygulaması

Yeni eklenen `ITagService` ve `TagService` sınıfları şu iş kurallarını uçtan uca yönetmektedir:

### 📋 Servis İş Kuralları Matrisi

| Kural Kodu | İş Kuralı Açıklaması | `TagService.cs` Kod Uygulaması |
|---|---|---|
| **BR-022** | Sadece **Admin** etiket oluşturabilir | `if (user.Role != UserRole.Admin) throw new ForbiddenException("Etiket oluşturma yetkisi sadece yöneticilere aittir.");` |
| **BR-021** | Etiket adı unique ve case-insensitive olmalı | `var existing = await _tagRepository.GetByNameAsync(request.Name.Trim().ToLower()); if (existing != null) throw new ValidationException(...);` |
| **BR-024** | Bir etiket göreve yalnızca 1 kez eklenebilir | `var existingRelation = await _tagRepository.GetTodoItemTagAsync(taskId, tagId); if (existingRelation != null) throw new ValidationException(...);` |
| **BR-029** | Yetkisiz erişimde 404 dönmeli (bilgi sızdırmaz) | `if (task is null \|\| task.OwnerId != userId) throw new NotFoundException("Görev bulunamadı.");` |

### 🔄 DTO ve Response Genişletmesi
[`TodoItemResponse.cs`](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Application/DTOs/TodoItemResponse.cs) nesnesi genişletilerek `Tags` ve `SubTasks` koleksiyonları response DTO'suna eklendi:

```csharp
public class TodoItemResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }

    // YENİ EKLENEN KOLEKSİYONLAR:
    public List<SubTaskResponse> SubTasks { get; set; } = new();
    public List<TagResponse> Tags { get; set; } = new();
}
```

---

## 6. API Katmanı & Yeni RESTful Endpoint'ler

[`TagsController.cs`](file:///c:/Projects/TodoApp/TodoApp/src/TodoApp.Api/Controllers/TagsController.cs) oluşturularak 5 yeni endpoint sisteme kazandırıldı:

```
[POST]   /api/tags                           -> Sadece Admin (BR-021, BR-022)
[GET]    /api/tags                           -> Giriş yapmış tüm kullanıcılar (BR-021)
[POST]   /api/todoitems/{taskId}/tags/{tagId}-> Görev Sahibi (BR-024)
[DELETE] /api/todoitems/{taskId}/tags/{tagId}-> Görev Sahibi (BR-023: Etiket silinmez, ilişki kalkar)
[GET]    /api/tags/{tagId}/todoitems         -> Belirli bir etikete sahip aktif görevleri listeleme
```

### 🔒 Rol Bazlı Güvenlik Örneği:
```csharp
[Authorize(Roles = "Admin")]
[HttpPost]
public async Task<IActionResult> Create(CreateTagRequest request)
{
    var userId = GetCurrentUserId();
    var result = await _tagService.CreateAsync(userId, request);
    return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
}
```

---

## 7. Test & Kalite Katmanı (Unit Tests)

`feature/tag-management` dalında 12 yeni birim testi içeren [`TagServiceTests.cs`](file:///c:/Projects/TodoApp/TodoApp/tests/TodoApp.Application.Tests/TagServiceTests.cs) ve güncellenen `TodoItemServiceTests.cs` eklendi:

1. `CreateAsync_WhenUserIsAdmin_CreatesTagAndReturnsResponse` ✅
2. `CreateAsync_WhenUserIsNotAdmin_ThrowsForbiddenException` (BR-022) ✅
3. `CreateAsync_WhenTagAlreadyExists_ThrowsValidationException` (BR-021 Case-Insensitive) ✅
4. `CreateAsync_WhenNameIsEmpty_ThrowsValidationException` ✅
5. `GetAllAsync_ReturnsAllTagsOrderedByName` ✅
6. `AssignTagToTodoItemAsync_WhenValid_AddsRelation` (BR-024) ✅
7. `AssignTagToTodoItemAsync_WhenAlreadyAssigned_ThrowsValidationException` (BR-024) ✅
8. `AssignTagToTodoItemAsync_WhenTaskNotOwned_ThrowsNotFoundException` (BR-029) ✅
9. `AssignTagToTodoItemAsync_WhenTaskNotFound_ThrowsNotFoundException` ✅
10. `AssignTagToTodoItemAsync_WhenTagNotFound_ThrowsNotFoundException` ✅
11. `RemoveTagFromTaskAsync_WhenValid_RemovesRelation` ✅
12. `RemoveTagFromTaskAsync_WhenRelationNotFound_ThrowsNotFoundException` ✅

---

## 8. Öğrenilen Dersler & Mimari Çıkarımlar (Best Practices)

### 🎓 1. Çoktan Çoğa İlişkilerde Açık Ara Tablo (Explicit Join Entity)
* EF Core 5+ ile örtük (implicit) N-N ilişkiler desteklense de, `TodoItemTag` gibi **açık bir ara tablo sınıfı** tanımlamak hem `AssignedAt` gibi ek ilişki alanları tutabilmeyi hem de Composite Key kısıtlamalarını net yönetebilmeyi sağlar.

### 🎓 2. Eager Loading ile N+1 Probleminin Önlenmesi
* İlişkili verileri (`SubTasks`, `Tags`) tekil detayda `Include().ThenInclude()` ile çekmek, veritabanına giden ek sorgu sayısını 1'e indirir ve performans kazandırır.

### 🎓 3. Liste ve Detay Ayrımı (Payload & Memory Management)
* Her endpoint'te tüm ilişkileri yüklemek yerine, liste görünümlerinde sadece özet verileri (hafif DTO), tekil detay sayfasında ise tüm alt koleksiyonları dönmek API'nin yüksek trafikte kararlı kalmasını sağlar.

### 🎓 4. Güvenlikte 404 vs 403 Standardı (BR-029 — IDOR Koruması)
* Kullanıcının erişim yetkisi olmayan bir kaynağa (başkasının görevi veya etiketi) erişmeye çalıştığında `403 Forbidden` yerine `404 Not Found` dönülmesi, sistemdeki ID ve kaynakların dışarıya sızdırılmasını (Enumeration Attack) engeller.

