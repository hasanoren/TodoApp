using FluentValidation.TestHelper;
using TodoApp.Application.DTOs;
using TodoApp.Application.Validators;
using Xunit;

namespace TodoApp.Application.Tests.ValidatorTests;

public class EntityValidatorTests
{
    private readonly CreateTodoItemRequestValidator _createTodoItemValidator = new();
    private readonly UpdateTodoItemRequestValidator _updateTodoItemValidator = new();
    private readonly CreateSubTaskRequestValidator _createSubTaskValidator = new();
    private readonly CreateTagRequestValidator _createTagValidator = new();
    private readonly ShareTaskRequestValidator _shareTaskValidator = new();
    private readonly CreateTransferRequestDtoValidator _transferRequestValidator = new();

    // --- TodoItem Validators ---

    [Fact]
    public void CreateTodoItemValidator_WhenValid_ShouldNotHaveErrors()
    {
        var model = new CreateTodoItemRequest
        {
            Title = "Geçerli Başlık",
            Description = "Açıklama metni",
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        var result = _createTodoItemValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateTodoItemValidator_WhenTitleEmpty_ShouldHaveError()
    {
        var model = new CreateTodoItemRequest { Title = "" };
        var result = _createTodoItemValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void CreateTodoItemValidator_WhenTitleExceeds200Chars_ShouldHaveError()
    {
        var model = new CreateTodoItemRequest { Title = new string('A', 201) };
        var result = _createTodoItemValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void CreateTodoItemValidator_WhenDescriptionExceeds2000Chars_ShouldHaveError()
    {
        var model = new CreateTodoItemRequest
        {
            Title = "Başlık",
            Description = new string('A', 2001)
        };
        var result = _createTodoItemValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void UpdateTodoItemValidator_WhenTitleEmpty_ShouldHaveError()
    {
        var model = new UpdateTodoItemRequest { Title = "" };
        var result = _updateTodoItemValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    // --- SubTask Validator ---

    [Fact]
    public void CreateSubTaskValidator_WhenValid_ShouldNotHaveErrors()
    {
        var model = new CreateSubTaskRequest { Title = "Alt Görev" };
        var result = _createSubTaskValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateSubTaskValidator_WhenTitleEmptyOrTooLong_ShouldHaveError()
    {
        var emptyModel = new CreateSubTaskRequest { Title = "" };
        var longModel = new CreateSubTaskRequest { Title = new string('A', 201) };

        _createSubTaskValidator.TestValidate(emptyModel).ShouldHaveValidationErrorFor(x => x.Title);
        _createSubTaskValidator.TestValidate(longModel).ShouldHaveValidationErrorFor(x => x.Title);
    }

    // --- Tag Validator ---

    [Fact]
    public void CreateTagValidator_WhenValid_ShouldNotHaveErrors()
    {
        var model = new CreateTagRequest { Name = "Backend" };
        var result = _createTagValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateTagValidator_WhenNameEmptyOrTooLong_ShouldHaveError()
    {
        var emptyModel = new CreateTagRequest { Name = "" };
        var longModel = new CreateTagRequest { Name = new string('A', 51) };

        _createTagValidator.TestValidate(emptyModel).ShouldHaveValidationErrorFor(x => x.Name);
        _createTagValidator.TestValidate(longModel).ShouldHaveValidationErrorFor(x => x.Name);
    }

    // --- ShareTask Validator ---

    [Fact]
    public void ShareTaskValidator_WhenValid_ShouldNotHaveErrors()
    {
        var model = new ShareTaskRequest { Email = "colleague@example.com" };
        var result = _shareTaskValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ShareTaskValidator_WhenInvalidEmail_ShouldHaveError()
    {
        var model = new ShareTaskRequest { Email = "not-an-email" };
        var result = _shareTaskValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // --- TransferRequest Validator ---

    [Fact]
    public void CreateTransferRequestDtoValidator_WhenValid_ShouldNotHaveErrors()
    {
        var model = new CreateTransferRequestDto { NewOwnerEmail = "newowner@example.com" };
        var result = _transferRequestValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateTransferRequestDtoValidator_WhenInvalidEmail_ShouldHaveError()
    {
        var model = new CreateTransferRequestDto { NewOwnerEmail = "" };
        var result = _transferRequestValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.NewOwnerEmail);
    }
}

