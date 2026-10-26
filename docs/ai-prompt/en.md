# Make a Lernkicker quiz or learning material with an AI

Copy this whole text into an AI chat (Claude, ChatGPT, Gemini, …), fill in the part under "My request" and add photos of the worksheets or notebook pages. The app adds the two JSON schemas at the end; if you copied this from GitHub, also attach `schema/quiz-v1.schema.json` and `schema/lesson-v1.schema.json`.

---

You create practice material for **Lernkicker**, a learning app for school children. Parents upload what you make in the parents' area of the app. There are two kinds of file:

- a **quiz** (`quiz/v1`): a practice test that the app grades, like the real test at school;
- **learning material** (`lesson/v1`): something to read and learn that is not graded: a text, a poem to learn by heart, flashcards, key facts, pictures, or a guide to look things up in (e.g. the rules of a game).

## My request

- What I want: [a quiz / learning material / both]
- Topic: [e.g. "The water cycle", or see the attached photos]
- School year and school type: [e.g. 4th grade, primary school in Bavaria, or "adult"]
- Curriculum or test: [e.g. "LehrplanPLUS", "the test is on 12 November", "use the order and points of the attached old test"]
- Language of the content: [e.g. German]
- Pictures: [none / draw simple ones yourself / I will add photos]

## How to write it

- Write all **content** (titles, instructions, questions, answers, explanations, `alt` texts) in the language above, at the child's level: short sentences, words the child knows from school. The JSON **keys** and enum values stay in English, exactly as in the schema.
- Set `language` to the content's language as a BCP 47 tag (`de`, `en`, …) and `gradeLevel` to the school year. For an adult learner, leave `gradeLevel` out and write at an adult level.
- Stick to the material: use what is in the photos or what the curriculum names for this year. Do not make up facts; if something is unclear, ask me before writing the file.
- For a quiz: number the instructions like a real test (`"1. Setze ein."`). If I attached a real test, keep its order and its points (`points` per exercise). Mix exercise types, and give wrong answers an `explanation` (e.g. a mnemonic). List every spelling that should count in `answers` (e.g. with and without an ending).
- For learning material: one topic per file, 1–30 blocks. Short `facts`, `flashcards` for terms, `text` for explanations, `poem` for anything to learn word for word. For a guide to look things up in (game rules, a glossary), set `"mode": "reference"` and use an `entries` block: one entry per card or term, with `name` and a short `text`.
- Games come for free: flashcards and entries unlock games in the app (catch the words, memory, crossword, word search) when there are at least 4 of them. Write the term on the front as **one word** where you can (an article like "die Ähre" is fine) and keep the back **short (up to 60 characters)**, e.g. "Weizen" → "Getreide für helles Brot und Nudeln".
- `id`: lower-case letters, digits and `-`, 3–64 characters, describing the topic (e.g. `hsu-water-cycle`). Exercise and block ids: short and unique within the file.

## Rules the schema does not check

Lernkicker refuses a file that breaks one of these:

- `cloze`: every `___` (three underscores) in `text` is a gap; the number of gaps equals the number of entries in `gaps`. With a `wordBank`, every gap has at least one answer from the word bank.
- `matching`: every row's `answer` is exactly one of the `options`.
- `multipleChoice`: `correct` is the index (from 0) of the correct option, or a list of indices; every index points to an existing option. 2–6 options.
- `ordering`: list the `steps` in the **correct** order; the app shuffles them.
- `wordSearch`: `words` are letters only (umlauts and ß are fine), no spaces or hyphens, 2–14 letters, each word once. The app builds a new grid every round; use `"level": "medium"` (diagonal, crossing words) or `"hard"` (also backwards) for older children; the default `easy` suits grades 1–3.
- `fallingWords`: every word's `answer` is exactly one of the `options`. Keep options short (a word or a few), because up to four sit side by side on a phone.
- Exercise ids are unique within a quiz, block ids unique within a lesson.
- A quiz has at most 50 exercises; a lesson file is at most 256 KB.

## Pictures

Pictures are optional. Use them where the real test has them (maps, diagrams, photos of plants) or where they help a young child.

- A picture is an `image` object: `{ "src": "water-cycle/clouds.svg", "alt": "…", "caption": "…", "credit": "…" }`. `src` and `alt` are required. `alt` says in the content language what matters in the picture for the task.
- `src` is a path **relative to the JSON file**, e.g. `water-cycle/clouds.svg`. No URLs, no leading `/`, no `..`. Use letters, digits, `-`, `_` and `.` in file names.
- Allowed: PNG, JPEG, GIF, WebP and SVG, at most 2 MB per picture and 20 MB per file together. The app removes scripts, links and anything else that is not drawing from an SVG.
- Where pictures can go in a quiz: on any exercise (`image`), on a `matching` row or a `fallingWords` word, a `trueFalse` statement, a `multipleChoice` question, and as picture answers (an option `{ "image": {…}, "text": "optional" }` instead of a text).
- Where pictures can go in a lesson: on `text` and `facts` blocks, on flashcards (`frontImage`, `backImage`), on `entries` and as an `image` block of its own.
- If you draw pictures yourself, draw simple, clear SVGs (shapes, diagrams, a map sketch) with a `viewBox`, no text that gives the answer away, and no external fonts or links.
- For pictures from the internet (e.g. Wikimedia Commons), fill in `credit` with the source and the licence, and only use pictures whose licence allows it.

## What to give me

- **Without pictures:** the JSON file, complete, in one code block (or as a file to download).
- **With pictures:** a **zip** with exactly one `.json` file and the pictures in a folder next to it, matching the `src` paths:

  ```
  hsu-water-cycle.zip
  ├── hsu-water-cycle.json
  └── water-cycle/
      ├── clouds.svg
      └── river.jpg
  ```

  If you cannot make a zip, give me the JSON and each SVG in its own code block, plus a list of the photos I have to add with the file name each needs.
- For both a quiz and learning material, make two separate files (two zips if they have pictures).
- Check the file against the schema below and the rules above before you give it to me.

## Small examples

A quiz:

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

Learning material:

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
