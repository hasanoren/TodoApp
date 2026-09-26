namespace TodoApp.Domain.Common;

/// <summary>
/// Varlık Denetimi (Auditing) için sözleşme.
/// Bu arayüzü uygulayan entity'lerin CreatedAt ve UpdatedAt alanları
/// DbContext.SaveChangesAsync sırasında otomatik olarak UTC zaman damgası ile doldurulur.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}

public abstract class BaseAuditableEntity : IAuditableEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
