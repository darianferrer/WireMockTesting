using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using TranslationService.IntegrationTests.RecordingDefault.Server;
using TranslationService.Translations;
using Xunit;

namespace TranslationService.IntegrationTests.RecordingDefault.Features.Notifications;

[Collection(nameof(CustomTestApplicationFactory))]
public class NotificationApiTests
{
    private readonly CustomTestApplicationFactory _applicationFactory;
    private readonly Fixture _fixture = new();

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    static NotificationApiTests()
    {
        _options.Converters.Add(new JsonStringEnumConverter());
    }

    public NotificationApiTests(CustomTestApplicationFactory applicationFactory)
    {
        _applicationFactory = applicationFactory;
    }

    [Fact]
    public async Task GivenNoTranslationType_WhenTextIsSubmitted_ThenItIsReturnedWithoutChange()
    {
        // Arrange
        AddScenarioHeader(nameof(GivenNoTranslationType_WhenTextIsSubmitted_ThenItIsReturnedWithoutChange));
        var client = _applicationFactory.CreateClient();
        var contract = new
        {
            Text = "Testing, 1, 2, 3 probando",
            Type = "None",
        };
        var content = GetContent(contract);

        // Act
        var result = await client.PostAsync("/translate", content);

        // Assert  
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseStream = await result.Content.ReadAsStreamAsync();
        var response = await JsonSerializer.DeserializeAsync<TranslationResponse>(
            responseStream,
            _options);
        response.Should().BeEquivalentTo(new
        {
            contract.Text,
            OriginalText = contract.Text,
            Type = TranslationType.None,
        });
    }

    [Fact]
    public async Task GivenYodaTranslationType_WhenTextIsSubmitted_ThenItIsReturnedTranslated()
    {
        // Arrange
        AddScenarioHeader(nameof(GivenYodaTranslationType_WhenTextIsSubmitted_ThenItIsReturnedTranslated));
        var client = _applicationFactory.CreateClient();
        var contract = new
        {
            Text = "Master Obiwan has lost a planet.",
            Type = "Yoda",
        };
        var content = GetContent(contract);

        // Act
        var result = await client.PostAsync("/translate", content);

        // Assert  
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        using var responseStream = await result.Content.ReadAsStreamAsync();
        var response = await JsonSerializer.DeserializeAsync<TranslationResponse>(
            responseStream,
            _options);
        response.Should().BeEquivalentTo(new
        {
            Text = "Lost a planet,  master obiwan has.",
            OriginalText = contract.Text,
            Type = TranslationType.Yoda,
        });
    }

    [Fact]
    public async Task GivenShakespeareTranslationType_WhenTextIsSubmitted_ThenItIsReturnedTranslated()
    {
        // Arrange
        AddScenarioHeader(nameof(GivenShakespeareTranslationType_WhenTextIsSubmitted_ThenItIsReturnedTranslated));
        var client = _applicationFactory.CreateClient();
        var contract = new
        {
            Text = "Master Obiwan has lost a planet.",
            Type = "Shakespeare",
        };
        var content = GetContent(contract);

        // Act
        var result = await client.PostAsync("/translate", content);

        // Assert  
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        using var responseStream = await result.Content.ReadAsStreamAsync();
        var response = await JsonSerializer.DeserializeAsync<TranslationResponse>(
            responseStream,
            _options);
        response.Should().BeEquivalentTo(new
        {
            Text = "Master obiwan hath did lose a planet.",
            OriginalText = contract.Text,
            Type = TranslationType.Shakespeare,
        });
    }

    [Theory]
    [InlineData(null, "None")]
    [InlineData("", "None")]
    [InlineData("Testing 1, 2, 3 probando", "WrongType")]
    [InlineData("Testing 1, 2, 3 probando", "Unknown")]
    public async Task GivenInvalidTranslationRequest_WhenItIsSubmitted_ThenBadRequestIsReturned(
        string? text,
        string translationType)
    {
        // Arrange
        AddScenarioHeader(nameof(GivenInvalidTranslationRequest_WhenItIsSubmitted_ThenBadRequestIsReturned));
        var client = _applicationFactory.CreateClient();
        var contract = new
        {
            Text = text,
            TranslationType = translationType,
        };
        var content = GetContent(contract);

        // Act
        var result = await client.PostAsync("/translate", content);

        // Assert  
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    // INFO: the mock associated with this test has been manually updated to simulate an upstream error
    public async Task GivenUpstreamError_WhenTextIsSubmitted_ThenErrorIsReturned()
    {
        // Arrange
        AddScenarioHeader(nameof(GivenUpstreamError_WhenTextIsSubmitted_ThenErrorIsReturned));
        var client = _applicationFactory.CreateClient();
        var contract = new
        {
            Text = "Master Obiwan has lost a planet.",
            Type = "Shakespeare",
        };
        var content = GetContent(contract);

        //_applicationFactory.FunTranslationsServer
        //    .Given(
        //        Request.Create()
        //            .UsingPost()
        //            .WithPath("/translate/shakespeare"))
        //    .RespondWith(
        //        Response.Create()
        //            .WithStatusCode(StatusCodes.Status500InternalServerError));

        // Act
        var result = await client.PostAsync("/translate", content);

        // Assert  
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        using var responseStream = await result.Content.ReadAsStreamAsync();
        var response = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            responseStream,
            _options);
        response.Should().BeEquivalentTo(new
        {
            Title = "An error occurred while processing your request.",
            Status = (int)HttpStatusCode.InternalServerError,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        });
    }

    private static StringContent GetContent<T>(T contract) =>
        new(JsonSerializer.Serialize(contract, _options), Encoding.UTF8, MediaTypeNames.Application.Json);

    private void AddScenarioHeader(string scenario) =>
        _applicationFactory.AddClientCustomisation(client =>
        {
            client.DefaultRequestHeaders.Remove(Constants.ScenarioHeader);
            client.DefaultRequestHeaders.Add(Constants.ScenarioHeader, scenario);
        });
}
