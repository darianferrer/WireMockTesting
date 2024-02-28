using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace TranslationService.IntegrationTests.RecordingManually.Server.DelegatingHandlers;

internal class RecordHttpCallDelegatingHandler : DelegatingHandler
{
    private readonly Lazy<TestConfiguration> _testConfiguration;
    private readonly RecordingHandler _recordingHandler;
    private readonly ScenarioAccessor _scenarioAccessor;

    public RecordHttpCallDelegatingHandler(
        IOptionsMonitor<TestConfiguration> testConfiguration,
        RecordingHandler recordingHandler,
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

internal static partial class UrlPathExtensions
{
    private const string _guid = @"[\da-zA-Z]{8}-([\da-zA-Z]{4}-){3}[\da-zA-Z]{12}";
    private static readonly Regex GuidExact = GuidRegex();

    public static string LastNonIdUrlPart(this string[] segments) => segments.Last(x => !x.IsGuid());

    public static string? LastIdUrlPart(this string[] segments) => segments.LastOrDefault(x => x.IsGuid());

    private static bool IsGuid(this string source) => GuidExact.IsMatch(source);

    [GeneratedRegex(@"^[\da-zA-Z]{8}-([\da-zA-Z]{4}-){3}[\da-zA-Z]{12}$", RegexOptions.Compiled)]
    private static partial Regex GuidRegex();
}