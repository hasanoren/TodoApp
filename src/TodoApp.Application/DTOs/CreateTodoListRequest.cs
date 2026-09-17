namespace TodoApp.Application.DTOs;

public class CreateTodoListRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ColorCode { get; set; }
}

