# WireMock basics

This project demonstrates a basic use of WireMock and its API when creating integration tests. 

## Struture

In the `Server` folder it contains a custom `WebApplicationFactory` that spins up a WireMock instance for `FunTranslation` upstream. There's also a `ICollectionFixture` that can be used by all tests to share the same `WebApplicationFactory` instance.

The tests are in `Features` folder and are written in Arrange/Act/Assert style. We are testing a whole feature, starting at the entry point (API in this case) and verifying the response is what we expect. WireMock API (`WireMock.Given`) is used to mock the external service