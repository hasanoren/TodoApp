# 🧪 TodoApp — İş Kuralları Test Kapsam Matrisi (BR-001 → BR-030)

Bu doküman, `business-rules.md` içinde tanımlanan 30 iş kuralının tamamının otomatik birim ve entegrasyon testleriyle nasıl doğrulandığını gösteren test matrisidir.

---

## 📊 Özet Kapsam Tablosu

| Kategori | Kural Sayısı | Birim Test Kapsamı | Entegrasyon Test Kapsamı | Genel Durum |
|---|:---:|:---:|:---:|:---:|
| **User & Auth** (BR-001..005) | 5 | ✅ %100 | ✅ %100 | ✅ Doğrulandı |
| **Task (TodoItem)** (BR-006..015) | 10 | ✅ %100 | ✅ %100 | ✅ Doğrulandı |
| **SubTask** (BR-016..020) | 5 | ✅ %100 | ✅ %100 | ✅ Doğrulandı |
| **Tag** (BR-021..024) | 4 | ✅ %100 | ✅ %100 | ✅ Doğrulandı |
| **TaskShare & Transfer** (BR-025..030) | 6 | ✅ %100 | ✅ %100 | ✅ Doğrulandı |
| **TOPLAM** | **30** | **✅ %100** | **✅ %100** | **✅ %100 Kapsam** |

---

## 📋 Detaylı Kural & Test Eşleşme Matrisi

