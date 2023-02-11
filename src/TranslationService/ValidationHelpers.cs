using FluentValidation.Results;

namespace TranslationService;

public static class ValidationHelpers
{
    public static Dictionary<string, string[]> GetValidationProblems(this ValidationResult validation)
        => validation.Errors.GroupBy(x => x.PropertyName, e => e.ErrorMessage).ToDictionary(x => x.Key, x => x.ToArray());
}