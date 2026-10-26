# Lernkicker

Lernkicker is a small self-hosted learning app for primary school children. Parents upload practice tests ("quizzes") and assign them to their children, the children practise on their phone, and the parents see the results. Several families can share one installation; each family only sees its own data. A parent can also add an adult profile (no grade, no school marks) to learn with their own quizzes and material. Each profile has a look (football, space or unicorn) that colours the app and the games on the child's devices. The UI is German and English.

It runs as a Docker container on a Synology NAS and is reachable through Tailscale (optionally public via Tailscale Funnel).

## Documentation

- [Plan](docs/plan.md): roles, building blocks, technology, phases
- [Architecture](docs/architecture.md): the hexagon, projects and dependency rules
- [Operations](docs/operations.md): running it on a Synology with Tailscale (and Funnel)
- [Security concept](docs/security.md): Funnel, login, family isolation, data protection, email
- [Quiz format `quiz/v1`](docs/quiz-format.md): what a quiz looks like as JSON
- [Lesson format `lesson/v1`](docs/lesson-format.md): learning material (poems, texts, flashcards, facts, guides to look things up in) as JSON
- [`schema/`](schema/): JSON Schema for quizzes and lessons, [`quizzes/`](quizzes/), [`lessons/`](lessons/): examples
- [Prompt for AI assistants](docs/ai-prompt/README.md): lets parents have an AI write a quiz or lesson (German and English); the app offers it on the upload pages
- [`tools/validate_quizzes.py`](tools/validate_quizzes.py): validates quizzes locally and in CI

## Projects

| Project | What it is |
| --- | --- |
| `src/Lernkicker.Domain` | Quizzes, exercise types, grading, families, children, devices, practice (no dependencies) |
| `src/Lernkicker.Application` | Use cases, ports and error codes |
| `src/Lernkicker.QuizFormat` | Adapter: `quiz/v1` and `lesson/v1` JSON and schema validation |
| `src/Lernkicker.Calendar` | Adapter: ICS import (only from public addresses) and feed with Ical.Net, local time with NodaTime |
| `src/Lernkicker.Telegram` | Adapter: the Telegram bot for parents (long polling) and its messages in German and English |
| `src/Lernkicker.Persistence` | Adapter: EF Core + SQLite, ASP.NET Core Identity, migrations |
| `src/Lernkicker.Contracts` | DTOs of the HTTP API, shared by server and client |
| `src/Lernkicker.Web` | Adapter: Minimal API, cookies, security headers; composition root; hosts the client |
| `src/Lernkicker.Client` | Blazor WebAssembly PWA for children and parents, localized (de, en) |
| `tests/*` | Unit tests for domain and format, integration tests for the API, family isolation, architecture and localization |

## Running locally

Needs the .NET 10 SDK.

```bash
dotnet test
dotnet run --project src/Lernkicker.Web
```

Then open http://localhost:5208. On first start the server creates the database in `data/`; the first visit shows the setup page, which creates the admin account with its own family (only from inside the tailnet, only once). Settings (`appsettings.json` or environment variables):

| Setting | Default | Meaning |
| --- | --- | --- |
| `Lernkicker:DataPath` | `data` | Folder for the SQLite database and the data protection keys |
| `Admin:Email`, `Admin:Password` | – | Optional: creates the admin at start instead of the setup page (unattended setups) |
| `Admin:Name`, `Admin:Family`, `Admin:Language` | `Admin`, `My family`, `de` | Display name, family name and language of the admin |
| `Lernkicker:Limits:LoginsPerMinute` | `10` | Parent logins and invitations per IP and minute |
| `Lernkicker:Limits:ChildLoginsPerMinute` | `20` | Pairing and PIN attempts per IP and minute |
| `Lernkicker:Limits:CalendarRequestsPerMinute` | `30` | Calendar imports and ICS feed requests per IP and minute |
| `Lernkicker:PublicUrl` | – | The address people use, e.g. `https://lernkicker.<tailnet>.ts.net`, for links in QR codes, invitations and the feed. Usually not needed: the setup page keeps the https address it was opened with. Wins over that |
| `Lernkicker:TrustedProxies` | – | Networks of reverse proxies (CIDR, comma-separated) whose `X-Forwarded-For` is trusted; loopback is always trusted |
| `Lernkicker:TimeZone` | `Europe/Berlin` | Time zone of all dates (IANA name) |
| `Lernkicker:Calendar:SyncHours` | `6` | How often subscribed calendars are imported again; `0` turns it off |
| `Lernkicker:Telegram:BotTokenFile` | – | File with the bot token from BotFather (a Docker secret). Usually not needed: the admin sets the token on `/admin`. A token from the configuration wins |
| `Lernkicker:Telegram:BotToken` | – | The token itself, for development only (user secrets) |
| `Lernkicker:Telegram:ReminderHour` | `18` | Local hour of the evening reminder with the next day's dates |

## Trying the Docker image

The image `ghcr.io/dapplo/lernkicker` (amd64 and arm64) is built by GitHub Actions on every push to `main` (`latest`, `sha-…`) and for `v*` tags. To try it on your own computer:

```bash
docker run --rm -p 8080:8080 -v lernkicker-data:/data ghcr.io/dapplo/lernkicker:latest
```

