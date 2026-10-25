# Lernkicker

Lernkicker is a small self-hosted learning app for primary school children. Parents upload practice tests ("quizzes") and assign them to their children, the children practise on their phone, and the parents see the results.

## Contents

- [Plan](docs/plan.md)
- [Architecture](docs/architecture.md)
- [Security concept](docs/security.md)
- [Quiz format `quiz/v1`](docs/quiz-format.md): what a quiz looks like as JSON
- [`schema/`](schema/): JSON Schema for quizzes, [`quizzes/`](quizzes/): example quizzes
- [`tools/validate_quizzes.py`](tools/validate_quizzes.py): validates quizzes locally and in CI
- `src/Lernkicker.Domain`: quizzes, grading, families, practice (no dependencies)
- `src/Lernkicker.Application`: use cases, ports and error codes
- `src/Lernkicker.QuizFormat`: adapter for the `quiz/v1` JSON format
- `src/Lernkicker.Persistence`: adapter for EF Core + SQLite and ASP.NET Core Identity
- `src/Lernkicker.Contracts`: DTOs of the HTTP API
- `src/Lernkicker.Web`: the HTTP API (Minimal API, cookies, security headers) and composition root

Run the tests with `dotnet test`, the server with `Admin__Email=you@example.org Admin__Password='at-least-12-chars' dotnet run --project src/Lernkicker.Web`.
