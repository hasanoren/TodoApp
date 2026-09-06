namespace TodoApp.Application.DTOs;

public class PaginatedRequest
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;
}
