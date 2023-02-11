using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using TranslationService.IntegrationTests.RecordingManually.Server.DelegatingHandlers;
using WireMock.Server;
using WireMock.Settings;

namespace TranslationService.IntegrationTests.RecordingManually.Server;

public class CustomTestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IConfigurationRoot _configuration;
    private readonly List<Action<HttpClient>> ClientCustomisation = new();
    private readonly List<MockedServerConfiguration> MockedServerConfigurations = new();

    public WireMockServer FunTranslationsServer { get; }

    public CustomTestApplicationFactory()
    {
        var settings = new WireMockServerSettings
        {
            WatchStaticMappingsInSubdirectories = true,
        };
        FunTranslationsServer = SetupMockServer(Constants.FunTranslations, settings);
        MockedServerConfigurations.Add(new()
        {
            MockedUrl = FunTranslationsServer.Url!,
            RealUrl = "https://api.funtranslations.com",
            Name = Constants.FunTranslations,
        });

        _configuration = LoadConfiguration();
    }

    public void AddClientCustomisation(Action<HttpClient> action)
    {
        ClientCustomisation.Add(action);
    }

    public void SetScenarioRecordingMode(string scenarioName, bool recordingMode)
    {
        var testConfiguration = Services.GetRequiredService<IOptionsMonitor<TestConfiguration>>()
            .CurrentValue;
        testConfiguration.SetScenarioRecordingMode(scenarioName, recordingMode);
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
                services.AddOptions<TestConfiguration>()
                    .Bind(_configuration.GetSection(TestConfiguration.Position))
                    .PostConfigure(opt =>
                    {
                        foreach (var config in MockedServerConfigurations)
                        {
                            var existingConfig = opt.FindServiceConfiguration(config.Name);
                            if (existingConfig is not null)
                            {
                                existingConfig.RealUrl = config.RealUrl;
                                existingConfig.MockedUrl = config.MockedUrl;
                            }
                            else
                            {
                                opt.ServiceConfigurations.Add(config);
                            }
                        }
                    });

                services.AddTransient<ScenarioAccessor>();
                services.AddTransient<WireMockRecordingHandler>();
                services.AddTransient<ScenarioDelegatingHandler>();
                services.AddTransient<NoExternalRequestsDelegatingHandler>();
                services.AddTransient<RecordHttpCallDelegatingHandler>();
                services.AddTransient<MockProxyDelegatingHandler>();

                services.ConfigureAll<HttpClientFactoryOptions>(options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(opt =>
                    {
                        opt.AdditionalHandlers.Add(opt.Services.GetRequiredService<RecordHttpCallDelegatingHandler>());
                        opt.AdditionalHandlers.Add(opt.Services.GetRequiredService<MockProxyDelegatingHandler>());
                        opt.AdditionalHandlers.Add(opt.Services.GetRequiredService<NoExternalRequestsDelegatingHandler>());
                        opt.AdditionalHandlers.Add(opt.Services.GetRequiredService<ScenarioDelegatingHandler>());
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

    private static IConfigurationRoot LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .AddJsonFile("appsettings.test.json")
            .Build();
    }

    private static WireMockServer SetupMockServer(
        string serverName,
        WireMockServerSettings settings)
    {
        var server = WireMockServer.Start(settings);
        var serverMappingFolders = Directory.GetDirectories(
            new TestConfiguration().GetMockedDataFolder(),
            serverName,
            SearchOption.AllDirectories);
        foreach (var scenarioFolder in serverMappingFolders)
        {
            server.ReadStaticMappings(scenarioFolder);
        }
        return server;
    }
}

internal static class Constants
{
    public const string ScenarioHeader = "x-scenario";
    public static readonly string DirSeparator = Path.DirectorySeparatorChar.ToString();

    public const string FunTranslations = "FunTranslations";
}

public class TestConfiguration
{
    /// <summary>
    ///     Config section name, defaults to "TestConfiguration"
    /// </summary>
    public const string Position = "TestConfiguration";

    public string MockedDataFolder { get; set; } = "MockData";

    public string GetMockedDataFolder() => @$"..{Constants.DirSeparator}..{Constants.DirSeparator}..{Constants.DirSeparator}{MockedDataFolder}";

    /// <summary>
    ///  Default WireMock server configuration. By default, "traceparent" and "authorization" headers are excluded from recordings.
    /// </summary>
    public MockedServerConfiguration DefaultConfig { get; set; } = new MockedServerConfiguration
    {
        Name = "Default",
        IgnoredRequestHeaders = new[] { "traceparent", "authorization" }
    };

    /// <summary>
    ///     WireMock server configuration for each service.
    /// </summary>
    public List<MockedServerConfiguration> ServiceConfigurations { get; set; }
        = new List<MockedServerConfiguration>();

    /// <summary>
    ///     Tracks which scenarios are using recording mode. Key: scenario name; Value: true for recording mode
    /// </summary>
    private IDictionary<string, bool> UseRealServers { get; set; }
        = new Dictionary<string, bool>();

    internal bool IsScenarioInRecordingMode(string? scenarioName)
    {
        return scenarioName is not null && UseRealServers.ContainsKey(scenarioName) && UseRealServers[scenarioName];
    }

    internal void SetScenarioRecordingMode(string scenarioName, bool useRealServer)
    {
        if (UseRealServers.ContainsKey(scenarioName))
        {
            UseRealServers[scenarioName] = useRealServer;
        }
        else
        {
            UseRealServers.Add(scenarioName, useRealServer);
        }
    }

    internal MockedServerConfiguration? FindServiceConfiguration(Uri url)
    {
        var host = url.GetLeftPart(UriPartial.Authority);
        return ServiceConfigurations.FirstOrDefault(x => x.MockedUrl == host || x.RealUrl == host);
    }

    internal MockedServerConfiguration? FindServiceConfiguration(string name)
    {
        return ServiceConfigurations.FirstOrDefault(x => x.Name == name);
    }
}

public class MockedServerConfiguration
{
    internal string MockedUrl { get; set; } = default!;

    internal string RealUrl { get; set; } = default!;

    /// <summary>
    ///     Should match one of the values in TMockServiceEnum of the ITestApplicationHelper
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    ///     Headers to be ignored when recording requests
    /// </summary>
    public string[] IgnoredRequestHeaders { get; set; } = Array.Empty<string>();
}
