# Quiz format `quiz/v1`

A practice quiz is **one JSON file** (UTF-8). Parents upload it in the parents' area, as a file or by pasting, or send it through the API. On upload, Lernkicker checks the file against the schema [`schema/quiz-v1.schema.json`](../schema/quiz-v1.schema.json) and the content rules below. A real test is [`quizzes/hsu/grain-and-elections.json`](../quizzes/hsu/grain-and-elections.json); [`quizzes/other/showcase-all-exercise-types.json`](../quizzes/other/showcase-all-exercise-types.json) uses every exercise type and every optional field once, to try them out or copy from.

The format's keys are English. The **content** (titles, questions, answers) is in whatever language the child learns in, usually German; declare it with `language`.

To have an AI assistant write one, give it the [prompt for AI assistants](ai-prompt/README.md); the upload page offers it for download (**Create with an AI**), with both schemas included.

Check locally:

```sh
pip install jsonschema
python3 tools/validate_quizzes.py my-quiz.json
```

## Structure

```json
{
  "$schema": "../../schema/quiz-v1.schema.json",
  "format": "quiz/v1",
  "id": "hsu-water-cycle",
  "title": "Der Wasserkreislauf",
  "subject": "science",
  "gradeLevel": 4,
  "language": "de",
  "topic": "Wasser",
  "durationMinutes": 20,
  "shuffle": true,
  "exercises": [ ... ]
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `format` | yes | always `"quiz/v1"` |
| `id` | yes | lower-case letters, digits and `-`, 3–64 characters. Uploading the same `id` again creates a new version; results stay with the old version. |
| `title` | yes | title the child sees |
| `subject` | yes | `science` (Bavarian HSU / Sachunterricht), `german`, `math`, `english`, `religion`, `ethics`, `music`, `art`, `sports`, `crafts`, `other`. The app shows the subject name in the user's language. |
| `gradeLevel` | no | school year, 1–13. Leave it out for material for adults (e.g. for work); the app then shows "for adults". |
| `language` | no | language of the content as a BCP 47 tag, default `"de"` |
| `topic` | no | e.g. "Wasser", for filtering |
| `description` | no | short text for parents |
| `durationMinutes` | no | estimated duration |
| `source` | no | e.g. "notebook entry of 20 Oct" |
| `shuffle` | no | default `true`: options, rows and statements are shuffled every round |
| `exercises` | yes | 1–50 exercises, in the order of the real test |
| `$schema` | no | path to the schema, so editors (VS Code, Rider) offer completion |

## Fields of every exercise

| Field | Required | Meaning |
| --- | --- | --- |
| `id` | yes | unique within the quiz |
| `type` | yes | `cloze`, `matching`, `ordering`, `trueFalse`, `freeText`, `multipleChoice` |
| `instruction` | yes | the task as written in the test |
| `hint` | no | small usage hint below the instruction |
| `points` | no | maximum points of the exercise (see grading) |
| `explanation` | no | shown after grading, e.g. a mnemonic |
| `solutionText` | no | text for "show solutions"; without it the app builds one |
| `image` | no | a picture the whole exercise refers to, e.g. a map or a diagram (see [Pictures](#pictures)) |

## Exercise types

### `cloze`: fill in the gaps

Every `___` (three underscores) in `text` is a gap. `gaps[i]` belongs to the i-th gap and lists every spelling that counts as correct. With a `wordBank` the gaps become drop-downs, otherwise text inputs.

```json
{
  "id": "cycle", "type": "cloze",
  "instruction": "Setze ein.",
  "wordBank": ["verdunstet", "Wolken", "Niederschlag"],
  "text": "Die Sonne erwärmt das Wasser, es ___. Der Dampf bildet ___. Daraus fällt ___.",
  "gaps": [
    { "answers": ["verdunstet"] },
    { "answers": ["Wolken"] },
    { "answers": ["Niederschlag", "Regen"] }
  ]
}
```

### `matching`: match each row to an option

Every row gets one of the `options` via a drop-down. An option may fit several rows. `answer` must equal one option exactly.

```json
{
  "id": "flour", "type": "matching",
  "instruction": "Weißmehl oder Vollkornmehl?",
  "options": ["Weißmehl", "Vollkornmehl"],
  "rows": [
    { "text": "Nur der helle Mehlkörper wird gemahlen.", "answer": "Weißmehl" },
    { "text": "Das ganze Korn wird gemahlen.", "answer": "Vollkornmehl" }
  ]
}
```

Each row may have an `image`, e.g. a plant to name.

### `ordering`: put steps in order

List the `steps` in the **correct** order (2–12). The app shuffles them, and the child taps them in order. A step counts as correct when it is in its position.

```json
{ "id": "bread", "type": "ordering", "instruction": "Bringe in die richtige Reihenfolge.",
  "steps": ["Säen", "Ernten", "Mahlen", "Backen"] }
