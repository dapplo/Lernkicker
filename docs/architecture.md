# Architecture

Lernkicker follows a hexagonal architecture (ports and adapters). The domain and the use cases in the middle know nothing about HTTP, JSON files, databases or Blazor; everything technical is an adapter around them.

```
          driving adapters                  core                    driven adapters
   ┌────────────────────────┐   ┌──────────────────────────┐   ┌──────────────────────────┐
   │ Lernkicker.Client      │   │ Lernkicker.Application   │   │ Lernkicker.QuizFormat    │
   │ (Blazor PWA)           │   │ use cases, ports,        │──▶│ IQuizFormat: quiz/v1     │
   │        │ HTTP + JSON   │   │ Result / error codes     │   │ JSON + JSON Schema       │
   │        ▼               │   │            │             │   ├──────────────────────────┤
   │ Lernkicker.Web         │──▶│            ▼             │──▶│ Lernkicker.Persistence   │
   │ Minimal API, cookies,  │   │ Lernkicker.Domain        │   │ EF Core + SQLite,        │
   │ composition root       │   │ quizzes, grading,        │   │ Identity, repositories   │
   └────────────────────────┘   │ families, practice,      │   ├──────────────────────────┤
                                │ calendar                 │──▶│ Lernkicker.Calendar      │
                                └──────────────────────────┘   │ Ical.Net, NodaTime: ICS  │
                                                               │ import/feed, local clock │
                                                               └──────────────────────────┘
        Lernkicker.Contracts
        (DTOs shared by Web and Client)
```

## Projects

| Project | Role | May reference |
| --- | --- | --- |
| `Lernkicker.Domain` | Entities and rules: `Quiz` and its exercise types, `QuizRules`, `QuizSheet` (what a child sees, without solutions), `Grader`, `Family`, `Child`, `ChildDevice`, `StoredQuiz`, `Assignment`, `Attempt`, `CalendarEvent`, `CalendarSubscription`, `CalendarFeed` | the BCL only |
| `Lernkicker.Application` | Use cases (`*UseCases`), ports (`Ports/Ports.cs`), `Result`/`AppError` and the stable `ErrorCodes` | Domain |
| `Lernkicker.QuizFormat` | Driven adapter for `IQuizFormat`: reads and writes `quiz/v1` JSON, validates against the embedded schema | Application, Domain, JsonSchema.Net |
| `Lernkicker.Calendar` | Driven adapter for `ICalendarSource` (fetches ICS over HTTP, only from public addresses), `ICalendarWriter` (the ICS feed) and `ILocalClock` (today in the family's time zone) | Application, Domain, Ical.Net, NodaTime |
| `Lernkicker.Persistence` | Driven adapter for the repositories, `IUnitOfWork`, `IParentAccounts`, `IPinHasher`: EF Core, SQLite, ASP.NET Core Identity, migrations | Application, Domain |
| `Lernkicker.Contracts` | Request and response DTOs of the HTTP API | nothing |
| `Lernkicker.Web` | Driving adapter (Minimal API endpoints, cookies, rate limits, security headers) and composition root; hosts the client | everything above |
| `Lernkicker.Client` | Driving adapter for people: the Blazor WebAssembly PWA, localized UI | Contracts |

`ArchitectureTests` in `tests/Lernkicker.Web.Tests` check these references, so a wrong dependency fails the build in CI.

## Rules

- Dependencies point inwards. The domain has no package references; the application layer only knows the domain.
- A use case returns `Result`/`Result<T>`. Failures are an `AppError` with a kind (invalid, not found, unauthorized, locked, too large) and a stable code such as `pin.wrong`. The web adapter maps the kind to an HTTP status and sends `{ "code": "...", "issues": [...] }`.
- **The server never sends user-facing text.** The client translates codes (`Error.<code>`, `Issue.<code>`) in `Resources/Strings*.resx`. Quiz validation issues carry named arguments (`{inText}`, `{answer}`, …) that the translation fills in. Only the schema validator's detail message (`{message}`) is English.
- Family isolation is enforced twice: the `IFamilyScope` port feeds a global EF Core query filter, and the use cases check that ids belong to the caller's family. `FamilyIsolationTests` check that one family can neither read nor change another family's data through the API.
- Children never receive solutions before handing in: `QuizSheet` is built from the domain quiz without answers, and grading happens on the server.
- Background work without a login (the calendar sync, the ICS feed) enters a family explicitly with `IFamilyScope.Enter`, so the query filter still applies; it refuses to switch away from a logged-in family.
- Time comes from `TimeProvider`, randomness from `Random`/`RandomNumberGenerator` passed in, so the domain stays testable.

## Adding things

- **A new exercise type:** domain record + `QuizSheet` + `Grader` + schema + `docs/quiz-format.md` + `QuizJson.ExerciseTypes` + a sheet DTO in Contracts + a view in `ExerciseView.razor` + tests.
- **A new error code:** a constant in `ErrorCodes` and an `Error.<code>` text in both resx files (`LocalizationTests` fails otherwise).
- **A new UI language:** add `Strings.<lang>.resx`, the language to `UiLanguage.Available`, `Languages.Supported`, `SatelliteResourceLanguages` in the client csproj and `supported` plus the loading texts in `wwwroot/js/start.js`.
- **A new adapter** (e.g. Telegram): define the port in Application, implement it in its own project, wire it in `Lernkicker.Web/Program.cs`.
