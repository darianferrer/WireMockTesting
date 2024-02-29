# Recording requests and responses for later use

*Note: This makes the assumption that your integration tests don't use external dependencies directly*

WireMock's proxying and mappings feature is easy to set up and use, but one problem is that it stores every mapping in the same folder and there's no clear way to identify which files are used in a test. This project uses WireMock Mappings APIs and http delegating handlers to manually save the files grouped by test.

## Struture

In the `Server` folder it contains a custom `WebApplicationFactory` that spins up a WireMock instance for `FunTranslation` upstream. There's also a `ICollectionFixture` that can be used by all tests to share the same `WebApplicationFactory` instance. 

In `Server/DelegatingHandlers` we have one from the previous *no leak* [project](tests/TranslationService.IntegrationTests.NoLeaks/Readme.md); and a new one that will add a `x-scenario` header to every http request, this way it will be easy to identify which test made which request and avoid clashes.

The tests are in `Features` folder and are written in Arrange/Act/Assert style. We are testing a whole feature, starting at the entry point (API in this case) and verifying the response is what we expect. There's no need to use the WireMock API (`WireMock.Given`) as each request and response gets stored in `MockData/{Test}/{ApiDependency}/{http_verb}_{url_last_non_id_part}.json`.