```

### `trueFalse`: judge statements

```json
{ "id": "tf", "type": "trueFalse", "instruction": "Richtig oder falsch?",
  "statements": [
    { "text": "Getreide wird im Frühling geerntet.", "isTrue": false, "explanation": "Geerntet wird im Sommer." }
  ] }
```

Each statement may have an `image`.

### `freeText`: type the answers

One text input per entry in `fields`. Case and surrounding whitespace are ignored; list other spellings (e.g. endings) as further `answers`. With `anyOrder: true` every correct answer may be in any field, but each counts only once.

```json
{ "id": "principles", "type": "freeText", "instruction": "Schreibe die 5 Wahlgrundsätze auf.",
  "anyOrder": true,
  "fields": [ { "answers": ["frei", "freie"] }, { "answers": ["geheim", "geheime"] } ] }
```

Each field may have a `label`, e.g. `"Hauptstadt von Bayern:"`.

### `multipleChoice`

`correct` is the index of the correct option (from 0), or a list of indices when several are correct. 2–6 options per question.

```json
{ "id": "democracy", "type": "multipleChoice", "instruction": "Kreuze an.",
  "questions": [
    { "text": "Wählen darf man ab …", "options": ["10 Jahren.", "18 Jahren."], "correct": 1 }
  ] }
```

Each question may have an `image`. An option is either a text or a **picture answer**: an object with an `image` and an optional `text` under it. Text and picture options can be mixed; the solution names a picture option by its `text`, or else by its `alt`.

```json
{ "text": "Welche Form ist ein Dreieck?",
  "options": [
    { "image": { "src": "shapes/circle.svg", "alt": "Ein Kreis" } },
    { "image": { "src": "shapes/triangle.svg", "alt": "Ein Dreieck" }, "text": "Dreieck" }
  ],
  "correct": 1 }
```

## Pictures

[`quizzes/other/showcase-pictures.json`](../quizzes/other/showcase-pictures.json) uses a picture in every place the format allows: on an exercise, on a `matching` row, on a `trueFalse` statement, on a `multipleChoice` question and as picture answers. Its pictures are in the folder next to it.

Pictures are files uploaded **together with the JSON**: the JSON names each one in an `image` object, and the parent uploads the JSON with its pictures, either as a `.zip` or by picking all files at once. Without pictures, a file stays exactly as before.

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

## Grading

- Every gap, row, step, statement, field and question is a **part**. Without `points`, every correct part gives 1 point.
- With `points`: points reached = round(correct parts ÷ all parts × `points`), rounding half away from zero. Example: 7 steps, `points: 4`, 5 correct → 2.86 → 3 points.
- For `multipleChoice` with several correct options, a question counts only when exactly the correct options are selected.
- The mark is only a rough guide on the German 1–6 scale: from 95 % → 1, 83 % → 2, 67 % → 3, 50 % → 4, 30 % → 5, below → 6.
- An adult learner (a profile marked as adult) gets no mark, only points and percent.

## Content rules (checked by the app and `tools/validate_quizzes.py`)

- The number of `___` in `text` equals the number of entries in `gaps`.
- With a `wordBank`: every gap has at least one answer from the word bank.
- `matching`: every `answer` is one of the `options`.
- `multipleChoice`: every index in `correct` points to an existing option.
- Exercise `id`s are unique within the quiz.

## Notes for authors (and for Claude)

- Write the content in the child's language and at their level (for Calvin: German, 4th grade).
- Create the exercises in the order and with the points of the real test, so the practice feels like the test.
- Give wrong answers an `explanation` where possible; it shows right after grading.
- Use pictures where the real test has them (maps, diagrams, photos of plants), and always write an `alt` that names what matters for the task.
