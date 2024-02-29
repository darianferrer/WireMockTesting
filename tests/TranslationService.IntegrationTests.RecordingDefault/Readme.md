# Recording requests and responses for later use

*Note: This makes the assumption that your integration tests don't use external dependencies directly*

One problem we face when writing mocks for integration tests is that it can take quite some time to mock every dependency; imagine if you have 1 feature that needs to call 3 external APIs and you want to write several tests that cover happy and unhappy paths! Luckily, WireMock provides the ability to serve as a proxy to the real API and store its requests and responses.

## Struture

In the `Server` folder it contains a custom `WebApplicationFactory` that spins up a WireMock instance for `FunTranslation` upstream. There's also a `ICollectionFixture` that can be used by all tests to share the same `WebApplicationFactory` instance. The WAF configures the `FunTranslations` WireMock server to proxy calls that are not already mapped in local files, and store new requests/responses.

In `Server/DelegatingHandlers` we have one from the previous *no leak* [project](tests/TranslationService.IntegrationTests.NoLeaks/Readme.md); and a new one that will add a `x-scenario` header to every http request, this way it will be easy to identify which test made which request and avoid clashes.

The tests are in `Features` folder and are written in Arrange/Act/Assert style. We are testing a whole feature, starting at the entry point (API in this case) and verifying the response is what we expect. There's no need to use the WireMock API (`WireMock.Given`) as each request and response gets stored in `MockData/__admin`.
