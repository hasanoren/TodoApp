namespace TodoApp.Application.DTOs;

public enum TaskFilterType
{
    All = 0,          // Kendi görevleri + Paylaşılanlar (varsayılan)
    OnlyMine = 1,     // Sadece kullanıcının sahip olduğu görevler
    SharedWithMe = 2, // Kendisiyle paylaşılan görevler
    SharedByMe = 3    // Sahip olduğu ve en az bir kullanıcıyla paylaştığı görevler
}

