using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using WireMock.Admin.Mappings;

namespace TranslationService.IntegrationTests.RecordingManually.Server.DelegatingHandlers;

internal class RecordHttpCallDelegatingHandler : DelegatingHandler
{
    private readonly Lazy<TestConfiguration> _testConfiguration;
    private readonly WireMockRecordingHandler _recordingHandler;
    private readonly ScenarioAccessor _scenarioAccessor;

    public RecordHttpCallDelegatingHandler(
        IOptionsMonitor<TestConfiguration> testConfiguration,
        WireMockRecordingHandler recordingHandler,
        ScenarioAccessor scenarioAccessor)
    {
        _testConfiguration = new(() => testConfiguration.CurrentValue);
        _recordingHandler = recordingHandler;
        _scenarioAccessor = scenarioAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var scenarioName = _scenarioAccessor.GetScenario();

        return _testConfiguration.Value.IsScenarioInRecordingMode(scenarioName)
            ? RecordRequestAndResponse(request, cancellationToken)
            : base.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> RecordRequestAndResponse(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        await _recordingHandler.SaveRecordingAsync(response, _testConfiguration.Value, cancellationToken);
        return response;
    }
}

internal class WireMockRecordingHandler
{
    private const string WildcardMatcher = "WildcardMatcher";
    private static readonly JsonSerializerSettings JsonSerialiserSettings = new()
    {
        DateParseHandling = DateParseHandling.None,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented,
    };

    private readonly ILogger<WireMockRecordingHandler> _logger;
    private readonly Lazy<TestConfiguration> _testConfiguration;

    public WireMockRecordingHandler(
        ILogger<WireMockRecordingHandler> logger,
        IOptionsMonitor<TestConfiguration> testConfiguration)
    {
        _logger = logger;
        _testConfiguration = new(() => testConfiguration.CurrentValue);
    }

    public async Task SaveRecordingAsync(
        HttpResponseMessage response,
        TestConfiguration testConfiguration,
        CancellationToken cancellationToken)
    {
        var path = GetPath(response.RequestMessage!, await GetHashAsync(response.RequestMessage!, cancellationToken));
        var content = await GetContentAsync(response, testConfiguration, cancellationToken);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, cancellationToken);
    }

