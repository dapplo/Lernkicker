# Probe-Format `probe/v1`

Eine Übungsprobe ist **eine JSON-Datei** (UTF-8). Eltern laden sie in der Eltern-Ansicht hoch, als Datei oder per Einfügen, oder schicken sie über die API. Beim Hochladen prüft Lernkicker die Datei gegen das Schema [`schema/probe-v1.schema.json`](../schema/probe-v1.schema.json) und auf die inhaltlichen Regeln unten. Ein vollständiges Beispiel ist [`proben/hsu/hsu-getreide-wahlen.json`](../proben/hsu/hsu-getreide-wahlen.json).

Lokal prüfen:

```sh
pip install jsonschema
python3 tools/validate_proben.py meine-probe.json
```

## Aufbau

```json
{
  "$schema": "../../schema/probe-v1.schema.json",
  "format": "probe/v1",
  "id": "hsu-wasserkreislauf",
  "titel": "Der Wasserkreislauf",
  "fach": "HSU",
  "klasse": 4,
  "thema": "Wasser",
  "dauerMinuten": 20,
  "mischen": true,
  "aufgaben": [ ... ]
}
```

| Feld | Pflicht | Bedeutung |
| --- | --- | --- |
| `format` | ja | immer `"probe/v1"` |
| `id` | ja | Kennung aus Kleinbuchstaben, Ziffern und `-`, 3–64 Zeichen. Gleiche `id` noch einmal hochladen = neue Version; Ergebnisse bleiben an der alten Version. |
| `titel` | ja | Titel, den das Kind sieht |
| `fach` | ja | `HSU`, `Deutsch`, `Mathematik`, `Englisch`, `Religion`, `Ethik`, `Musik`, `Kunst`, `Sport`, `Werken`, `Sonstiges` |
| `klasse` | ja | Jahrgangsstufe, 1–13 |
| `thema` | nein | z. B. „Wasser“, zum Filtern |
| `beschreibung` | nein | kurzer Text für die Eltern |
| `dauerMinuten` | nein | geschätzte Dauer |
| `quelle` | nein | z. B. „Hefteintrag vom 20.10.“ |
| `mischen` | nein | Standard `true`: Optionen, Zeilen und Schritte werden bei jeder Runde neu gemischt |
| `aufgaben` | ja | 1–50 Aufgaben, Reihenfolge wie in der echten Probe |
| `$schema` | nein | Pfad zum Schema, damit Editoren (VS Code, Rider) Autovervollständigung anbieten |

## Felder jeder Aufgabe

| Feld | Pflicht | Bedeutung |
| --- | --- | --- |
| `id` | ja | eindeutig innerhalb der Probe |
| `typ` | ja | `luecke`, `zuordnen`, `reihenfolge`, `richtigFalsch`, `freitext`, `auswahl` |
| `anweisung` | ja | Arbeitsauftrag, wie er in der Probe steht |
| `hinweis` | nein | kleiner Bedienhinweis unter der Anweisung |
| `punkte` | nein | Höchstpunktzahl der Aufgabe (siehe Bewertung) |
| `erklaerung` | nein | wird nach dem Auswerten gezeigt, z. B. ein Merksatz |
| `loesungstext` | nein | Text für „Lösungen zeigen“; fehlt er, erzeugt die App ihn |

## Aufgabentypen

### `luecke` – Lückentext

Jedes `___` (drei Unterstriche) im `text` ist eine Lücke. `luecken[i]` gehört zur i-ten Lücke und listet alle Schreibweisen, die als richtig gelten. Mit `wortspeicher` werden die Lücken Auswahllisten, sonst Eingabefelder.

```json
{
  "id": "kreislauf", "typ": "luecke",
  "anweisung": "Setze ein.",
  "wortspeicher": ["verdunstet", "Wolken", "Niederschlag"],
  "text": "Die Sonne erwärmt das Wasser, es ___. Der Dampf bildet ___. Daraus fällt ___.",
  "luecken": [
    { "antworten": ["verdunstet"] },
    { "antworten": ["Wolken"] },
    { "antworten": ["Niederschlag", "Regen"] }
  ]
}
```

### `zuordnen` – Zeilen einer Option zuordnen

Jede Zeile bekommt per Auswahlliste eine der `optionen`. Eine Option darf zu mehreren Zeilen passen. `antwort` muss exakt einer Option entsprechen.

