using Microsoft.Extensions.Options;

namespace TranslationService.IntegrationTests.RecordingManually.Server.DelegatingHandlers;

internal class NoExternalRequestsDelegatingHandler : DelegatingHandler
{
    private readonly ScenarioAccessor _scenarioAccessor;
    private readonly Lazy<TestConfiguration> _testConfiguration;

    public NoExternalRequestsDelegatingHandler(
        ScenarioAccessor scenarioAccessor,
        IOptionsMonitor<TestConfiguration> options)
    {
        _scenarioAccessor = scenarioAccessor;
        _testConfiguration = new(() => options.CurrentValue);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var scenarioName = _scenarioAccessor.GetScenario();
        var server = _testConfiguration.Value.FindServiceConfiguration(request.RequestUri!);

        return server is not null || _testConfiguration.Value.IsScenarioInRecordingMode(scenarioName)
            ? base.SendAsync(request, cancellationToken)
            : throw new UnMockedRequestException(request.RequestUri);
    }
}

internal class UnMockedRequestException : Exception
{
    public UnMockedRequestException(Uri? requestedUrl)
        : base($"A request was made to \"{requestedUrl}\" which is not a mocked upstream service") { }
}
