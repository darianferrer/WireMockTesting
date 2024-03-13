using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Http.Json;
using TranslationService;
using TranslationService.FunTranslations;
using TranslationService.Settings;
using TranslationService.Translations;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<AppSettings>()
    .BindConfiguration(AppSettings.Position)
    .ValidateDataAnnotations();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SchemaFilter<EnumSchemaFilter>());

builder.Services.AddTransient<IValidator<TranslationRequest>, TranslationRequestValidator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<FunTranslationServiceClient>();

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/translate", async (
    TranslationRequest toTranslate,
    FunTranslationServiceClient client,
    IValidator<TranslationRequest> validator,
    CancellationToken token) =>
{
    var validation = validator.Validate(toTranslate);
    if (!validation.IsValid)
    {
        return Results.ValidationProblem(validation.GetValidationProblems());
    }
    var (text, translationType) = toTranslate;

    var translatedText = translationType switch
    {
        TranslationType.None => text,
        TranslationType.Yoda => await client.YodaTranslateAsync(text, token),
        TranslationType.Shakespeare => await client.ShakespeareTranslateAsync(text, token),
        _ => null,
    };
    if (translatedText is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            {  nameof(TranslationRequest.Type), ["Unsuported translation type"] },
        });
    }

    return Results.Ok(new TranslationResponse(translatedText, text, translationType));
})
.ProducesValidationProblem()
.Produces<TranslationResponse>();

app.UseExceptionHandler(exceptionHandlerApp
    => exceptionHandlerApp.Run(async context
        => await Results.Problem()
                     .ExecuteAsync(context)));

app.Run();

public partial class Program { }
