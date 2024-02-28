using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WireMock.Admin.Mappings;

namespace TranslationService.IntegrationTests.RecordingManually.Server.DelegatingHandlers;

internal class RecordingHandler
{
    private static readonly JsonSerializerSettings JsonSerialiserSettings = new()
    {
        DateParseHandling = DateParseHandling.None,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented,
    };

    private readonly WireMockMappingModelBuilder _mappingModelBuilder;
    private readonly FilePathService _filePathService;

    public RecordingHandler(
        WireMockMappingModelBuilder mappingModelBuilder,
        FilePathService filePathService)
    {
        _mappingModelBuilder = mappingModelBuilder;
        _filePathService = filePathService;
    }

    public async Task SaveRecordingAsync(
        HttpResponseMessage response,
        TestConfiguration testConfiguration,
        CancellationToken cancellationToken)
    {
        var newModel = await _mappingModelBuilder.GetFromHttpResponseAsync(response, testConfiguration, JsonSerialiserSettings, cancellationToken);
        var newModelSerialised = JsonConvert.SerializeObject(newModel, JsonSerialiserSettings);

        var originalPath = _filePathService.GetOriginalPath(
            new(response.RequestMessage!.RequestUri!, response.RequestMessage.Method.ToString()));
        var path = await _filePathService.GetVersionedPathAsync(
            new(originalPath, content =>
            {
                var existingModel = JsonConvert.DeserializeObject<MappingModel>(content, JsonSerialiserSettings);
                return JToken.DeepEquals(newModelSerialised, JToken.Parse(content));
            }),
            cancellationToken: cancellationToken);

        await _filePathService.SaveFileAsync(new(path, newModelSerialised),
            cancellationToken);
    }
}