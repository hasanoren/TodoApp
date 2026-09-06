using FluentValidation;
using TodoApp.Application.DTOs;

namespace TodoApp.Application.Validators;

public class PaginatedRequestValidator : AbstractValidator<PaginatedRequest>
{
    public PaginatedRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Sayfa numarası en az 1 olmalıdır.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginatedRequest.MaxPageSize)
            .WithMessage($"Sayfa boyutu 1 ile {PaginatedRequest.MaxPageSize} arasında olmalıdır.");
    }
}
