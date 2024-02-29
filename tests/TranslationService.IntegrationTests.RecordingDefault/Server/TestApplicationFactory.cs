using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using TranslationService.IntegrationTests.RecordingDefault.Server.DelegatingHandlers;
using WireMock.Handlers;
using WireMock.Server;
using WireMock.Settings;

namespace TranslationService.IntegrationTests.RecordingDefault.Server;

public class CustomTestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IConfigurationRoot _configuration;
    private const string FunTranslationsPosition = "AppSettings:FunTranslations";
    private readonly List<Action<HttpClient>> ClientCustomisation = [];

    public WireMockServer FunTranslationsServer { get; }

    public CustomTestApplicationFactory()
    {
        var originalConfig = LoadConfiguration();

        var settings = new WireMockServerSettings
        {
            ReadStaticMappings = true,
            ProxyAndRecordSettings = new ProxyAndRecordSettings
            {
                Url = originalConfig.GetValue<string>(FunTranslationsPosition)!,
                SaveMapping = true,
                SaveMappingToFile = true,
                ExcludedHeaders = ["traceparent", "Host"],
                AppendGuidToSavedMappingFile = true,
            },
            FileSystemHandler = CreateFileHandler(originalConfig),
        };
        FunTranslationsServer = WireMockServer.Start(settings);
        _configuration = LoadConfiguration((FunTranslationsPosition, FunTranslationsServer.Url!));
    }

    public void AddClientCustomisation(Action<HttpClient> action)
    {
        ClientCustomisation.Add(action);
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        foreach (var action in ClientCustomisation)
        {
            action(client);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder
            .ConfigureAppConfiguration(c => c.AddConfiguration(_configuration))
            .UseEnvironment("test")
            .ConfigureTestServices(services =>
            {
                services.AddOptions<WireMockServers>()
                    .Configure(opt => opt.MockedUrls = [FunTranslationsServer.Url!]);

                services.AddTransient<ScenarioAccessor>();
                services.AddTransient<ScenarioDelegatingHandler>();
                services.AddTransient<NoExternalRequestsDelegatingHandler>();

                services.ConfigureAll<HttpClientFactoryOptions>(options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(opt =>
                    {
                        opt.AdditionalHandlers.Add(opt.Services.GetRequiredService<ScenarioDelegatingHandler>());
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

    private static IConfigurationRoot LoadConfiguration(params (string ServiceConfig, string? MockedUrl)[] mockedServers)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .AddJsonFile("appsettings.test.json");
        return (mockedServers.Length == 0
            ? builder
            : builder.AddInMemoryCollection(mockedServers.ToDictionary(x => x.ServiceConfig, x => x.MockedUrl)))
            .Build();
    }

    private static LocalFileSystemHandler CreateFileHandler(IConfigurationRoot originalConfig)
    {
        var mocksPath = Path.Combine(
            "..", // net6.0
            "..", // Debug
            "..", // bin
            originalConfig["TestConfiguration:MocksPath"]!);
        return new(mocksPath);
    }
}

internal record WireMockServers
{
    public HashSet<string> MockedUrls { get; set; } = [];

    public bool IsUrlMocked(Uri url)
    {
        var host = url.GetLeftPart(UriPartial.Authority);
        return MockedUrls is not null && MockedUrls.Contains(host);
    }
}

internal static class Constants
{
    public const string ScenarioHeader = "x-scenario";
}