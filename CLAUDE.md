# Lernkicker

Self-hosted learning app for primary school kids (see `docs/plan.md`). Runs as a Docker image on a Synology NAS behind Tailscale.

- All code, comments, scripts, CI, docs and the quiz format (JSON keys, enum values) are in English.
- The UI is localized: German (default) and English. Every text a child or parent sees comes from `src/Lernkicker.Client/Resources/Strings.resx` (en) and `Strings.de.resx` (de); add each new key to both. The server only returns error codes (`ErrorCodes`, `QuizIssueCodes`); the client translates them (`Error.<code>`, `Issue.<code>`). `LocalizationTests` enforce this.
- Quiz *content* is in the quiz's `language` (German for Calvin's quizzes).
- Hexagonal architecture (`docs/architecture.md`): Domain depends on nothing, Application only on Domain, adapters (QuizFormat, Persistence, Web, Client) depend inwards and never on each other; Web is the composition root. `ArchitectureTests` enforce the references.
- Quizzes use the `quiz/v1` format: spec in `docs/quiz-format.md`, schema in `schema/quiz-v1.schema.json`, examples in `quizzes/`. Keep all three (and `QuizJson`, the domain types) in sync when the format changes.
- Validate quizzes with `python3 tools/validate_quizzes.py` (needs `pip install jsonschema`); CI runs the same check. Run `dotnet test` for everything else.
- New quizzes for Bavarian 4th grade follow LehrplanPLUS Grundschule (subject `science` = HSU); mirror the order and points of the real Probe.
- Stack: .NET 10, ASP.NET Core Minimal APIs, EF Core + SQLite, Blazor WebAssembly PWA, Telegram.Bot, Ical.Net.
