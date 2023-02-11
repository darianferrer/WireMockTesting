using Xunit;

namespace TranslationService.IntegrationTests.RecordingDefault.Server;

[CollectionDefinition(nameof(CustomTestApplicationFactory))]
public class TestApplicationFactoryCollection
    : ICollectionFixture<CustomTestApplicationFactory>
{
}