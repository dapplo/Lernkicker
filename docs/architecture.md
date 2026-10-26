# Architecture

Lernkicker follows a hexagonal architecture (ports and adapters). The domain and the use cases in the middle know nothing about HTTP, JSON files, databases or Blazor; everything technical is an adapter around them.

```
          driving adapters                  core                    driven adapters
   ┌────────────────────────┐   ┌──────────────────────────┐   ┌──────────────────────────┐
   │ Lernkicker.Client      │   │ Lernkicker.Application   │   │ Lernkicker.QuizFormat    │
   │ (Blazor PWA)           │   │ use cases, ports,        │──▶│ IQuizFormat, ILesson-    │
   │        │ HTTP + JSON   │   │ Result / error codes     │   │ Format: JSON + Schema    │
   │        ▼               │   │            │             │   ├──────────────────────────┤
   │ Lernkicker.Web         │──▶│            ▼             │──▶│ Lernkicker.Persistence   │
   │ Minimal API, cookies,  │   │ Lernkicker.Domain        │   │ EF Core + SQLite,        │
   │ composition root       │   │ quizzes, grading,        │   │ Identity, repositories   │
   └────────────────────────┘   │ families, practice,      │   ├──────────────────────────┤
                                │ calendar                 │──▶│ Lernkicker.Calendar      │
   ┌────────────────────────┐   └──────────────────────────┘   │ Ical.Net, NodaTime: ICS  │
   │ Lernkicker.Telegram    │                ▲                  │ import/feed, local clock │
   │ bot: commands in,      │────────────────┘                  └──────────────────────────┘
   │ messages out (IMes-    │
   │ senger), reminders     │
   └────────────────────────┘
        Lernkicker.Contracts
        (DTOs shared by Web and Client)
```

## Projects