    private string GetPath(HttpRequestMessage httpRequestMessage, string requestHash)
    {
        var requestUri = httpRequestMessage.RequestUri!;
        var requestUriSegments = requestUri.Segments
            .Select(s => s.Replace("/", ""))
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .ToArray();
        var scenarioName = httpRequestMessage.Headers.GetValues(Constants.ScenarioHeader).First();
        var serverName = GetServiceConfigName(requestUri);

        var method = httpRequestMessage.Method.ToString().ToLower();
        var lastNonIdPart = requestUriSegments.LastNonIdUrlPart();
        var lastIdPart = requestUriSegments.LastIdUrlPart();
        lastIdPart = lastIdPart is null
            ? string.Empty
            : $"_{lastIdPart}";
        var path = $"{_testConfiguration.Value.GetMockedDataFolder()}{Constants.DirSeparator}{scenarioName}{Constants.DirSeparator}{serverName}{Constants.DirSeparator}{method}_{lastNonIdPart}{lastIdPart}_{requestHash}.json";

        var directory = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Creating Folder[{directory}]", directory);
            Directory.CreateDirectory(directory);
        }
        return path;
    }

    private async Task<string> GetContentAsync(
        HttpResponseMessage response,
        TestConfiguration testConfiguration,
        CancellationToken cancellationToken = default)
    {
        var request = response.RequestMessage!;
        var parameters = request.RequestUri.ParseQueryString();
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var serverName = GetServiceConfigName(request.RequestUri!);
        var serverConfig = GetMockedServerConfiguration(serverName, testConfiguration);

        var requestBuilder = new RequestModelBuilder()
            .WithMethods(request.Method.ToString())
            .WithHeaders(BuildRequestHeaders(request, serverConfig, testConfiguration.DefaultConfig))
            .WithPath(request.RequestUri!.PathAndQuery);

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
            .WithHeaders(response.Headers.ToDictionary(x => x.Key, x => (object)x.Value))
            // TODO: Add support for other formats like xml
            .WithBodyAsJson(JsonConvert.DeserializeObject(responseBody, JsonSerialiserSettings))
            .WithBodyAsJsonIndented(true);

        var content = new MappingModelBuilder()
            .WithRequest(requestBuilder.Build())
            .WithResponse(responseBuilder.Build())
            .Build();

        return JsonConvert.SerializeObject(content, JsonSerialiserSettings);

        static List<HeaderModel> BuildRequestHeaders(
            HttpRequestMessage request,
            MockedServerConfiguration serverConfig,
            MockedServerConfiguration defaultConfig) => 
            request.Headers
            .Where(x => !serverConfig.IgnoredRequestHeaders.Concat(defaultConfig.IgnoredRequestHeaders).Select(x => x.ToLower()).Contains(x.Key.ToLower()))
            .Select(h => new HeaderModelBuilder().WithName(h.Key).WithMatchers(h.Value.Select(v => BuildMatcher(v)).ToList()).Build())
            .ToList();

        static async Task<BodyModel?> BuildBodyAsync(HttpContent? content, CancellationToken cancellationToken)
        {
            if (content is null) return null;

            var stringContent = await content.ReadAsStringAsync(cancellationToken);
            return stringContent is null
                ? null
                : new BodyModelBuilder()
                .WithMatcher(new MatcherModelBuilder().WithName(WildcardMatcher).WithPattern(stringContent).WithIgnoreCase(true).Build())
                .Build();
        }

        static List<ParamModel> BuildParams(System.Collections.Specialized.NameValueCollection parameters) => parameters
            .Cast<string>()
            .Select(x => new ParamModelBuilder().WithName(x).WithMatchers(BuildMatcher(parameters[x]!)).Build())
            .ToList();

        static MatcherModel BuildMatcher(string v)
            => new MatcherModelBuilder().WithName(WildcardMatcher).WithPattern(v).WithIgnoreCase(true).Build();
    }

    private static async Task<string> GetHashAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        using var md5 = MD5.Create();
        md5.Initialize();

        var values = await GetValuesToHashAsync(request, cancellationToken);
        var requestBytes = GetHashableBytes(values.ToArray());
        return GetHashableString(md5.ComputeHash(requestBytes));

        static byte[] GetHashableBytes(params string[] values)
        {
            var sb = new StringBuilder();
            foreach (var value in values)
            {
                sb.Append(value);
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        static string GetHashableString(byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (var t in bytes)
                sb.Append(t.ToString("X2"));
            return sb.ToString();
        }

        static async Task<List<string>> GetValuesToHashAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var values = new List<string>();

            var content = request.Content is null
                ? await Task.FromResult((string?)null)
                : await request.Content.ReadAsStringAsync(cancellationToken);
            if (content is not null)
            {
                values.Add(content);
            }

            var acceptHeader = request.Headers?.Accept?.FirstOrDefault()?.ToString();
            if (acceptHeader is not null)
            {
                values.Add(acceptHeader);
            }

            var ifMatch = request.Headers?.IfMatch?.FirstOrDefault()?.ToString();
            if (ifMatch is not null)
            {
                values.Add(ifMatch);
            }

            var userAgent = request.Headers?.UserAgent?.FirstOrDefault()?.ToString();
            if (userAgent is not null)
            {
                values.Add(userAgent);
            }
            return values;
        }
    }

    private static MockedServerConfiguration GetMockedServerConfiguration(
        string serverName, 
        TestConfiguration testConfiguration)
    {
        return testConfiguration.FindServiceConfiguration(serverName) 
            ?? throw new Exception("Couldn't find server configuration");
    }

    private string GetServiceConfigName(Uri requestUri) =>
        _testConfiguration.Value.FindServiceConfiguration(requestUri)?.Name
        ?? throw new Exception($"'{requestUri}' doesn't  match with an existing mocked server");
}

internal static class UrlPathExtensions
{
    private const string _guid = @"[\da-zA-Z]{8}-([\da-zA-Z]{4}-){3}[\da-zA-Z]{12}";
    private static readonly Regex GuidExact = new("^" + _guid + "$", RegexOptions.Compiled);

    public static string LastNonIdUrlPart(this string[] segments) => segments.Last(x => !x.IsGuid());

    public static string? LastIdUrlPart(this string[] segments) => segments.LastOrDefault(x => x.IsGuid());

    private static bool IsGuid(this string source) => GuidExact.IsMatch(source);
}