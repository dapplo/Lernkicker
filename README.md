# Lernkicker

Lernkicker is a small self-hosted learning app for primary school children. Parents upload practice tests ("quizzes") and assign them to their children, the children practise on their phone, and the parents see the results. Several families can share one installation; each family only sees its own data. The UI is German and English.

It runs as a Docker container on a Synology NAS and is reachable through Tailscale (optionally public via Tailscale Funnel).

## Documentation

- [Plan](docs/plan.md): roles, building blocks, technology, phases
- [Architecture](docs/architecture.md): the hexagon, projects and dependency rules
- [Operations](docs/operations.md): running it on a Synology with Tailscale (and Funnel)
- [Security concept](docs/security.md): Funnel, login, family isolation, data protection, email
- [Quiz format `quiz/v1`](docs/quiz-format.md): what a quiz looks like as JSON
- [`schema/`](schema/): JSON Schema for quizzes, [`quizzes/`](quizzes/): example quizzes
- [`tools/validate_quizzes.py`](tools/validate_quizzes.py): validates quizzes locally and in CI

## Projects

| Project | What it is |
| --- | --- |
| `src/Lernkicker.Domain` | Quizzes, exercise types, grading, families, children, devices, practice (no dependencies) |
| `src/Lernkicker.Application` | Use cases, ports and error codes |
| `src/Lernkicker.QuizFormat` | Adapter: `quiz/v1` JSON and schema validation |
| `src/Lernkicker.Calendar` | Adapter: ICS import (only from public addresses) and feed with Ical.Net, local time with NodaTime |
| `src/Lernkicker.Persistence` | Adapter: EF Core + SQLite, ASP.NET Core Identity, migrations |
| `src/Lernkicker.Contracts` | DTOs of the HTTP API, shared by server and client |
| `src/Lernkicker.Web` | Adapter: Minimal API, cookies, security headers; composition root; hosts the client |
| `src/Lernkicker.Client` | Blazor WebAssembly PWA for children and parents, localized (de, en) |
| `tests/*` | Unit tests for domain and format, integration tests for the API, family isolation, architecture and localization |

## Running locally

Needs the .NET 10 SDK.

```bash
dotnet test
Admin__Email=you@example.org Admin__Password='at-least-12-chars' dotnet run --project src/Lernkicker.Web
```

Then open http://localhost:5208. On first start the server creates the database in `data/` and the admin account (with its own family). Settings (`appsettings.json` or environment variables):

| Setting | Default | Meaning |
| --- | --- | --- |
| `Lernkicker:DataPath` | `data` | Folder for the SQLite database and the data protection keys |
| `Admin:Email`, `Admin:Password` | – | Admin account, created on first start |
| `Admin:Name`, `Admin:Family`, `Admin:Language` | `Admin`, `My family`, `de` | Display name, family name and language of the admin |
| `Lernkicker:Limits:LoginsPerMinute` | `10` | Parent logins and invitations per IP and minute |
| `Lernkicker:Limits:ChildLoginsPerMinute` | `20` | Pairing and PIN attempts per IP and minute |
| `Lernkicker:Limits:CalendarRequestsPerMinute` | `30` | Calendar imports and ICS feed requests per IP and minute |
| `Lernkicker:TimeZone` | `Europe/Berlin` | Time zone of all dates (IANA name) |
| `Lernkicker:Calendar:SyncHours` | `6` | How often subscribed calendars are imported again; `0` turns it off |

## Pages

| Path | Who | What |
| --- | --- | --- |
| `/` | child | Pair the phone, PIN, list of quizzes with stars, dates of the next two weeks |
| `/pair#<code>` | child | Opened from the QR code |
| `/practice/<assignment>` | child | One exercise per screen, hand in, result with solutions |
| `/parents` | parent | Login, children, add a child |
| `/parents/quizzes` | parent | Upload (file or paste) and delete quizzes |
| `/parents/children/<id>` | parent | Assign quizzes, dates, subscribed calendars, feed link, results, QR code for pairing, devices, settings |
| `/invitation#<code>` | new parent | Create an account from an invitation |
| `/admin` | admin | Invite families, delete families (tailnet only) |
| `/privacy` | everyone | Privacy notice |

## API

All endpoints are under `/api`, take and return JSON, and answer errors with `{ "code": "...", "issues": [...] }`.

| Endpoint | Who |
| --- | --- |
| `POST /api/account/login`, `POST /api/account/logout`, `GET /api/account/me`, `PUT /api/account/language`, `POST /api/account/invitation` | parents |
| `POST /api/admin/invitations`, `GET /api/admin/families`, `DELETE /api/admin/families/{id}` | admin, tailnet only |
| `GET /api/quizzes`, `POST /api/quizzes` (raw JSON body), `GET /api/quizzes/{quizId}`, `DELETE /api/quizzes/{quizId}` | parents |
| `GET/POST /api/children`, `PUT/DELETE /api/children/{id}`, `PUT …/pin`, `POST …/pairing`, `GET …/devices`, `DELETE …/devices/{deviceId}`, `POST …/devices/{deviceId}/unlock`, `GET/POST …/assignments`, `DELETE …/assignments/{assignmentId}`, `GET …/attempts`, `GET …/attempts/{attemptId}` | parents |
| `GET/POST /api/children/{id}/calendar` (`?from=&to=`), `PUT/DELETE …/calendar/{eventId}`, `GET/POST …/subscriptions`, `POST …/subscriptions/{subscriptionId}/sync`, `DELETE …/subscriptions/{subscriptionId}`, `POST/DELETE …/feed` | parents |
| `GET /calendar/{token}.ics` | calendar apps (secret link, no login) |
| `POST /api/device/pair`, `GET /api/device/status`, `POST /api/device/login`, `POST /api/device/logout` | child's device |
| `GET /api/device/calendar`, `GET /api/device/assignments`, `GET /api/device/assignments/{id}`, `POST /api/device/assignments/{id}/submit` | child |
