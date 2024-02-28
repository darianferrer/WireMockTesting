using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using static TranslationService.IntegrationTests.RecordingManually.Server.Constants;

namespace TranslationService.IntegrationTests.RecordingManually.Server.DelegatingHandlers;

internal partial class FilePathService
{
    // The following characters will be stripped out by the encoded encoded URI before the file name is sanitised < > / \ ? but this is an exhaustive list of invalid characters
    private static readonly Regex _invalidChars = InvalidChars();

    private readonly ScenarioAccessor _scenarioAccessor;
    private readonly Lazy<TestConfiguration> _testConfiguration;

    public FilePathService(
        IOptionsMonitor<TestConfiguration> testConfiguration,
        ScenarioAccessor scenarioAccessor)
    {
        _scenarioAccessor = scenarioAccessor;
        _testConfiguration = new(() => testConfiguration.CurrentValue);
    }

    public string GetOriginalPath(PathRequest request)
    {
        var serviceKey = GetServiceConfigName(request.RequestUri);
        var scenarioName = _scenarioAccessor.GetScenario();

        var requestUri = request.RequestUri;
        var requestUriSegments = requestUri.Segments
            .Select(s => s.Replace("/", ""))
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .ToArray();

        var method = request.RequestMethod.ToLower();
        var lastNonIdPart = requestUriSegments.LastNonIdUrlPart();
        var lastIdPart = requestUriSegments.LastIdUrlPart();
        lastIdPart = lastIdPart is null
            ? string.Empty
            : $"_{lastIdPart}";

        var rawFileName = $"{method}_{lastNonIdPart}{lastIdPart}";
        var filePath = $"{scenarioName}{DirSeparator}{serviceKey}{DirSeparator}";

        return filePath + SanitizeFileName(rawFileName);
    }

    public async Task<string> GetVersionedPathAsync(VersionPathRequest request,
        int version = 0,
        CancellationToken cancellationToken = default)
    {
        var mocksFolder = _testConfiguration.Value.GetMockedDataFolder();
        var versionedPath = Path.Join(mocksFolder, version == 0 ? $"{request.Path}.json" : $"{request.Path}_{version}.json");
        if (File.Exists(versionedPath) && !await HasSameContent(versionedPath, request))
        {
            return await GetVersionedPathAsync(request, ++version, cancellationToken);
        }
        return versionedPath;
    }

    public async Task SaveFileAsync(NewFileRequest request, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(request.Path)!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(
            request.Path,
            request.Content,
            Encoding.UTF8,
            cancellationToken);
    }

    private static async Task<bool> HasSameContent(string versionedPath,
        VersionPathRequest request)
    {
        var existingContent = await File.ReadAllTextAsync(versionedPath);
        return request.ContentCompare(existingContent);
    }

    private string GetServiceConfigName(Uri requestUri) =>
        _testConfiguration.Value.FindServiceConfiguration(requestUri)?.Name
        ?? throw new Exception($"'{requestUri}' doesn't  match with an existing mocked server");

    private static string SanitizeFileName(string fileName) => _invalidChars.Replace(fileName, "_");

    [GeneratedRegex(@"[<>:""/\\|?*]", RegexOptions.Compiled)]
    private static partial Regex InvalidChars();

    public record PathRequest(Uri RequestUri, string RequestMethod);

    public record VersionPathRequest(string Path, Func<string, bool> ContentCompare);

    public record NewFileRequest(string Path, string Content);
}