using TodoApp.Domain.Entities;

namespace TodoApp.Application.DTOs;

public class TodoItemFilterDto : PaginatedRequest
{
    public TaskFilterType FilterType { get; set; } = TaskFilterType.All;
    public string? Search { get; set; }
    public TodoItemStatus? Status { get; set; }
    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }
    public TodoApp.Domain.Enums.TodoItemPriority? Priority { get; set; }
    public Guid? TodoListId { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public string? SortOrder { get; set; } = "desc";
}

