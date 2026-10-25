# Lernkicker

Self-hosted learning app for primary school kids (see `docs/plan.md`). Runs as a Docker image on a Synology NAS behind Tailscale.

- Everything a child or parent sees in the app is German. Code identifiers and comments in English, except the test format, whose JSON keys are German.
- Tests ("Proben") use the `probe/v1` format: spec in `docs/probe-format.md`, schema in `schema/probe-v1.schema.json`, examples in `proben/`. Keep all three in sync when the format changes.
- Validate tests with `python3 tools/validate_proben.py` (needs `pip install jsonschema`); CI runs the same check.
- New tests for Bavarian 4th grade follow LehrplanPLUS Grundschule; mirror the order and points of the real Probe.
- Stack: .NET 10, ASP.NET Core Minimal APIs, EF Core + SQLite, Blazor WebAssembly PWA, Telegram.Bot, Ical.Net.
