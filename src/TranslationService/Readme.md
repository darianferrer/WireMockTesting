# Translation service
A translation API that supports 2 "languages", Yoda and Shakespeare, using an external service called [Fun Translations](https://funtranslations.com/).

POST `/translate` with the following contract
```
{
  "text": "string";
  "type": Unknown | None | Yoda | Shakespeare;
}
```

will return the following response:
```
{
  "text": "string";
  "originalText": string;
  "type": Unknown | None | Yoda | Shakespeare;
}
```
## Notes
* Unknown and None wont result in any translation, `text` and `originalText` would be the same
* The [Fun Translation service](https://funtranslations.com/api/) has a rate limiting setting of 60 API calls per day with a max of 5 per hour.

## How to run
First thing is to clone the repository. After that's completed:
* Download and install [.NET 5 SDK](https://dotnet.microsoft.com/download)
* From `TranslationService` run `dotnet run`
* Open a browser and navigate to https://localhost:5000/swagger to start using the API

## Test
See `Readme.md` in each test project