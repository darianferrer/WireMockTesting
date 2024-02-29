using Microsoft.AspNetCore.Http;

namespace TranslationService.IntegrationTests.RecordingManually.Server.DelegatingHandlers;

internal class ScenarioDelegatingHandler : DelegatingHandler
{
    private readonly ScenarioAccessor _scenarioAccessor;

    public ScenarioDelegatingHandler(ScenarioAccessor scenarioAccessor)
    {
        _scenarioAccessor = scenarioAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var scenario = _scenarioAccessor.GetScenario();
        if (scenario is not null && !request.Headers.Contains(Constants.ScenarioHeader))
        {
            request.Headers.Add(Constants.ScenarioHeader, scenario);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

internal class ScenarioAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ScenarioAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetScenario()
    {
        var context = _httpContextAccessor.HttpContext;
        return context != null
            && context.Request.Headers.TryGetValue(Constants.ScenarioHeader, out var scenario)
            && !string.IsNullOrWhiteSpace(scenario)
            ? (string?)scenario
            : null;
    }
}