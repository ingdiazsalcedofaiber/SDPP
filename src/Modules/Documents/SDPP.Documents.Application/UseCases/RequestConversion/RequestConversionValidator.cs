using FluentValidation;

namespace SDPP.Documents.Application.UseCases.RequestConversion;

public sealed class RequestConversionValidator : AbstractValidator<RequestConversionCommand>
{
    public RequestConversionValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
    }
}
