using Microsoft.Extensions.Options;

namespace TranslationService.IntegrationTests.NoLeaks.Server.DelegatingHandlers;

internal class NoExternalRequestsDelegatingHandler : DelegatingHandler
{
    private readonly WireMockServers _wireMockServers;

    public NoExternalRequestsDelegatingHandler(IOptions<WireMockServers> wireMockServers)
    {
        _wireMockServers = wireMockServers.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _wireMockServers.IsUrlMocked(request.RequestUri!)
            ? base.SendAsync(request, cancellationToken)
            : throw new UnMockedRequestException(request.RequestUri);
    }
}

internal class UnMockedRequestException : Exception
{
    public UnMockedRequestException(Uri? requestedUrl)
        : base($"A request was made to \"{requestedUrl}\" which is not a mocked upstream service") { }
}