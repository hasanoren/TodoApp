using FluentValidation;
using TodoApp.Application.DTOs;

namespace TodoApp.Application.Validators;

public class CreateTransferRequestDtoValidator : AbstractValidator<CreateTransferRequestDto>
{
    public CreateTransferRequestDtoValidator()
    {
        RuleFor(x => x.NewOwnerEmail)
            .NotEmpty().WithMessage("Yeni sahip e-posta adresi zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
            .MaximumLength(256).WithMessage("E-posta adresi en fazla 256 karakter olabilir.");
    }
}

