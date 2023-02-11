using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WireMock.Server;

namespace TranslationService.IntegrationTests.Server;

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
            .UseEnvironment("test");
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