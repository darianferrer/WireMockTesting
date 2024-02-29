# No Leaks in your tests

*Note: This makes the assumption that your integration tests don't use external dependencies directly*

When working on microservices that have external dependencies, we might forget to mock one of them when writing a new integration test. This can cause problems because when executing the tests, that API could be down or its resource no longer available, and this will make your tests brittle. In this project it might not seem relevant, but in a "macroservice" with a dozens of API dependencies, a test setup could choose what servers to use (and start them if they are not on) so this issue becomes more apparent in this case.

This project demonstrates how to solve that problem by adding a delegating handler to all our http clients that will throw an exception if it's base address is not from a WireMock server.

## Struture

In the `Server` folder it contains a custom `WebApplicationFactory` that spins up a WireMock instance for `FunTranslation` upstream. There's also a `ICollectionFixture` that can be used by all tests to share the same `WebApplicationFactory` instance. It also has a subfolder where there's a delegating handler that's registered in the WAF for all clients.

The tests are in `Features` folder and are written in Arrange/Act/Assert style. We are testing a whole feature, starting at the entry point (API in this case) and verifying the response is what we expect. WireMock API (`WireMock.Given`) is used to mock the external service.
