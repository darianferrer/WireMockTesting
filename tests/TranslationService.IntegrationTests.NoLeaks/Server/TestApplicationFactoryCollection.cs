using Xunit;

namespace TranslationService.IntegrationTests.NoLeaks.Server;

[CollectionDefinition(nameof(CustomTestApplicationFactory))]
public class TestApplicationFactoryCollection
    : ICollectionFixture<CustomTestApplicationFactory>
{
}