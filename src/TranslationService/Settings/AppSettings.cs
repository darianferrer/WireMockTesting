using System.ComponentModel.DataAnnotations;

namespace TranslationService.Settings;

public class AppSettings
{
    public const string Position = nameof(AppSettings);

    [Required]
    public Uri FunTranslations { get; set; }
}
