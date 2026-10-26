# Lernkicker-Probe oder Lernstoff mit einer KI erstellen

Kopiere diesen ganzen Text in einen KI-Chat (Claude, ChatGPT, Gemini, …), fülle den Teil unter „Mein Auftrag“ aus und hänge Fotos der Arbeitsblätter oder Hefteinträge an. Die App hängt die beiden JSON-Schemas unten an; wenn du den Text von GitHub kopiert hast, hänge zusätzlich `schema/quiz-v1.schema.json` und `schema/lesson-v1.schema.json` an.

---

Du erstellst Übungsmaterial für **Lernkicker**, eine Lern-App für Schulkinder. Eltern laden, was du erstellst, im Elternbereich der App hoch. Es gibt zwei Arten von Dateien:

- eine **Probe** (`quiz/v1`): ein Übungstest, den die App bewertet, wie die echte Probe in der Schule;
- **Lernstoff** (`lesson/v1`): etwas zum Lesen und Lernen ohne Bewertung: ein Text, ein Gedicht zum Auswendiglernen, Lernkarten, Merksätze, Bilder oder etwas zum Nachschlagen (z. B. Spielregeln).

## Mein Auftrag

- Was ich möchte: [eine Probe / Lernstoff / beides]
- Thema: [z. B. „Der Wasserkreislauf“, oder siehe die angehängten Fotos]
- Klasse und Schulart: [z. B. 4. Klasse, Grundschule in Bayern, oder „Erwachsene“]
- Lehrplan oder Probe: [z. B. „LehrplanPLUS“, „die Probe ist am 12. November“, „nimm Reihenfolge und Punkte der angehängten alten Probe“]
- Sprache des Inhalts: [z. B. Deutsch]
- Bilder: [keine / zeichne einfache Bilder selbst / ich füge Fotos hinzu]

## So schreibst du es

- Schreibe den ganzen **Inhalt** (Titel, Arbeitsaufträge, Fragen, Antworten, Erklärungen, `alt`-Texte) in der Sprache oben und passend für das Kind: kurze Sätze, Wörter, die das Kind aus der Schule kennt. Die JSON-**Schlüssel** und festen Werte bleiben englisch, genau wie im Schema.
- Setze `language` auf die Sprache des Inhalts als BCP-47-Kürzel (`de`, `en`, …) und `gradeLevel` auf die Klassenstufe. Für Erwachsene lässt du `gradeLevel` weg und schreibst für Erwachsene.
- Bleib beim Stoff: Nimm, was auf den Fotos steht oder was der Lehrplan für diese Klasse vorsieht. Erfinde keine Fakten; wenn etwas unklar ist, frag mich, bevor du die Datei schreibst.
- Für eine Probe: Nummeriere die Arbeitsaufträge wie in einer echten Probe (`"1. Setze ein."`). Wenn ich eine echte Probe angehängt habe, behalte ihre Reihenfolge und ihre Punkte (`points` je Aufgabe). Mische die Aufgabentypen und gib falschen Antworten eine `explanation` (z. B. eine Eselsbrücke). Liste in `answers` jede Schreibweise auf, die zählen soll (z. B. mit und ohne Endung).
- Für Lernstoff: ein Thema pro Datei, 1–30 Blöcke. Kurze `facts`, `flashcards` für Begriffe, `text` für Erklärungen, `poem` für alles, was wörtlich gelernt wird. Für etwas zum Nachschlagen (Spielregeln, ein Wörterverzeichnis) setze `"mode": "reference"` und nimm einen `entries`-Block: ein Eintrag pro Karte oder Begriff, mit `name` und kurzem `text`.
- `id`: Kleinbuchstaben, Ziffern und `-`, 3–64 Zeichen, nach dem Thema benannt (z. B. `hsu-water-cycle`). Ids von Aufgaben und Blöcken: kurz und eindeutig innerhalb der Datei.

## Regeln, die das Schema nicht prüft

Lernkicker lehnt eine Datei ab, die eine dieser Regeln verletzt:

- `cloze`: Jedes `___` (drei Unterstriche) in `text` ist eine Lücke; es gibt genauso viele Lücken wie Einträge in `gaps`. Mit `wordBank` hat jede Lücke mindestens eine Antwort aus dem Wortspeicher.
- `matching`: Die `answer` jeder Zeile ist genau eine der `options`.
- `multipleChoice`: `correct` ist der Index (ab 0) der richtigen Antwort oder eine Liste von Indizes; jeder Index zeigt auf eine vorhandene Antwort. 2–6 Antworten.
- `ordering`: Liste die `steps` in der **richtigen** Reihenfolge auf; die App mischt sie.
- `wordSearch`: `words` bestehen nur aus Buchstaben (Umlaute und ß sind erlaubt), ohne Leerzeichen oder Bindestriche, 2–14 Buchstaben, jedes Wort nur einmal. Die App baut jede Runde ein neues Gitter; für ältere Kinder `"level": "medium"` (schräg, Wörter kreuzen sich) oder `"hard"` (auch rückwärts) setzen; das voreingestellte `easy` passt für die Klassen 1–3.
- `fallingWords`: Die `answer` jedes Worts ist genau eine der `options`. Halte die Antworten kurz (ein Wort oder wenige), weil bis zu vier nebeneinander auf ein Handy passen müssen.
- Aufgaben-Ids sind innerhalb einer Probe eindeutig, Block-Ids innerhalb eines Lernstoffs.
- Eine Probe hat höchstens 50 Aufgaben; eine Lernstoff-Datei ist höchstens 256 KB groß.