| Project | Role | May reference |
| --- | --- | --- |
| `Lernkicker.Domain` | Entities and rules: `Quiz` and its exercise types, `QuizRules`, `QuizSheet` (what a child sees, without solutions), `Grader`, `Family`, `Child`, `ChildDevice`, `StoredQuiz`, `Assignment`, `Attempt`, `Lesson` and its block types, `StoredLesson`, `LessonAssignment`, `CalendarEvent`, `CalendarSubscription`, `CalendarFeed`, `TelegramLink`, `TelegramLinkCode`, `ServerSecret`, `ImageRef` (a picture in a quiz or lesson), `StoredImage`, `DocumentImage`, `PlaySettings`, `Match` and the games for several players (`IGameRules`; `Battleship`, `ConnectFour`, `Checkers`, `MauMau`, `Chess`) | the BCL only |
| `Lernkicker.Application` | Use cases (`*UseCases`), ports (`Ports/Ports.cs`), `Result`/`AppError` and the stable `ErrorCodes` | Domain |
| `Lernkicker.QuizFormat` | Driven adapter for `IQuizFormat`, `ILessonFormat` and `ISvgSanitizer`: reads `quiz/v1` and `lesson/v1` JSON, validates against the embedded schemas, cleans uploaded SVG pictures | Application, Domain, JsonSchema.Net |
| `Lernkicker.Calendar` | Driven adapter for `ICalendarSource` (fetches ICS over HTTP, only from public addresses), `ICalendarWriter` (the ICS feed) and `ILocalClock` (today in the family's time zone) | Application, Domain, Ical.Net, NodaTime |
| `Lernkicker.Telegram` | Adapter for Telegram: drives `TelegramUseCases` with the commands parents send (long polling), implements `IMessenger` (a queue that sends results and reminders; restarts the bot when the admin changes the token) and the daily reminder; the bot's texts in `Resources/Messages*.resx` | Application, Domain, Telegram.Bot |
| `Lernkicker.Persistence` | Driven adapter for the repositories, `IUnitOfWork`, `IParentAccounts`, `IPinHasher`: EF Core, SQLite, ASP.NET Core Identity, migrations | Application, Domain |
| `Lernkicker.Contracts` | Request and response DTOs of the HTTP API | nothing |
| `Lernkicker.Web` | Driving adapter (Minimal API endpoints, cookies, rate limits, security headers, `ISecretProtector` with Data Protection) and composition root; hosts the client | everything above |
| `Lernkicker.Client` | Driving adapter for people: the Blazor WebAssembly PWA, localized UI | Contracts |

`ArchitectureTests` in `tests/Lernkicker.Web.Tests` check these references, so a wrong dependency fails the build in CI.

## Rules

- Dependencies point inwards. The domain has no package references; the application layer only knows the domain.
- A use case returns `Result`/`Result<T>`. Failures are an `AppError` with a kind (invalid, not found, unauthorized, locked, too large) and a stable code such as `pin.wrong`. The web adapter maps the kind to an HTTP status and sends `{ "code": "...", "issues": [...] }`.
- **The server never sends user-facing text** to the client. The client translates codes (`Error.<code>`, `Issue.<code>`) in `Resources/Strings*.resx`. Quiz validation issues carry named arguments (`{inText}`, `{answer}`, …) that the translation fills in. Only the schema validator's detail message (`{message}`) is English. The exception is the Telegram adapter, which has no client: use cases hand it a `Notification` record, and it picks the text in the parent's Telegram language (German by default).
- Family isolation is enforced twice: the `IFamilyScope` port feeds a global EF Core query filter, and the use cases check that ids belong to the caller's family. `FamilyIsolationTests` check that one family can neither read nor change another family's data through the API.
- Children never receive solutions before handing in: `QuizSheet` is built from the domain quiz without answers, and grading happens on the server.
- Background work without a login (the calendar sync, the ICS feed) enters a family explicitly with `IFamilyScope.Enter`, so the query filter still applies; it refuses to switch away from a logged-in family.
- Time comes from `TimeProvider`, randomness from `Random`/`RandomNumberGenerator` passed in, so the domain stays testable.

## Playing with others

Children play turn-based games with each other on their own devices: battleship, Connect Four, checkers (German rules: men capture backwards, flying kings), Mau-Mau for two to five and chess (all rules, including the draws).

- **Permissions:** a parent unlocks games per child and chooses with whom: only the own family, the own family plus picked families or children, or everyone on the server (`PlaySettings`). Two children meet only when both sides allow it (`PlaySettings.CanPlayWith`), so nobody can open another family's child to strangers. Within a family it is enough to unlock the game, which also lets a parent test a game with their own adult profile against a second profile on another device.
- **Matches** are the one entity that belongs to several families, so it has no family filter; `MatchUseCases` checks the seat for every read and move. A table opens with the host on seat 0 and as many seats as the game allows; when the last seat is taken the game starts, and for games with room for more (Mau-Mau) the host may start once `MinPlayers` sit at the table (`Match.StartEarly` closes the gaps in the seat numbers). A game ends with a winner or as a draw (`GameStep.Drawn`). The state is the game's own JSON in `Match.State`, stored after every move, so nobody has to be online at the same time. Changes to matches run one at a time (an in-memory lock; Lernkicker is one process).
- **Rules** live in the domain behind `IGameRules`: start, play a move, give up, whose move it is, and the view of one seat (no hidden ships or cards ever leave the server). Games for one player against the computer are out of scope.
- **Live updates:** the web adapter's SignalR hub (`/hubs/play`, child login only) only says "lobby changed" or "match changed" (`IPlayNotifier`); the pages then load again through the API, with the usual checks. Whoever is connected counts as online in the lobby (`IPlayPresence`).

## Adding things

- **A new game for several players:** a class implementing `IGameRules` with its move and view records in `Domain/Multiplayer`, an entry in `MultiplayerGames.All`, a view DTO and move DTOs in `Contracts/Play.cs` with their `JsonDerivedType`, the cases in `PlayMapping`, `PlayGames.All` and its icon in the client, a component in `Components/Multiplayer` and a case in `MatchPage.razor`, `Play.Game.<game>` and the game's texts in both resx files, its refusals in `GameErrors`/`ErrorCodes` with `Error.<code>` texts, and tests. A game's random choices (who starts, shuffling) come from the `Random` passed to `Start`; anything random later needs a seed kept in the state.
- **A new exercise type:** domain record + `QuizSheet` + `Grader` + schema + `docs/quiz-format.md` + `QuizJson.ExerciseTypes` + a sheet DTO in Contracts + a view in `ExerciseView.razor` + tests.
- **A new lesson block type:** domain record + schema + `docs/lesson-format.md` + `LessonJson` + `Mapping` (block DTO) + a case in `Learn.razor` + a `Learn.Type.<type>` text + tests.
- **A new place for a picture:** an `ImageRef?` on the domain record and its `Images(path)` (so uploads check it), the schema (`"image": { "$ref": "#/$defs/image" }`), both format docs, `Mapping` (an `ImageDto` via `ToDto(images)`), a `<Picture>` in the view, and the example with pictures.
- **A new error code:** a constant in `ErrorCodes` and an `Error.<code>` text in both resx files (`LocalizationTests` fails otherwise).
- **A new UI language:** add `Strings.<lang>.resx`, the language to `UiLanguage.Available`, `Languages.Supported`, `SatelliteResourceLanguages` in the client csproj and `supported` plus the loading texts in `wwwroot/js/start.js`.
- **A new bot message:** a `Notification` record in Application, a case in `MessageTexts.Format` and the text in both `Messages*.resx` (`TelegramTextsTests`).
- **A new adapter:** define the port in Application, implement it in its own project, wire it in `Lernkicker.Web/Program.cs`.
