using Microsoft.Extensions.Options;
using TranslationService.Infrastructure;
using TranslationService.Settings;

namespace TranslationService.FunTranslations;

public class FunTranslationServiceClient : ServiceClientBase
{
    public FunTranslationServiceClient(IOptions<AppSettings> options, HttpClient httpClient) : base(httpClient)
    {
        httpClient.BaseAddress = options.Value.FunTranslations;
    }

    protected override string ErrorMessage => "FunTranslation API failed to return a response";

    public Task<string> ShakespeareTranslateAsync(string text, CancellationToken cancellationToken) =>
        TranslateAsync("translate/shakespeare", text, cancellationToken);

    public Task<string> YodaTranslateAsync(string text, CancellationToken cancellationToken) =>
        TranslateAsync("translate/yoda", text, cancellationToken);

    private async Task<string> TranslateAsync(string uri, string text, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string?, string?>("text", text)
        });

        var translation = await PostAsync<Translation>(uri, content, cancellationToken);
        return translation.Contents.Translated;
    }
}
