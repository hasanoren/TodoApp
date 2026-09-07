using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface ITaskAuthorizationService
{
    /// <summary>
    /// BR-029: Kullanıcının görevi okuma yetkisini doğrular (Sahip veya Paylaşılan).
    /// Görev silinmişse ve allowTrash false ise 404 fırlatır (BR-011, BR-018).
    /// </summary>
    Task<TodoItem> EnsureCanReadAsync(Guid taskId, Guid userId, bool allowTrash = false);

    /// <summary>
    /// BR-025: Kullanıcının görevi düzenleme (başlık, açıklama, tarih) yetkisini doğrular (Sahip veya Paylaşılan).
    /// Silinmiş görev düzenlenemez (404).
    /// </summary>
    Task<TodoItem> EnsureCanModifyAsync(Guid taskId, Guid userId);

    /// <summary>
    /// BR-025: Kullanıcının görevi tamamlama/açma yetkisini doğrular (Sahip veya Paylaşılan).
    /// Silinmiş görev tamamlanamaz (404).
    /// </summary>
    Task<TodoItem> EnsureCanCompleteAsync(Guid taskId, Guid userId);

    /// <summary>
    /// BR-008, BR-026: Kullanıcının görevi silme yetkisini doğrular.
    /// YALNIZCA görev sahibi (Owner) silebilir. Paylaşılan veya yabancı kullanıcı 404 alır.
    /// </summary>
    Task<TodoItem> EnsureCanDeleteAsync(Guid taskId, Guid userId);

    /// <summary>
    /// BR-010, BR-013, BR-030: Kullanıcının görevin mutlak sahibi (Owner) olduğunu doğrular.
    /// Paylaşım yönetimi, sahiplik devri, geri yükleme ve kalıcı silme için kullanılır.
    /// </summary>
    Task<TodoItem> EnsureOwnerAsync(Guid taskId, Guid userId);

    /// <summary>
    /// BR-012, BR-020: Kullanıcının ana göreve alt görev ekleme veya alt görevleri listeleme yetkisini doğrular (Sahip veya Paylaşılan).
    /// Ana görev silinmişse alt görev eklenemez/listelenemez (BR-012, BR-018).
    /// </summary>
    Task<TodoItem> EnsureCanManageSubTasksAsync(Guid taskId, Guid userId);

    /// <summary>
    /// BR-020: Alt görevi tamamlama/açma yetkisini doğrular (Sahip veya Paylaşılan).
    /// Üst görev silinmişse 404 fırlatır.
    /// </summary>
    Task<SubTask> EnsureCanCompleteSubTaskAsync(Guid subTaskId, Guid userId);

    /// <summary>
    /// BR-020, BR-026, BR-029: Alt görevi silme yetkisini doğrular.
    /// YALNIZCA üst görevin sahibi (Owner) silebilir. Paylaşılan kullanıcı alt görevi silemez (404 döner).
    /// </summary>
    Task<SubTask> EnsureCanDeleteSubTaskAsync(Guid subTaskId, Guid userId);
}