Then open http://localhost:8080 and create the admin on the setup page. Login cookies are HTTPS-only, and browsers only make an exception for `localhost`: on another machine (e.g. `http://<nas-ip>:8080`) the login does not stick. For the NAS, use the Tailscale setup in [Operations](docs/operations.md), which serves HTTPS.

## Pages

| Path | Who | What |
| --- | --- | --- |
| `/` | child | Pair the phone, PIN, list of quizzes with stars, learning material, dates of the next two weeks |
| `/pair#<code>` | child | Opened from the QR code |
| `/practice/<assignment>` | child | One exercise per screen, hand in, result with solutions |
| `/learn/<assignment>` | child | Read and practise learning material: poem (read, practise, recite), flashcards, facts, entries to look up; mark parts as known (or jump between parts in a reference lesson) |
| `/parents` | parent | Login, children, add a child or adult, connect Telegram |
| `/parents/quizzes` | parent | Upload (JSON with its pictures, a zip, or paste), preview, share and delete quizzes |
| `/parents/lessons` | parent | Upload (JSON with its pictures, a zip, or paste), preview, share and delete learning material |
| `/parents/preview/<quiz\|lesson>/<id>` | parent | Preview as the child sees it |
| `/parents/library` | parent | Shared library: what other families shared, with preview (`/parents/library/<kind>/<id>`) and copy |
| `/parents/children/<id>` | parent | Assign quizzes and learning material (with progress), dates, subscribed calendars, feed link, results, QR code for pairing, devices, settings |
| `/invitation#<code>` | new parent | Create an account from an invitation |
| `/setup` | first visitor | Fresh server only: create the admin account and family, or start from a backup (tailnet only, once) |
| `/admin` | admin | Invite families, delete families, set the Telegram bot token, download a backup (tailnet only) |
| `/privacy` | everyone | Privacy notice |

## API

All endpoints are under `/api`, take and return JSON, and answer errors with `{ "code": "...", "issues": [...] }`.

| Endpoint | Who |
| --- | --- |
| `GET /api/setup`, `POST /api/setup`, `POST /api/setup/restore` (backup zip as body; tailnet only, while no admin exists) | first visitor |
| `POST /api/account/login`, `POST /api/account/logout`, `GET /api/account/me`, `PUT /api/account/language`, `POST /api/account/invitation`, `GET/DELETE /api/account/telegram`, `POST /api/account/telegram/link` | parents |
| `POST /api/admin/invitations`, `GET /api/admin/families`, `DELETE /api/admin/families/{id}`, `GET/PUT/DELETE /api/admin/telegram` (bot token, write-only), `GET /api/admin/backup` (zip) | admin, tailnet only |
| `GET /api/quizzes`, `POST /api/quizzes` (raw JSON body, a zip, or a multipart form with the JSON and its pictures), `GET /api/quizzes/{quizId}`, `GET …/{quizId}/zip` (with pictures), `GET …/{quizId}/preview`, `PUT …/{quizId}/shared`, `DELETE /api/quizzes/{quizId}` | parents |
| `GET /api/lessons`, `POST /api/lessons` (like quizzes), `GET /api/lessons/{lessonId}`, `GET …/{lessonId}/zip`, `GET …/{lessonId}/preview`, `PUT …/{lessonId}/shared`, `DELETE /api/lessons/{lessonId}`, `GET/POST /api/children/{id}/lessons`, `DELETE …/lessons/{assignmentId}` | parents |
| `GET /api/library`, `GET /api/library/{quizzes\|lessons}/{id}` (preview), `POST /api/library/{quizzes\|lessons}/{id}/copy` | parents |
| `GET /api/media/{hash}` (a picture of a quiz or lesson the caller may see) | parents, children |
| `GET/POST /api/children`, `PUT/DELETE /api/children/{id}`, `PUT …/pin`, `POST …/pairing`, `GET …/devices`, `DELETE …/devices/{deviceId}`, `POST …/devices/{deviceId}/unlock`, `GET/POST …/assignments`, `DELETE …/assignments/{assignmentId}`, `GET …/attempts`, `GET …/attempts/{attemptId}` | parents |
| `GET/POST /api/children/{id}/calendar` (`?from=&to=`), `PUT/DELETE …/calendar/{eventId}`, `GET/POST …/subscriptions`, `POST …/subscriptions/{subscriptionId}/sync`, `DELETE …/subscriptions/{subscriptionId}`, `POST/DELETE …/feed` | parents |
| `GET /calendar/{token}.ics` | calendar apps (secret link, no login) |
| `POST /api/device/pair`, `GET /api/device/status`, `POST /api/device/login`, `POST /api/device/logout` | child's device |
| `GET /api/device/calendar`, `GET /api/device/assignments`, `GET /api/device/assignments/{id}`, `POST /api/device/assignments/{id}/submit`, `GET /api/device/lessons`, `GET /api/device/lessons/{id}`, `POST /api/device/lessons/{id}/practice` | child |

## License and support

Lernkicker is released under the [MIT License](LICENSE).

If it helps your family, you can support the development via [GitHub Sponsors](https://github.com/sponsors/lakritzator) or [Ko-fi](https://ko-fi.com/lakritzator).