```json
{
  "id": "mehl", "typ": "zuordnen",
  "anweisung": "Weißmehl oder Vollkornmehl?",
  "optionen": ["Weißmehl", "Vollkornmehl"],
  "zeilen": [
    { "text": "Nur der helle Mehlkörper wird gemahlen.", "antwort": "Weißmehl" },
    { "text": "Das ganze Korn wird gemahlen.", "antwort": "Vollkornmehl" }
  ]
}
```

### `reihenfolge` – Schritte ordnen

`schritte` in der **richtigen** Reihenfolge angeben (2–12). Die App mischt sie, das Kind tippt sie der Reihe nach an. Ein Schritt zählt als richtig, wenn er an seiner Position steht.

```json
{ "id": "brot", "typ": "reihenfolge", "anweisung": "Bringe in die richtige Reihenfolge.",
  "schritte": ["Säen", "Ernten", "Mahlen", "Backen"] }
```

### `richtigFalsch` – Aussagen bewerten

```json
{ "id": "rf", "typ": "richtigFalsch", "anweisung": "Richtig oder falsch?",
  "aussagen": [
    { "text": "Getreide wird im Frühling geerntet.", "richtig": false, "erklaerung": "Geerntet wird im Sommer." }
  ] }
```

### `freitext` – Antworten eintippen

Ein Eingabefeld je Eintrag in `felder`. Groß-/Kleinschreibung und Leerzeichen am Rand werden ignoriert; andere Schreibweisen (z. B. Endungen) als weitere `antworten` angeben. Mit `reihenfolgeEgal: true` darf jede richtige Antwort in jedem Feld stehen, zählt aber nur einmal.

```json
{ "id": "grundsaetze", "typ": "freitext", "anweisung": "Schreibe die 5 Wahlgrundsätze auf.",
  "reihenfolgeEgal": true,
  "felder": [ { "antworten": ["frei", "freie"] }, { "antworten": ["geheim", "geheime"] } ] }
```

Optional hat jedes Feld eine `beschriftung`, z. B. `"Hauptstadt von Bayern:"`.

### `auswahl` – Multiple Choice

`richtig` ist der Index der richtigen Option (ab 0) oder eine Liste von Indizes, wenn mehrere richtig sind. 2–6 Optionen je Frage.

```json
{ "id": "demokratie", "typ": "auswahl", "anweisung": "Kreuze an.",
  "fragen": [
    { "frage": "Wählen darf man ab …", "optionen": ["10 Jahren.", "18 Jahren."], "richtig": 1 }
  ] }
```

## Bewertung

- Jede Lücke, Zeile, Aussage, jeder Schritt, jedes Feld und jede Frage ist ein **Teil**. Ohne `punkte` gibt jeder richtige Teil 1 Punkt.
- Mit `punkte` gilt: erreichte Punkte = gerundet(richtige Teile ÷ alle Teile × `punkte`), kaufmännisch gerundet. Beispiel: 7 Schritte, `punkte: 4`, 5 richtig → 2,86 → 3 Punkte.
- Bei `auswahl` mit mehreren richtigen Optionen zählt die Frage nur, wenn genau die richtigen angekreuzt sind.
- Die Note ist nur eine Orientierung: ab 95 % → 1, 83 % → 2, 67 % → 3, 50 % → 4, 30 % → 5, darunter 6.

## Inhaltliche Regeln (prüft `tools/validate_proben.py`)

- Anzahl der `___` im `text` = Anzahl der Einträge in `luecken`.
- Mit `wortspeicher`: jede Lücke hat mindestens eine Antwort aus dem Wortspeicher.
- `zuordnen`: jede `antwort` steht in `optionen`.
- `auswahl`: jeder Index in `richtig` zeigt auf eine vorhandene Option.
- Aufgaben-`id`s sind innerhalb der Probe eindeutig.

## Hinweise für Autoren (und für Claude)

- Alles, was das Kind sieht, ist auf Deutsch und in der Sprache der 4. Klasse.
- Aufgaben in der Reihenfolge und mit den Punkten der echten Probe anlegen, damit sich die Übung wie die Probe anfühlt.
- Falsche Antworten möglichst mit `erklaerung` versehen: Die erscheint direkt nach dem Auswerten.
- Bilder sind in v1 noch nicht vorgesehen; sie kommen in einer späteren Version als ZIP-Upload.
