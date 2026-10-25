# Sicherheitskonzept

Stand: 25.10.2026. Lernkicker ist über **Tailscale Funnel** öffentlich erreichbar, z. B. unter `https://lernkicker.<tailnet>.ts.net`. Es speichert Daten von Kindern aus mehreren Familien. Deshalb gilt: Die Adresse ist kein Geheimnis, jeder Zugriff braucht eine Anmeldung, und jede Familie sieht nur ihre eigenen Daten.

## Ausgangslage und Bedrohungen

- **Die Adresse ist öffentlich bekannt.** Jedes HTTPS-Zertifikat für `*.ts.net` steht in den öffentlichen Certificate-Transparency-Logs. Scanner finden den Namen innerhalb von Minuten. Sicherheit darf also nie davon abhängen, dass niemand die Adresse kennt.
- **Angreifer aus dem Internet:** Passwörter oder PINs durchprobieren, bekannte Lücken ausnutzen, die NAS als Einstieg ins Heimnetz nutzen.
- **Neugierige Nutzer:** ein Elternteil, das versucht, Ergebnisse anderer Kinder zu sehen.
- **Kinder:** ein Kind, das an die Eltern-Ansicht oder die Lösungen will (harmlos, aber zu bedenken).
- **Datenschutz:** Daten fremder Kinder; Robin ist dafür verantwortlich (DSGVO).

## 1. Netz und Betrieb

| Maßnahme | Umsetzung |
| --- | --- |
| Nur die App ist öffentlich | Funnel leitet nur Port 443 an den App-Container weiter. DSM, SSH und andere Dienste der NAS bleiben unsichtbar. |
| Container abschotten | Eigener Tailscale-Knoten mit `tag:lernkicker`. Die Tailscale-Policy erlaubt diesem Tag **keine** Verbindungen zu anderen Geräten im Tailnet. Docker-Netz ohne Zugriff aufs Heimnetz (eigenes Bridge-Netz; nur ausgehend Internet für Telegram und Kalender-Import). |
| Container härten | Läuft als Nicht-root-Benutzer, Dateisystem schreibgeschützt (`read_only: true`), einziger beschreibbarer Ordner `/data`, `cap_drop: [ALL]`, `no-new-privileges`. |
| TLS | Tailscale beendet TLS im eigenen Container. Die App setzt HSTS und leitet nichts über HTTP aus. |
| Echte Client-IP | Für Rate-Limits liest die App `X-Forwarded-For`, aber nur vom lokalen Tailscale-Proxy. |
| Notaus | `tailscale funnel off` im Sidecar (oder Funnel in der Serve-Konfiguration abschalten) macht die App sofort wieder privat. Für Robin bleibt sie über das Tailnet erreichbar. |
| Updates | Renovate oder Dependabot im Repo für NuGet-Pakete und Basis-Images; neues Image per Container Manager übernehmen. |
| Backup | Hyper Backup verschlüsselt den Ordner `data/` (SQLite-Datenbank, Proben, Schlüssel). |

## 2. Anmeldung

**Keine offene Registrierung.** Konten entstehen nur über Einladungen.

| Wer | Wie | Schutz |
| --- | --- | --- |
| Admin (Robin) | Passkey oder Passwort + TOTP-2FA (Pflicht) | Admin-Bereich zusätzlich nur aus dem Tailnet erreichbar: Anfragen über Funnel tragen den Header `Tailscale-Funnel-Request`, die App lehnt sie für Admin-Seiten ab |
| Elternteil | Einladungslink (einmalig, 7 Tage gültig), dann Passkey oder Passwort (mind. 12 Zeichen, Abgleich mit bekannten geleakten Passwörtern); 2FA empfohlen | Sperre nach 5 Fehlversuchen für 15 Minuten, Rate-Limit pro IP |
| Kind | Elternteil zeigt in seiner Ansicht einen QR-Code, das Kinderhandy scannt ihn: Das Gerät wird **gebunden**. Danach 4-stellige PIN. | Ohne gebundenes Gerät nützt eine PIN nichts. Nach 5 falschen PINs ist das Gerät gesperrt, bis ein Elternteil es freigibt. Elternteil sieht gebundene Geräte und kann sie entfernen. |

Technik: ASP.NET Core Identity (in .NET 10 mit Passkey-Unterstützung). Der Gerätebindungs-Schlüssel ist 256 Bit zufällig und wird nur als Hash gespeichert. Cookies: `Secure`, `HttpOnly`, `SameSite=Strict`, Antiforgery-Token bei allen Änderungen. Die Data-Protection-Schlüssel liegen in `/data/keys`, damit Anmeldungen einen Neustart überleben.

**Passwort vergessen ohne E-Mail:** Der Admin erzeugt für das Elternteil einen einmaligen Rücksetz-Link (1 Stunde gültig) und schickt ihn auf dem Weg, auf dem auch die Einladung kam. Mit Passkeys kommt das selten vor. E-Mail siehe unten.

## 3. Berechtigungen und Trennung der Familien

- Jede Tabelle mit Familiendaten hat eine `FamilieId`. Ein globaler EF-Core-Filter beschränkt jede Abfrage auf die Familie des angemeldeten Benutzers.
- Zusätzlich prüft jeder Endpunkt die Rolle (Admin, Elternteil, Kind). Kinder-Endpunkte geben **nie** Lösungen heraus: Die PWA schickt die Antworten, der Server wertet aus und liefert erst danach Lösung und Erklärung.
- IDs nach außen sind zufällige GUIDs, keine fortlaufenden Nummern.
- Integrationstests prüfen für jeden Endpunkt, dass Familie A keine Daten von Familie B lesen oder ändern kann. Diese Tests sind Pflicht für neue Endpunkte.
- Der Admin verwaltet Familien, sieht aber keine Ergebnisse fremder Kinder.
- Geteilte Proben (Klassengruppe) werden als **Kopie** in die andere Familie übernommen. Ergebnisse werden nie geteilt.

