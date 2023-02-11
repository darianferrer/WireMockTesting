using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using TranslationService.IntegrationTests.NoLeaks.Server.DelegatingHandlers;
using TranslationService.Translations;
using WireMock.Server;

namespace TranslationService.IntegrationTests.NoLeaks.Server;

public class CustomTestApplicationFactory : WebApplicationFactory<Program>
{
    private IConfigurationRoot _configuration;

    public WireMockServer FunTranslationsServer { get; }

    public CustomTestApplicationFactory()
    {
        FunTranslationsServer = WireMockServer.Start();
        LoadConfiguration(FunTranslationsServer.Url!);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder
            .ConfigureAppConfiguration(c => c.AddConfiguration(_configuration))
            .UseEnvironment("test")
            .ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IValidator<TranslationRequest>, TranslationRequestValidator>());
                services.AddOptions<WireMockServers>()
                    .Configure(opt => opt.MockedUrls = new() { FunTranslationsServer.Url! });

                services.AddTransient<NoExternalRequestsDelegatingHandler>();

                services.ConfigureAll<HttpClientFactoryOptions>(options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(opt =>
                    {
                        opt.AdditionalHandlers.Add(opt.Services.GetRequiredService<NoExternalRequestsDelegatingHandler>());
                    });
                });
            });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            FunTranslationsServer.Dispose();
        }
    }

    [MemberNotNull(nameof(_configuration))]
    private void LoadConfiguration(string url)
    {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .AddJsonFile("appsettings.test.json")
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "AppSettings:FunTranslations", url },
            })
            .Build();
    }
}

internal record WireMockServers
{
    public HashSet<string> MockedUrls { get; set; }

    public bool IsUrlMocked(Uri url)
    {
        var host = url.GetLeftPart(UriPartial.Authority);
        return MockedUrls is not null && MockedUrls.Contains(host);
    }
}