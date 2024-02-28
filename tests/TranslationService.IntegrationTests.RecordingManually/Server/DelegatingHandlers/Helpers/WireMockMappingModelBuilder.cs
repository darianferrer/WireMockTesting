using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using WireMock.Admin.Mappings;

namespace TranslationService.IntegrationTests.RecordingManually.Server.DelegatingHandlers;

internal class WireMockMappingModelBuilder
{
    private const string WildcardMatcher = "WildcardMatcher";
    private readonly Lazy<TestConfiguration> _testConfiguration;

    public WireMockMappingModelBuilder(IOptionsMonitor<TestConfiguration> testConfiguration)
    {
        _testConfiguration = new(() => testConfiguration.CurrentValue);
    }

    public async Task<MappingModel> GetFromHttpResponseAsync(
        HttpResponseMessage response,
        TestConfiguration testConfiguration,
        JsonSerializerSettings jsonSerializerSettings,
        CancellationToken cancellationToken = default)
    {
        var request = response.RequestMessage!;
        var parameters = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri!.Query);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        var serviceKey = GetServiceConfigName(request.RequestUri!);
        var serverConfig = GetMockedServerConfiguration(serviceKey, testConfiguration);

        var requestBuilder = new RequestModelBuilder()
            .WithMethods(request.Method.ToString())
            .WithHeaders(BuildRequestHeaders(request, serverConfig))
            .WithPath(WebUtility.UrlDecode(request.RequestUri!.AbsolutePath));

        var paramModels = BuildParams(parameters);
        if (paramModels is { Count: > 0 })
        {
            requestBuilder.WithParams(paramModels);
        }

        var contentModel = await BuildBodyAsync(request.Content, cancellationToken);
        if (contentModel is not null)
        {
            requestBuilder.WithBody(contentModel);
        }

        var responseBuilder = new ResponseModelBuilder()
            .WithStatusCode(response.StatusCode)
            .WithHeaders(BuildResponseHeaders(response, serverConfig))
            // TODO: Add support for other formats like xml
            .WithBodyAsJson(JsonConvert.DeserializeObject(responseBody, jsonSerializerSettings))
            .WithBodyAsJsonIndented(true);

        var content = new MappingModelBuilder()
            .WithRequest(requestBuilder.Build())
            .WithResponse(responseBuilder.Build())
            .Build();

        return content;
    }

    private static List<HeaderModel> BuildRequestHeaders(HttpRequestMessage request, MockedServerConfiguration serverConfig) => request.Headers
        .Where(x => !serverConfig.IgnoredRequestHeaders.Select(x => x.ToLower()).Contains(x.Key.ToLower()))
        .Select(h => new HeaderModelBuilder().WithName(h.Key).WithMatchers(h.Value.Select(v => BuildMatcher(v)).ToList()).Build())
        .ToList();

    private static Dictionary<string, object> BuildResponseHeaders(HttpResponseMessage response, MockedServerConfiguration serverConfig) => response.Headers.Concat(response.Content.Headers)
        .Where(x => !serverConfig.IgnoredResponseHeaders.Select(x => x.ToLower()).Contains(x.Key.ToLower()))
        .ToDictionary(x => x.Key, x => (object)x.Value);

    private static async Task<BodyModel?> BuildBodyAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null) return null;

        var stringContent = await content.ReadAsStringAsync(cancellationToken);
        return stringContent is null
            ? null
            : new BodyModelBuilder()
                .WithMatcher(BuildMatcher(stringContent))
                .Build();
    }

    private static List<ParamModel> BuildParams(Dictionary<string, StringValues> parameters) => parameters
        .Select(x => new ParamModelBuilder().WithName(x.Key).WithMatchers(BuildMatcher(x.Value!)).Build())
        .ToList();

    private static MatcherModel BuildMatcher(string v)
        => new MatcherModelBuilder().WithName(WildcardMatcher).WithPattern(v).WithIgnoreCase(true).Build();

    private string GetServiceConfigName(Uri requestUri) =>
        _testConfiguration.Value.FindServiceConfiguration(requestUri)?.Name
        ?? throw new Exception($"'{requestUri}' doesn't  match with an existing mocked server");

    private static MockedServerConfiguration GetMockedServerConfiguration(
        string serverName,
        TestConfiguration testConfiguration)
    {
        return testConfiguration.FindServiceConfiguration(serverName)
            ?? throw new Exception("Couldn't find server configuration");
    }
}