## 4. Eingaben und Uploads

- Proben: nur JSON, höchstens 256 KB, Prüfung gegen `schema/probe-v1.schema.json` und die inhaltlichen Regeln. Ungültige Dateien werden abgelehnt.
- Alle Texte aus Proben werden als Text ausgegeben, nie als HTML (Blazor kodiert standardmäßig; kein `MarkupString` für Probeninhalte). Damit kann eine Probe keinen Schadcode in fremde Browser bringen, auch wenn sie geteilt wird.
- Content-Security-Policy ohne fremde Quellen: Schriften und Skripte liefert die App selbst aus, keine CDNs, keine Tracker.
- Bilder (später als ZIP): Größenlimit, nur PNG/JPEG/WebP, werden neu kodiert und ohne Metadaten gespeichert.
- API-Tokens für Uploads (z. B. für Claude oder Skripte): pro Elternteil, nur Recht „Proben hochladen“, nur als Hash gespeichert, jederzeit widerrufbar.

## 5. Telegram

- Der Bot antwortet nur auf verknüpfte Chat-IDs; alles andere wird ignoriert und nicht gespeichert.
- Verknüpfungscodes sind einmalig, 10 Minuten gültig und nur über die angemeldete Eltern-Ansicht erzeugbar.
- Ein Kind kann nur verknüpft werden, wenn ein Elternteil „Telegram erlaubt“ eingeschaltet hat; das Abschalten löst die Verknüpfung sofort.
- Beim BotFather: Gruppenbeitritt abschalten (`/setjoingroups` → Disable).
- Der Bot-Token liegt als Docker-Secret vor, nie im Repo oder Image.
- Long Polling: Telegram muss die App nicht erreichen können.

## 6. Kalender-Feeds

- Jedes Kind hat eine geheime ICS-Adresse mit einem 256-Bit-Zufallstoken. Die Adresse kann ohne Anmeldung abgerufen werden (das brauchen Kalender-Apps), deshalb kann sie jederzeit neu erzeugt werden; die alte wird dann ungültig.
- Der Feed enthält nur Titel, Datum und Ort der Termine, keinen Nachnamen und keine Ergebnisse.

## 7. Rate-Limits und Überwachung

- ASP.NET Core Rate Limiter: Anmeldung, PIN, Einladung einlösen, Rücksetz-Link, Upload, ICS-Feed jeweils mit eigenem Limit pro IP und pro Konto.
- Fehlgeschlagene Anmeldungen und Sperren werden protokolliert, ohne Passwörter, PINs oder Antworten der Kinder.
- Bei auffälligen Mustern (z. B. mehr als 20 Fehlversuche in 10 Minuten oder eine Gerätesperre) bekommt der Admin eine Telegram-Nachricht.

## 8. Datenschutz

- **Daten so knapp wie möglich:** Von Kindern nur Vor- oder Spitzname und Klasse, kein Geburtsdatum, kein Foto. Von Eltern E-Mail-Adresse (als Login-Name) und Anzeigename.
- **Einwilligung:** Beim Einlösen der Einladung stimmt das Elternteil einem kurzen Datenschutzhinweis zu (wer verantwortlich ist, was gespeichert wird, wo es liegt, wie man es löscht).
- **Export und Löschen:** Eltern können alle Daten ihrer Familie als JSON herunterladen und die Familie vollständig löschen. Löschen entfernt alles sofort aus der Datenbank; Backups laufen nach ihrer Aufbewahrungszeit aus.
- **Aufbewahrung:** Ergebnisse älter als zwei Schuljahre werden automatisch gelöscht.
- Keine Dritten: keine Analyse-Dienste, keine externen Schriften. Telegram nur für Familien, die es selbst einschalten.
- Ein schriftlicher Datenschutzhinweis ist ratsam; dieses Konzept ist keine Rechtsberatung.

## E-Mail

**E-Mail ist für den Start nicht nötig.** Einladungs- und Rücksetz-Links erzeugt die App, und Robin schickt sie selbst (Messenger, persönlich). Benachrichtigungen laufen über Telegram.

Wenn die App später selbst E-Mails senden soll (Einladungen, „Passwort vergessen“, Wochenbericht für Eltern ohne Telegram), braucht sie einen **SMTP-Zugang**. Möglichkeiten:

| Weg | Was nötig ist | Bemerkung |
| --- | --- | --- |
| Vorhandenes Postfach (z. B. Gmail, GMX, mailbox.org) | SMTP-Server, Benutzer, App-Passwort | Am schnellsten. Absender ist dieses Postfach. Tageslimits beachten. |
| Versanddienst (z. B. Brevo, Mailjet, Postmark) | Konto, API-Key oder SMTP-Zugang, eigene Domain mit SPF- und DKIM-Einträgen | Zuverlässigste Zustellung; kleine kostenlose Kontingente reichen hier aus. |
| Eigener Mailserver auf der NAS | – | Nicht empfohlen: Mails von Privatanschlüssen landen oft im Spam. |

In der App: Versand mit MailKit, Zugangsdaten als Docker-Secret, Absender z. B. `lernkicker@<deine-domain>`. Ist kein SMTP konfiguriert, zeigt die App die Links zum Kopieren an, statt sie zu verschicken.
