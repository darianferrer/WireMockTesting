using Microsoft.Extensions.Options;

namespace TranslationService.IntegrationTests.RecordingManually.Server.DelegatingHandlers;

internal class MockProxyDelegatingHandler : DelegatingHandler
{
    private readonly ScenarioAccessor _scenarioAccessor;
    private readonly Lazy<TestConfiguration> _testConfiguration;

    public MockProxyDelegatingHandler(
        ScenarioAccessor scenarioAccessor,
        IOptionsMonitor<TestConfiguration> options)
    {
        _scenarioAccessor = scenarioAccessor;
        _testConfiguration = new(() => options.CurrentValue);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var scenarioName = _scenarioAccessor.GetScenario();

        if (!_testConfiguration.Value.IsScenarioInRecordingMode(scenarioName))
        {
            var server = _testConfiguration.Value.FindServiceConfiguration(request.RequestUri!);
            request.RequestUri = server is not null
                ? new Uri(new Uri(server.MockedUrl), request.RequestUri!.PathAndQuery)
                : throw new MockedRequestException(request.RequestUri);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

internal class MockedRequestException : Exception
{
    public MockedRequestException(Uri? requestedUrl)
        : base($"A mocked server for \"{requestedUrl}\" cannot be found") { }
}