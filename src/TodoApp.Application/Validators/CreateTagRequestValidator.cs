using FluentValidation;
using TodoApp.Application.DTOs;

namespace TodoApp.Application.Validators;

public class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Etiket adı zorunludur.")
            .MaximumLength(50).WithMessage("Etiket adı en fazla 50 karakter olabilir.");
    }
}

