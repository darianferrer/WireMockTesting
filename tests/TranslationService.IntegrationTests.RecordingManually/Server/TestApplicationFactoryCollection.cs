using Xunit;

namespace TranslationService.IntegrationTests.RecordingManually.Server;

[CollectionDefinition(nameof(CustomTestApplicationFactory))]
public class TestApplicationFactoryCollection
    : ICollectionFixture<CustomTestApplicationFactory>
{
}