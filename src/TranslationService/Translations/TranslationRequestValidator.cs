using FluentValidation;

namespace TranslationService.Translations;

public class TranslationRequestValidator : AbstractValidator<TranslationRequest>
{
    public TranslationRequestValidator()
    {
        RuleFor(c => c.Text)
            .NotEmpty();
        RuleFor(c => c.Type)
            .IsInEnum()
            .NotEqual(TranslationType.Unknown);
    }
}
