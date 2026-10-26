# Lesson format `lesson/v1`

Learning material is **one JSON file** (UTF-8): a poem to learn by heart, a text to read, flashcards, key facts, or a guide to look things up in (e.g. the rules of a game). Unlike a quiz it is not graded. The child reads and practises it on the phone and marks each part they know with "Kann ich!", and parents see that progress.

Parents upload a lesson under **Lernstoff verwalten / Manage learning material**, then give it to a child on the child's page, optionally with a due date (e.g. the day the poem is recited). The due date also shows up in the child's calendar. On upload, Lernkicker checks the file against [`schema/lesson-v1.schema.json`](../schema/lesson-v1.schema.json) and the rules below. The examples are [`lessons/german/poem-septembermorgen.json`](../lessons/german/poem-septembermorgen.json) and, for a guide to look things up in, [`lessons/other/game-mau-mau.json`](../lessons/other/game-mau-mau.json).

As with quizzes, the keys are English and the **content** is in the lesson's `language` (default `de`).

To have an AI assistant write one, give it the [prompt for AI assistants](ai-prompt/README.md); the upload page offers it for download (**Create with an AI**), with both schemas included.

Check locally (the same script checks quizzes and lessons, picked by `format`):

```sh
pip install jsonschema
python3 tools/validate_quizzes.py my-lesson.json
```

## Structure

```json
{
  "$schema": "../../schema/lesson-v1.schema.json",
  "format": "lesson/v1",
  "id": "poem-septembermorgen",
  "title": "Gedicht: Septembermorgen",
  "subject": "german",
  "gradeLevel": 4,
  "language": "de",
  "topic": "Gedichte",
  "description": "Ein kurzes Herbstgedicht zum Auswendiglernen.",
  "source": "Eduard Mörike (1827), gemeinfrei",
  "blocks": [ ... ]
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `format` | yes | Always `lesson/v1` |
| `id` | yes | Lowercase letters, digits and hyphens, 3–64 characters. Unique within a family: uploading the same `id` again **replaces** the lesson, and assignments and progress stay |
| `title` | yes | At most 120 characters |
| `subject` | yes | Same list as quizzes: `science` (HSU), `german`, `math`, `english`, `religion`, `ethics`, `music`, `art`, `sports`, `crafts`, `other` |
| `gradeLevel` | no | School year, 1–13. Leave it out for material for adults (e.g. for work) |
| `language` | no | BCP 47 tag of the content, default `de` |
| `mode` | no | `learn` (default) or `reference`, see [Learning or looking up](#learning-or-looking-up) |
| `topic`, `description`, `source` | no | Shown to the child (`topic`, `description`) or kept for parents (`source`, e.g. the worksheet) |
| `blocks` | yes | 1–30 parts, shown in this order |

## Learning or looking up

`mode` says how the child uses the lesson:

- **`learn`** (default): something to learn. Every part has a "Kann ich!" button, the child finishes with "Fertig für heute", and parents see how many parts the child knows.
- **`reference`**: something to look things up in, e.g. the rules of a card game or a list of signs. There are no "Kann ich!" buttons and no progress; instead a row of the parts' titles at the top jumps to each part. The child's list and the parents' page show it as "Zum Nachschlagen".

All block types work in both modes; an `entries` block fits a reference lesson best.

Pictures work as in quizzes: see [Pictures](#pictures). [`lessons/math/shapes.json`](../lessons/math/shapes.json) uses each kind of picture once.

Every block has an `id` (lowercase, digits, hyphens, at most 32 characters, unique within the lesson), a `type` and an optional `title`. Without a title the app shows the type's name ("Gedicht", "Lernkarten", …). Progress is stored per block `id`, so keep the ids when you change a lesson.

## Block types

### `text`: something to read

```json
{ "id": "tips", "type": "text", "title": "So lernst du ein Gedicht",
  "text": "Lies das Gedicht **zweimal laut**.\n\n- Lerne immer nur zwei Zeilen.\n- Sag sie auf, ohne hinzuschauen.\n\nAm nächsten Tag *wiederholst* du alles." }
```

An optional `image` is shown above the text. `text` (at most 10,000 characters) knows a little formatting: an empty line starts a new paragraph, lines starting with `- ` are a list, `**bold**` and `*italic*`. Nothing else is interpreted; HTML is shown as plain text.

### `poem`: learn by heart

```json
{ "id": "poem", "type": "poem", "title": "Septembermorgen", "author": "Eduard Mörike",
  "hint": "Achte beim Aufsagen auf Pausen am Ende jeder Zeile.",
  "stanzas": [ ["Im Nebel ruhet noch die Welt,", "Noch träumen Wald und Wiesen:"], ["…"] ] }
```

`stanzas` is a list (1–40) of stanzas, each a list (1–30) of lines (at most 300 characters each). `author` and `hint` are optional. The app offers three modes:

- **Lesen / Read:** the whole poem.
- **Üben / Practise:** words disappear step by step (every third word, every second, all but the first word of a line, all). The child taps a gap to see the word.
- **Aufsagen / Recite:** the lines are hidden; the child says the next line out loud and then reveals it to check.

It works for any text to learn word for word, e.g. a times table rhyme or a short speech.

### `flashcards`: question and answer

```json
{ "id": "terms", "type": "flashcards", "title": "Begriffe",
  "cards": [ { "front": "Strophe", "back": "Mehrere Verse, die zusammengehören" } ] }