## Bilder

Bilder sind freiwillig. Nimm sie, wo die echte Probe welche hat (Karten, Schaubilder, Fotos von Pflanzen) oder wo sie einem jungen Kind helfen.

- Ein Bild ist ein `image`-Objekt: `{ "src": "water-cycle/clouds.svg", "alt": "…", "caption": "…", "credit": "…" }`. `src` und `alt` sind Pflicht. `alt` sagt in der Sprache des Inhalts, worauf es auf dem Bild für die Aufgabe ankommt.
- `src` ist ein Pfad **relativ zur JSON-Datei**, z. B. `water-cycle/clouds.svg`. Keine URLs, kein `/` am Anfang, kein `..`. Verwende in Dateinamen Buchstaben, Ziffern, `-`, `_` und `.`.
- Erlaubt sind PNG, JPEG, GIF, WebP und SVG, höchstens 2 MB pro Bild und 20 MB pro Datei zusammen. Aus einem SVG entfernt die App Skripte, Links und alles andere, was keine Zeichnung ist.
- Wo Bilder in einer Probe stehen können: an jeder Aufgabe (`image`), an einer `matching`-Zeile oder einem `fallingWords`-Wort, einer `trueFalse`-Aussage, einer `multipleChoice`-Frage und als Bildantworten (eine Antwort `{ "image": {…}, "text": "freiwillig" }` statt eines Texts).
- Wo Bilder im Lernstoff stehen können: an `text`- und `facts`-Blöcken, auf Lernkarten (`frontImage`, `backImage`), an Einträgen (`entries`) und als eigener `image`-Block.
- Wenn du Bilder selbst zeichnest, zeichne einfache, klare SVGs (Formen, Schaubilder, eine Kartenskizze) mit `viewBox`, ohne Text, der die Lösung verrät, und ohne externe Schriften oder Links.
- Für Bilder aus dem Internet (z. B. Wikimedia Commons) trage in `credit` Quelle und Lizenz ein, und nimm nur Bilder, deren Lizenz das erlaubt.

## Was ich von dir bekomme

- **Ohne Bilder:** die JSON-Datei, vollständig, in einem Codeblock (oder als Datei zum Herunterladen).
- **Mit Bildern:** eine **Zip-Datei** mit genau einer `.json`-Datei und den Bildern in einem Ordner daneben, passend zu den `src`-Pfaden:

  ```
  hsu-water-cycle.zip
  ├── hsu-water-cycle.json
  └── water-cycle/
      ├── clouds.svg
      └── river.jpg
  ```

  Wenn du keine Zip-Datei erstellen kannst, gib mir die JSON-Datei und jedes SVG in einem eigenen Codeblock, dazu eine Liste der Fotos, die ich hinzufügen muss, mit dem Dateinamen, den jedes braucht.
- Für Probe und Lernstoff erstelle zwei getrennte Dateien (zwei Zip-Dateien, wenn sie Bilder haben).
- Prüfe die Datei gegen das Schema unten und die Regeln oben, bevor du sie mir gibst.

## Kleine Beispiele

Eine Probe:

```json
{
  "format": "quiz/v1",
  "id": "hsu-water-cycle",
  "title": "Der Wasserkreislauf",
  "subject": "science",
  "gradeLevel": 4,
  "language": "de",
  "exercises": [
    {
      "id": "cycle", "type": "cloze", "points": 3,
      "instruction": "1. Setze ein.",
      "wordBank": ["verdunstet", "Wolken", "Regen"],
      "text": "Die Sonne erwärmt das Wasser, es ___. Der Dampf bildet ___. Daraus fällt ___.",
      "gaps": [{ "answers": ["verdunstet"] }, { "answers": ["Wolken"] }, { "answers": ["Regen"] }]
    },
    {
      "id": "true-false", "type": "trueFalse",
      "instruction": "2. Richtig oder falsch?",
      "statements": [
        { "text": "Wasser verdunstet nur im Sommer.", "isTrue": false, "explanation": "Wasser verdunstet das ganze Jahr, im Sommer nur schneller." }
      ]
    }
  ]
}
```

Lernstoff:

```json
{
  "format": "lesson/v1",
  "id": "hsu-water-cycle-facts",
  "title": "Der Wasserkreislauf",
  "subject": "science",
  "gradeLevel": 4,
  "language": "de",
  "blocks": [
    { "id": "facts", "type": "facts", "title": "Merke", "items": ["Die Sonne lässt Wasser verdunsten.", "Aus Wasserdampf werden Wolken."] },
    { "id": "terms", "type": "flashcards", "title": "Begriffe", "cards": [{ "front": "Niederschlag", "back": "Regen, Schnee oder Hagel" }] }
  ]
}
```
