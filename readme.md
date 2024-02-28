# How to use WireMock

WireMock if often used to replace an external API dependency mocking its requests and responses, see its [wiki](https://github.com/WireMock-Net/WireMock.Net/wiki/What-Is-WireMock.Net) for more details nad use cases. 

In this project, we are going to focus on how to use it for [integration/functional tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-8.0), together with dotnet's WebApplicationFactory and some custom code with http delegating handlers to tackle some of the problems associated with these kind of tests.

## What's inside

This is a demo project where we could play and demo some of the capabilities that WireMock provides for creating integration tests that require mocking external APIs. Each integration test project builds on top of the previous to solve a specific issue:

### TranslationService
This is the production service, see it's [readme](src/TranslationService/Readme.md) for more info.

### TranslationService.IntegrationTests
The [simplest](tests/TranslationService.IntegrationTests/Readme.md) way to use WireMock for writing integration tests.

### TranslationService.IntegrationTests.NoLeaks
Sometimes a new dependency is added to our service and we forget to mock it for our tests, in this [project](tests/TranslationService.IntegrationTests.NoLeaks/Readme.md) we show how to avoid having http leaks in your integration tests.

### TranslationService.IntegrationTests.RecordingDefault
WireMock has a built-in feature that proxies calls to the real APIs and can also record and replay these calls, see how to do it [here](tests/TranslationService.IntegrationTests.RecordingDefault/Readme.md).

### TranslationService.IntegrationTests.RecordingManually
The default way WireMocks stores the recordings has some issues which are explained in this [project](tests/TranslationService.IntegrationTests.RecordingManually/Readme.md) and we show how to use a more opinionated manual recording strategy.