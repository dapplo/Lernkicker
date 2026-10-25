# Lernkicker – Plan

As of 2026-10-25. A longer version with diagrams is the Claude doc "Lern-App: Umsetzungsplan".

## Goal

Children practise tests on their phone; parents pick the tests and see results and dates. Several families (e.g. classmates) share one installation, and each family only sees its own data.

## Roles

| Role | Login | May |
| --- | --- | --- |
| Admin | Email + password (2FA planned) | Invite and delete families, run the server; sees no results of other families' children |
| Parent | Invitation link, then email + password | Add children, upload, delete and assign quizzes, see results, manage dates, enable Telegram |
| Child | QR code from a parent, then a 4-digit PIN | Practise assigned quizzes and learning material, see stars and dates |

## Building blocks

- **Quizzes:** JSON in the [`quiz/v1`](quiz-format.md) format. Upload (file, paste, later an API token), assign to children (optionally with a test date), delete (archived when there are results). Later: share with a class group; results stay private.
- **Learning material:** JSON in the [`lesson/v1`](lesson-format.md) format: texts, poems to learn by heart (read, practise with words hidden, recite line by line), flashcards, key facts. Parents give it to a child with an optional due date and see how often it was practised and which parts the child knows.
- **Child app:** PWA with an icon on the home screen, short rounds, stars; wrong answers come back.
- **Languages:** the UI is German and English (German by default); quiz content is in the quiz's own `language`. The server only sends codes, the client translates them. More UI languages are a new `Strings.<lang>.resx`.
- **Telegram:** one bot, only for linked accounts (one-time code via `t.me/<bot>?start=…`). Parents: results, reminders, `/send`. Children: only when enabled, questions with answer buttons. Long polling, no open port.
- **Calendar:** dates per child (tests, football, other), the due dates of assignments, subscribed ICS calendars (football team, school) that are imported again every few hours, a secret ICS feed per child for the parents' calendar app, and the next two weeks on the child's start page. fussball.de has no ICS export, so the football dates come from the club's or coach's calendar (or are entered by hand).
- **Access:** Tailscale (MagicDNS + HTTPS certificate), public via Tailscale Funnel. Details in the [security concept](security.md).

## Technology

| Part | Technology |
| --- | --- |
| Backend + API | ASP.NET Core (.NET 10), Minimal APIs, ASP.NET Core Identity |
| Database | EF Core with SQLite, a global filter per family |
| UI | Blazor WebAssembly PWA, `IStringLocalizer` with resx files |
| Telegram | Telegram.Bot as a BackgroundService |
| Calendar | Ical.Net |
| Push | Web Push (VAPID) |
| Operations | one Docker image (ghcr.io) + a Tailscale sidecar on the Synology, backup of `data/` |

The code follows a hexagonal architecture, see [architecture](architecture.md).

## Phases

1. **Core:** quiz format v1, upload, assign, delete, child PWA, results, German and English UI, Docker image with Tailscale.
2. **Calendar and Telegram:** dates, ICS import and feed *(done)*; bot for parents and children, Web Push.
3. **More families:** class groups, export and deletion, turn on Funnel.

## Open questions

- Tailscale on the Synology: sidecar with `--advertise-tags` (current plan) or a label-based proxy such as TSDProxy?
- Sending email (SMTP) later: an existing mailbox or a delivery service, see the security concept.
