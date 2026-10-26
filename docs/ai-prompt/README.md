# Prompt for AI assistants

Ready-to-paste prompts that let a parent have an AI (Claude, ChatGPT, Gemini, …) write a quiz (`quiz/v1`) or learning material (`lesson/v1`) for Lernkicker, with pictures as a zip.

- [`en.md`](en.md): English
- [`de.md`](de.md): German

The app serves them in the parents' area (**Create with an AI**, on the quiz and learning material upload pages) with both schemas appended, so one download is everything the AI needs:

| URL | Content |
| --- | --- |
| `/api/format/ai-prompt?lang=de` | the prompt in `de` or `en` (default), followed by both schemas |
| `/api/format/quiz-v1.schema.json` | [`schema/quiz-v1.schema.json`](../../schema/quiz-v1.schema.json) |
| `/api/format/lesson-v1.schema.json` | [`schema/lesson-v1.schema.json`](../../schema/lesson-v1.schema.json) |

They need no login: they hold no family data. The schemas are also public on GitHub, e.g. `https://raw.githubusercontent.com/dapplo/lernkicker/main/schema/quiz-v1.schema.json`.

Keep both languages in step, and keep the rules and examples in line with [`quiz-format.md`](../quiz-format.md) and [`lesson-format.md`](../lesson-format.md). `AiPromptTests` checks that both prompts load and that their examples are valid quizzes and lessons.
