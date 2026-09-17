namespace TodoApp.Application.DTOs;

public class UpdateTodoListRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ColorCode { get; set; }
}

