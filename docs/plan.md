# Lernkicker – Plan

Stand: 25.10.2026. Ausführliche Fassung mit Diagrammen: Claude-Doc „Lern-App: Umsetzungsplan“.

## Ziel

Kinder üben Übungsproben auf dem Handy, Eltern wählen die Proben aus und sehen Ergebnisse und Termine. Mehrere Familien (z. B. Klassenkameraden) nutzen dieselbe Installation, jede Familie sieht nur ihre eigenen Daten.

## Rollen

| Rolle | Anmeldung | Darf |
| --- | --- | --- |
| Admin | E-Mail + Passwort, 2FA | Familien einladen und löschen, Betrieb; sieht keine Ergebnisse fremder Kinder |
| Elternteil | Einladungslink, dann E-Mail + Passwort | Kinder anlegen, Proben hochladen, löschen und zuweisen, Ergebnisse sehen, Termine pflegen, Telegram freischalten |
| Kind | QR-Code vom Elternteil, danach 4-stellige PIN | Zugewiesene Proben üben, Sterne und Termine sehen |

## Bausteine

- **Proben:** JSON im Format [`probe/v1`](probe-format.md). Hochladen (Datei, Einfügen, API mit Token), Zuweisen an Kinder (optional mit Termin), Löschen (mit Ergebnissen: archivieren). Optional für eine Klassengruppe freigeben; Ergebnisse bleiben privat.
- **Kinder-App:** PWA, ein Symbol auf dem Startbildschirm, kurze Runden, Sterne, falsche Fragen kommen wieder.
- **Telegram:** ein Bot, nur für verknüpfte Konten (Einmal-Code per `t.me/<bot>?start=…`). Eltern: Ergebnisse, Erinnerungen, `/senden`. Kind: nur wenn freigeschaltet, Fragen mit Antwortknöpfen. Long Polling, kein offener Port.
- **Kalender:** Termine je Kind (Proben, Fußball, Sonstiges), Import von Team-Kalendern (ICS, z. B. fussball.de), geheimer ICS-Feed je Kind.
- **Zugang:** Tailscale (MagicDNS + HTTPS-Zertifikat), öffentlich über Tailscale Funnel. Details im [Sicherheitskonzept](sicherheit.md).

## Technik

| Teil | Technik |
| --- | --- |
| Backend + API | ASP.NET Core (.NET 10), Minimal APIs, ASP.NET Core Identity |
| Datenbank | EF Core mit SQLite, globaler Filter je Familie |
| Oberfläche | Blazor WebAssembly mit PWA-Vorlage |
| Telegram | Telegram.Bot als BackgroundService |
| Kalender | Ical.Net |
| Push | Web Push (VAPID) |
| Betrieb | ein Docker-Image (ghcr.io) + Tailscale-Sidecar auf der Synology, Backup von `data/` |

## Phasen

1. **Kern:** Probe-Format v1 (dieser Stand), Upload, Zuweisen, Löschen, Kinder-PWA, Ergebnisse, Docker-Image mit Tailscale.
2. **Telegram und Kalender:** Bot für Eltern und Kinder, Termine, ICS, Fußball-Import, Web Push.
3. **Weitere Familien:** Einladungen, Klassengruppen, Datenschutz-Seite, Export und Löschen, Funnel einschalten.

## Offen

- Tailscale-Einrichtung auf der Synology: Sidecar mit `--advertise-tags` oder Label-basierter Proxy (z. B. TSDProxy)?
- Lizenz des Repos.
- E-Mail-Versand (SMTP) später: vorhandenes Postfach oder Versanddienst, siehe Sicherheitskonzept.