```

1–100 cards with `front` and `back` (at most 300 characters each). `frontImage` and `backImage` put a picture above the text of that side, e.g. a picture on the front and its name on the back. The child turns a card over and says "Wusste ich" (done) or "Nochmal" (comes again at the end), until every card is done. The order is shuffled.

### `facts`: remember this

```json
{ "id": "facts", "type": "facts", "title": "Merke", "items": ["Mörike lebte von 1804 bis 1875."] }
```

1–30 short sentences, shown as a highlighted list, optionally with an `image` above them.

### `entries`: things to look up

```json
{ "id": "cards", "type": "entries", "title": "Sonderkarten",
  "entries": [
    { "name": "7", "text": "Der Nächste zieht **2 Karten**." },
    { "name": "Bube", "text": "Du wünschst dir eine Farbe.", "image": { "src": "cards/jack.svg", "alt": "Ein Bube" } }
  ] }
```

1–100 entries, each with a `name` (at most 120 characters), a `text` (at most 2,000 characters, same formatting as a `text` block) and an optional small `image` next to it. The app shows them as a compact list; with more than five entries there is a search field that keeps the entries containing every word typed, in the name or the text. Use it for what each card of a game does, a glossary, or traffic signs.

### `image`: a picture on its own

```json
{ "id": "map", "type": "image", "title": "Bayern",
  "image": { "src": "maps/bavaria.png", "alt": "Karte von Bayern mit den sieben Regierungsbezirken", "credit": "Wikimedia Commons, CC BY-SA 4.0" } }
```

A large picture; tapping it shows it full screen. Like every block, the child can mark it with "Kann ich!".

## Games from flashcards and entries

Nothing has to be written for games: the cards of every `flashcards` block (`front`, `back`) and the entries of every `entries` block (`name`, `text`) are pairs of a term and what it means. Under the lesson the child gets a **Spielen / Play** section with each game the pairs are enough for, and every round picks new pairs in a new order. Games are practice only: nothing is graded or stored. [`lessons/science/grain-terms.json`](../lessons/science/grain-terms.json) unlocks all of them.

| Game | Needs at least 4 pairs where | A round |
| --- | --- | --- |
| Wörter fangen / Catch the words | the term has at most 40 characters and the meaning at most 60 | up to 12 terms fall; the child taps their meaning (3 shown at a time), like the quiz type `fallingWords` |
| Memory | the term has at most 30 characters and the meaning at most 60 | 6 pairs, term and meaning on separate cards (the front picture goes on the term's card) |
| Kreuzworträtsel / Crossword | the term is one word of 3–12 letters and the meaning at most 120 characters | up to 10 words that cross; the meanings are the clues |
| Suchsel / Word search | the term is one word of 3–12 letters | up to 10 words in a new grid, like the quiz type `wordSearch` (`medium`) |

"One word" ignores a leading article (`der`, `die`, `das`, `ein`, `eine`, `the`, `a`, `an`), so "die Ähre" counts as "Ähre"; terms with spaces, digits or hyphens don't count. `**` and `*` are removed. A term or a meaning that appears twice is used once, so every round has exactly one right answer. To unlock games, write fronts as single words and keep backs short.

## Pictures

Pictures are files uploaded **together with the JSON**: the JSON names each one in an `image` object, and the parent uploads the JSON with its pictures, either as a `.zip` or by picking all files at once. Without pictures, a file stays exactly as before. Replacing a lesson replaces its pictures too.

```json
"image": { "src": "plants/rose.jpg", "alt": "Eine rote Rose", "caption": "Die Rose", "credit": "Wikimedia Commons, CC0" }
```

| Field | Required | Meaning |
| --- | --- | --- |
| `src` | yes | Path of the picture relative to the JSON file, e.g. `plants/rose.jpg`. No URLs, no leading `/`, no `..`; letters, digits, umlauts, `-`, `_`, `.`, spaces and `/` |
| `alt` | yes | What the picture shows, in the content language (at most 200 characters). Screen readers read it, and the app shows it when the picture is missing |
| `caption` | no | Text under the picture |
| `credit` | no | Source and licence, shown small under the picture. Fill it in for pictures from the internet (e.g. Wikimedia Commons) |

How the upload finds the files:

- **Zip:** exactly one `.json` file; `src` is relative to its folder. Folders such as `__MACOSX` are ignored; paths with `..` are refused.
- **Several files picked at once:** the browser sends file names without folders, so `plants/rose.jpg` also matches a file named `rose.jpg` (as long as only one file has that name).
- **API:** `POST` the raw JSON as before, a zip with `Content-Type: application/zip`, or a `multipart/form-data` form with the files.

Rules:

- Formats: PNG, JPEG, GIF, WebP and SVG, recognised by the file's content, not its name. An SVG is cleaned on upload: only drawing elements stay, and scripts, event handlers, embedded HTML and links to anything outside the file are removed.
- At most 2 MB per picture and 20 MB for all pictures of one file together; at most 100 files per upload. The parents' page shrinks large photos (longest side 1600 px) before uploading, which also removes their metadata such as the GPS position.
- A picture used several times is uploaded and stored once.
- Issues: `missingImage` (no file for a `src`), `imageType` (not one of the formats), `imageTooLarge`, `imagesTooLarge`.
- Downloading a file with pictures (`GET …/{id}/zip`) gives a zip in the same layout, ready to change and upload again.

## Rules beyond the schema

- Block ids must be unique within the lesson (`duplicateId`).
- The file may be at most 256 KB.

Errors come back as the same issue codes as for quizzes (`schema`, `invalidJson`, `duplicateId`, …), with the JSON path of the problem.
