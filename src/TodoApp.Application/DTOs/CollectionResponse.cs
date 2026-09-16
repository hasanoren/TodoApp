namespace TodoApp.Application.DTOs;

/// <summary>
/// Düz dizi (naked array) yerine genişletilebilir JSON nesnesi dönmek için generic sarmalayıcı (T8.2.9).
/// JSON çıktısı: { "items": [ ... ] }
/// </summary>
public class CollectionResponse<T>
{
    public List<T> Items { get; init; } = new();

    public CollectionResponse() { }

    public CollectionResponse(List<T> items)
    {
        Items = items;
    }

    public CollectionResponse(IEnumerable<T> items)
    {
        Items = items.ToList();
    }
}

