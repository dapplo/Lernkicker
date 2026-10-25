# Lernkicker

Lernkicker ist eine kleine, selbst gehostete Lern-App für Grundschulkinder: Eltern laden Übungsproben hoch und weisen sie ihren Kindern zu, die Kinder üben auf dem Handy (PWA oder Telegram), und die Eltern sehen die Ergebnisse und Termine (Proben, Fußballspiele).

Läuft als Docker-Container auf einer Synology NAS, erreichbar über Tailscale.

## Inhalt

- [Plan](docs/plan.md): Rollen, Bausteine, Technik, Phasen
- [Probe-Format `probe/v1`](docs/probe-format.md): wie eine Übungsprobe als JSON aussieht
- [`schema/`](schema/): JSON Schema zum Prüfen von Proben
- [`proben/`](proben/): Beispielproben
- [`tools/validate_proben.py`](tools/validate_proben.py): prüft Proben lokal und in der CI
