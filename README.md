# Lernkicker

Lernkicker is a small self-hosted learning app for primary school children. Parents upload practice tests ("quizzes") and assign them to their children, the children practise on their phone, and the parents see the results. Several families can share one installation; each family only sees its own data. The UI is German and English.

It runs as a Docker container on a Synology NAS and is reachable through Tailscale (optionally public via Tailscale Funnel).

## Documentation

- [Plan](docs/plan.md): roles, building blocks, technology, phases
- [Architecture](docs/architecture.md): the hexagon, projects and dependency rules
- [Operations](docs/operations.md): running it on a Synology with Tailscale (and Funnel)
- [Security concept](docs/security.md): Funnel, login, family isolation, data protection, email
- [Quiz format `quiz/v1`](docs/quiz-format.md): what a quiz looks like as JSON
- [Lesson format `lesson/v1`](docs/lesson-format.md): learning material (poems, texts, flashcards, facts) as JSON
- [`schema/`](schema/): JSON Schema for quizzes, [`quizzes/`](quizzes/): example quizzes
- [`tools/validate_quizzes.py`](tools/validate_quizzes.py): validates quizzes locally and in CI

## Projects

| Project | What it is |
| --- | --- |
| `src/Lernkicker.Domain` | Quizzes, exercise types, grading, families, children, devices, practice (no dependencies) |
| `src/Lernkicker.Application` | Use cases, ports and error codes |
| `src/Lernkicker.QuizFormat` | Adapter: `quiz/v1` and `lesson/v1` JSON and schema validation |
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
| `Lernkicker:PublicUrl` | – | The address people use, e.g. `https://lernkicker.<tailnet>.ts.net`, for links in QR codes, invitations and the feed. Needed behind a proxy that does not send `X-Forwarded-Proto` |
| `Lernkicker:TrustedProxies` | – | Networks of reverse proxies (CIDR, comma-separated) whose `X-Forwarded-For` is trusted; loopback is always trusted |
| `Lernkicker:TimeZone` | `Europe/Berlin` | Time zone of all dates (IANA name) |
| `Lernkicker:Calendar:SyncHours` | `6` | How often subscribed calendars are imported again; `0` turns it off |

## Trying the Docker image

The image `ghcr.io/dapplo/lernkicker` (amd64 and arm64) is built by GitHub Actions on every push to `main` (`latest`, `sha-…`) and for `v*` tags. To try it on your own computer:

```bash
docker run --rm -p 8080:8080 -v lernkicker-data:/data \
  -e Admin__Email=you@example.org -e Admin__Password='at-least-12-chars' \
  ghcr.io/dapplo/lernkicker:latest
```

Then open http://localhost:8080 and log in under **I am a parent / Ich bin ein Elternteil**. Login cookies are HTTPS-only, and browsers only make an exception for `localhost`: on another machine (e.g. `http://<nas-ip>:8080`) the login does not stick. For the NAS, use the Tailscale setup in [Operations](docs/operations.md), which serves HTTPS.

## Pages

| Path | Who | What |
| --- | --- | --- |
| `/` | child | Pair the phone, PIN, list of quizzes with stars, learning material, dates of the next two weeks |
| `/pair#<code>` | child | Opened from the QR code |
| `/practice/<assignment>` | child | One exercise per screen, hand in, result with solutions |
| `/learn/<assignment>` | child | Read and practise learning material: poem (read, practise, recite), flashcards, facts; mark parts as known |
| `/parents` | parent | Login, children, add a child |
| `/parents/quizzes` | parent | Upload (file or paste) and delete quizzes |
| `/parents/lessons` | parent | Upload (file or paste) and delete learning material |
| `/parents/children/<id>` | parent | Assign quizzes and learning material (with progress), dates, subscribed calendars, feed link, results, QR code for pairing, devices, settings |
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
| `GET /api/lessons`, `POST /api/lessons` (raw JSON body), `GET /api/lessons/{lessonId}`, `DELETE /api/lessons/{lessonId}`, `GET/POST /api/children/{id}/lessons`, `DELETE …/lessons/{assignmentId}` | parents |
| `GET/POST /api/children`, `PUT/DELETE /api/children/{id}`, `PUT …/pin`, `POST …/pairing`, `GET …/devices`, `DELETE …/devices/{deviceId}`, `POST …/devices/{deviceId}/unlock`, `GET/POST …/assignments`, `DELETE …/assignments/{assignmentId}`, `GET …/attempts`, `GET …/attempts/{attemptId}` | parents |
| `GET/POST /api/children/{id}/calendar` (`?from=&to=`), `PUT/DELETE …/calendar/{eventId}`, `GET/POST …/subscriptions`, `POST …/subscriptions/{subscriptionId}/sync`, `DELETE …/subscriptions/{subscriptionId}`, `POST/DELETE …/feed` | parents |
| `GET /calendar/{token}.ics` | calendar apps (secret link, no login) |
| `POST /api/device/pair`, `GET /api/device/status`, `POST /api/device/login`, `POST /api/device/logout` | child's device |
| `GET /api/device/calendar`, `GET /api/device/assignments`, `GET /api/device/assignments/{id}`, `POST /api/device/assignments/{id}/submit`, `GET /api/device/lessons`, `GET /api/device/lessons/{id}`, `POST /api/device/lessons/{id}/practice` | child |

## License and support

Lernkicker is released under the [MIT License](LICENSE).

If it helps your family, you can support the development via [GitHub Sponsors](https://github.com/sponsors/lakritzator) or [Ko-fi](https://ko-fi.com/lakritzator).
