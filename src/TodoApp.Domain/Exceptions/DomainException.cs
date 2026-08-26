namespace TodoApp.Domain.Exceptions;

// Tüm özel exception'larımızın temel sınıfı
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

// Kaynak zaten var (örn. email zaten kayıtlı) → 409 Conflict
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}

// Kaynak bulunamadı → 404 Not Found
public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }
}

// Yetkisiz işlem (BR-013, BR-025 gibi) → 403 Forbidden (veya bazı durumlarda 404, BR-029'da olduğu gibi)
public class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message) { }
}

// Geçersiz istek/validasyon hatası (BR-004, BR-027 gibi) → 400 Bad Request
public class ValidationException : DomainException
{
    public ValidationException(string message) : base(message) { }
}