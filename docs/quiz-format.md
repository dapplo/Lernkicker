# Quiz format `quiz/v1`

A practice quiz is **one JSON file** (UTF-8). Parents upload it in the parents' area, as a file or by pasting, or send it through the API. On upload, Lernkicker checks the file against the schema [`schema/quiz-v1.schema.json`](../schema/quiz-v1.schema.json) and the content rules below. A real test is [`quizzes/hsu/grain-and-elections.json`](../quizzes/hsu/grain-and-elections.json); [`quizzes/other/showcase-all-exercise-types.json`](../quizzes/other/showcase-all-exercise-types.json) uses every exercise type and every optional field once, to try them out or copy from.

The format's keys are English. The **content** (titles, questions, answers) is in whatever language the child learns in, usually German; declare it with `language`.

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
| `gradeLevel` | yes | school year, 1–13 |
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

## Grading

- Every gap, row, step, statement, field and question is a **part**. Without `points`, every correct part gives 1 point.
- With `points`: points reached = round(correct parts ÷ all parts × `points`), rounding half away from zero. Example: 7 steps, `points: 4`, 5 correct → 2.86 → 3 points.
- For `multipleChoice` with several correct options, a question counts only when exactly the correct options are selected.
- The mark is only a rough guide on the German 1–6 scale: from 95 % → 1, 83 % → 2, 67 % → 3, 50 % → 4, 30 % → 5, below → 6.

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
- Images are not part of v1; they come in a later version as a ZIP upload.
