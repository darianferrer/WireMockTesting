using Xunit;

namespace TranslationService.IntegrationTests.Server;

[CollectionDefinition(nameof(CustomTestApplicationFactory))]
public class TestApplicationFactoryCollection
    : ICollectionFixture<CustomTestApplicationFactory>
{
}