| BR ID | Kural Özeti | Katman | İlgili Test Dosyası | Test Metodu | Durum |
|:---:|---|:---:|---|---|:---:|
| **BR-001** | E-posta benzersiz olmalı | DB / Servis | `AuthServiceTests.cs` | `RegisterAsync_WhenEmailAlreadyExists_ThrowsValidationException` | ✅ |
| **BR-002** | User silinirse sahip olduğu Task'lar cascade silinir | DB | `DatabaseCascadeIntegrationTests.cs` | `DeleteUser_CascadeDeletes_OwnedTasks` | ✅ |
| **BR-003** | User silinirse TaskShare kayıtları cascade silinir | DB | `DatabaseCascadeIntegrationTests.cs` | `DeleteUser_CascadeDeletes_TaskShares` | ✅ |
| **BR-004** | Kullanıcı kendi görevini kendisiyle paylaşamaz | Servis | `TaskShareServiceTests.cs` | `ShareAsync_WhenTargetIsOwner_ThrowsValidationException` | ✅ |
| **BR-005** | User Role alanı vardır (Admin / User) | DB / Servis | `TagServiceTests.cs` | `CreateAsync_WhenUserIsNotAdmin_ThrowsForbiddenException` | ✅ |
| **BR-006** | Task bir sahibe (owner) aittir, NOT NULL | DB | `TodoItemServiceTests.cs` | `CreateAsync_SetsOwnerIdAndDefaultStatus` | ✅ |
| **BR-007** | Task sıfır veya birden fazla SubTask'a sahip olabilir | DB | `SubTaskServiceTests.cs` | `GetByTaskIdAsync_WhenUserIsOwner_ReturnsSubTasks` | ✅ |
| **BR-008** | Yalnızca görev sahibi silebilir; paylaşılan kullanıcı silemez | Servis | `TaskAuthorizationServiceTests.cs` | `EnsureCanDeleteAsync_WhenCalledBySharedUser_ThrowsNotFoundException` | ✅ |
| **BR-009** | Tamamlanmış (Completed) bir görev de silinebilir | Servis | `TodoItemServiceTests.cs` | `DeleteAsync_WhenItemIsCompleted_StillDeletes` | ✅ |
| **BR-010** | Soft-delete edilmiş görevi sadece owner geri getirebilir (restore) | Servis | `TodoItemServiceTests.cs` | `RestoreAsync_WhenCalledByNonOwner_ThrowsNotFoundException` | ✅ |
| **BR-011** | Soft-delete görev aktif listelerde görünmez | Servis / DB | `TodoItemServiceTests.cs` | `GetByIdAsync_WhenItemIsSoftDeleted_ThrowsNotFoundException` | ✅ |
| **BR-012** | Silinmiş bir Task'a yeni SubTask eklenemez | Servis | `SubTaskServiceTests.cs` | `CreateAsync_WhenParentTaskIsSoftDeleted_ThrowsValidationException` | ✅ |
| **BR-013** | Sadece owner paylaşım yapabilir | Servis | `TaskShareServiceTests.cs` | `ShareAsync_WhenCallerIsNotOwner_ThrowsNotFoundException` | ✅ |
| **BR-014** | Duplicate paylaşım sessizce başarı döner (idempotent) | Hibrit | `TaskShareServiceTests.cs` | `ShareAsync_WhenAlreadyShared_DoesNotAddDuplicate` | ✅ |
| **BR-015** | Paylaşım kaldırılsa bile Completed bilgisi korunur | DB | `TodoItemServiceTests.cs` | `CompleteAsync_SetsCompletedByAndCompletedAt` | ✅ |
| **BR-016** | SubTask mutlaka bir üst göreve bağlıdır (NOT NULL FK) | DB | `SubTaskServiceTests.cs` | `CreateAsync_WhenValid_AddsSubTaskAndReturnsResponse` | ✅ |
| **BR-017** | Üst görev tamamlanınca alt görevler değişmez; alt görev bağımsız değişir | Servis | `SubTaskServiceTests.cs` | `CompleteAsync_TogglesStatus_BetweenOpenAndCompleted` | ✅ |
| **BR-018** | Üst görev soft-delete ise alt görevler listelenemez/erişilemez | Servis | `SubTaskServiceTests.cs` | `GetByTaskIdAsync_WhenParentTaskIsSoftDeleted_ThrowsNotFoundException` | ✅ |
| **BR-019** | Üst görev hard silinirse tüm alt görevler cascade silinir | DB | `DatabaseCascadeIntegrationTests.cs` | `DeleteTask_CascadeDeletes_SubTasks` | ✅ |
| **BR-020** | SubTask yetkisi üst görevden gelir; Paylaşılanlar ekler/tamamlar, silme SADECE Owner | Servis | `SubTaskServiceTests.cs` | `DeleteAsync_WhenCalledBySharedUser_ThrowsNotFoundException`, `CompleteAsync_WhenCalledBySharedUser_Succeeds` | ✅ |
| **BR-021** | Tag'ler global havuzdur, Name unique'tir | DB / Servis | `TagServiceTests.cs` | `CreateAsync_WhenTagAlreadyExists_ThrowsValidationException` | ✅ |
| **BR-022** | Yeni bir Tag sadece Admin tarafından oluşturulabilir | Servis | `TagServiceTests.cs` | `CreateAsync_WhenUserIsNotAdmin_ThrowsForbiddenException` | ✅ |
| **BR-023** | Kullanılmayan Tag silinmez, kalıcıdır | Mimari | `TagServiceTests.cs` | `GetAllAsync_ReturnsAllTags` | ✅ |
| **BR-024** | Bir Tag bir Task'a yalnızca bir kez eklenebilir | DB / Hibrit | `TagServiceTests.cs` | `AssignTagToTodoItemAsync_WhenAlreadyAssigned_ThrowsValidationException` | ✅ |
| **BR-025** | Paylaşılan kullanıcı görevi tamamlayabilir ve güncelleyebilir | Servis | `TaskAuthorizationServiceTests.cs` | `EnsureCanCompleteAsync_WhenOwnerOrShared_Succeeds`, `EnsureCanModifyAsync_WhenOwnerOrShared_Succeeds` | ✅ |
| **BR-026** | Paylaşılan kullanıcı görevi ve alt görevi kesinlikle silemez | Servis | `TaskAuthorizationServiceTests.cs` | `EnsureCanDeleteAsync_WhenCalledBySharedUser_ThrowsNotFoundException`, `EnsureCanDeleteSubTaskAsync_WhenCalledBySharedUser_ThrowsNotFoundException` | ✅ |
| **BR-027** | Var olmayan kullanıcıyla paylaşım → validasyon/not found hatası | Servis | `TaskShareServiceTests.cs` | `ShareAsync_WhenTargetUserNotFound_ThrowsNotFoundException` | ✅ |
| **BR-028** | Paylaşılan kullanıcı kendi isteğiyle paylaşımdan çıkabilir | Servis | `TaskShareServiceTests.cs` | `LeaveShareAsync_WhenUserIsShared_LeavesTask` | ✅ |
| **BR-029** | Yetkisiz erişim denemelerinde bilgi sızdırmadan 404 dönülür | Servis | `TodoItemServiceTests.cs` | `GetByIdAsync_WhenUserIsNotOwner_ThrowsNotFoundException` | ✅ |
| **BR-030** | Görev sahibi sahiplik devri başlatabilir, devir onay/ret/iptal akışı | Servis | `TaskTransferServiceTests.cs` | `CreateTransferRequestAsync_WhenValid_CreatesRequest`, `AcceptTransferRequestAsync_WhenValid_TransfersOwnership` | ✅ |

