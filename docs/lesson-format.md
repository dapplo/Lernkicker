# Lesson format `lesson/v1`

Learning material is **one JSON file** (UTF-8): a poem to learn by heart, a text to read, flashcards, key facts. Unlike a quiz it is not graded. The child reads and practises it on the phone and marks each part they know with "Kann ich!", and parents see that progress.

Parents upload a lesson under **Lernstoff verwalten / Manage learning material**, then give it to a child on the child's page, optionally with a due date (e.g. the day the poem is recited). The due date also shows up in the child's calendar. On upload, Lernkicker checks the file against [`schema/lesson-v1.schema.json`](../schema/lesson-v1.schema.json) and the rules below. The example is [`lessons/german/poem-septembermorgen.json`](../lessons/german/poem-septembermorgen.json).

As with quizzes, the keys are English and the **content** is in the lesson's `language` (default `de`).

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
| `gradeLevel` | yes | School year, 1–13 |
| `language` | no | BCP 47 tag of the content, default `de` |
| `topic`, `description`, `source` | no | Shown to the child (`topic`, `description`) or kept for parents (`source`, e.g. the worksheet) |
| `blocks` | yes | 1–30 parts, shown in this order |

Every block has an `id` (lowercase, digits, hyphens, at most 32 characters, unique within the lesson), a `type` and an optional `title`. Without a title the app shows the type's name ("Gedicht", "Lernkarten", …). Progress is stored per block `id`, so keep the ids when you change a lesson.

## Block types

### `text`: something to read

```json
{ "id": "tips", "type": "text", "title": "So lernst du ein Gedicht",
  "text": "Lies das Gedicht **zweimal laut**.\n\n- Lerne immer nur zwei Zeilen.\n- Sag sie auf, ohne hinzuschauen.\n\nAm nächsten Tag *wiederholst* du alles." }
```

`text` (at most 10,000 characters) knows a little formatting: an empty line starts a new paragraph, lines starting with `- ` are a list, `**bold**` and `*italic*`. Nothing else is interpreted; HTML is shown as plain text.

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

1–100 cards with `front` and `back` (at most 300 characters each). The child turns a card over and says "Wusste ich" (done) or "Nochmal" (comes again at the end), until every card is done. The order is shuffled.

### `facts`: remember this

```json
{ "id": "facts", "type": "facts", "title": "Merke", "items": ["Mörike lebte von 1804 bis 1875."] }
```

1–30 short sentences, shown as a highlighted list.

## Rules beyond the schema

- Block ids must be unique within the lesson (`duplicateId`).
- The file may be at most 256 KB.

Errors come back as the same issue codes as for quizzes (`schema`, `invalidJson`, `duplicateId`, …), with the JSON path of the problem.
