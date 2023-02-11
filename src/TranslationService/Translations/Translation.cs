namespace TranslationService.Translations;

public record TranslationRequest(string Text, TranslationType Type);

public record TranslationResponse(string Text, string OriginalText, TranslationType Type);

public enum TranslationType : byte
{
    Unknown,
    None,
    Shakespeare,
    Yoda,
}
