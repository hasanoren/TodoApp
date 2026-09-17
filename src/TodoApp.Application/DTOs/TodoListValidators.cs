using FluentValidation;

namespace TodoApp.Application.DTOs;

public class CreateTodoListRequestValidator : AbstractValidator<CreateTodoListRequest>
{
    public CreateTodoListRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Liste adı boş olamaz.")
            .MaximumLength(100).WithMessage("Liste adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.ColorCode)
            .MaximumLength(7).WithMessage("Renk kodu en fazla 7 karakter olabilir (#RRGGBB).");
    }
}

public class UpdateTodoListRequestValidator : AbstractValidator<UpdateTodoListRequest>
{
    public UpdateTodoListRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Liste adı boş olamaz.")
            .MaximumLength(100).WithMessage("Liste adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.ColorCode)
            .MaximumLength(7).WithMessage("Renk kodu en fazla 7 karakter olabilir (#RRGGBB).");
    }
}

