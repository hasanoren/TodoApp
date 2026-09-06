using FluentValidation;
using TodoApp.Application.DTOs;

namespace TodoApp.Application.Validators;

public class CreateSubTaskRequestValidator : AbstractValidator<CreateSubTaskRequest>
{
    public CreateSubTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Alt görev başlığı zorunludur.")
            .MaximumLength(200).WithMessage("Alt görev başlığı en fazla 200 karakter olabilir.");
    }
}

