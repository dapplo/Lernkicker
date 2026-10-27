# AI assistants (MCP)

Lernkicker has an [MCP](https://modelcontextprotocol.io) server, so an AI assistant such as Claude can work for a family on its own: let a parent learn or add a child, write a quiz or a lesson, check and upload it, put it on a child's todo list with a due date, add the test to the calendar, and look at the results afterwards to see what to practise next. Nobody has to click anything in between.

The endpoint is `https://<your-lernkicker>/mcp` (Streamable HTTP, stateless). It is built with the official C# SDK ([`ModelContextProtocol.AspNetCore`](https://github.com/modelcontextprotocol/csharp-sdk)) as another driving adapter next to the HTTP API (`src/Lernkicker.Web/Mcp`); the tools call the same use cases, so the same rules, checks and family isolation apply.

## 1. Create an API key

Each family creates its own keys: **Eltern → Konto → KI-Assistenten (MCP) → Schlüssel erstellen** (in English: *Parents → Account → AI assistants (MCP) → Create key*).

- Give the key a name (e.g. "Claude"), so you know later what it is for.
- **Read only** keys can look at everything (children, quizzes, lessons, todo lists, results, calendar) but change nothing; the tools that change things are not even listed for them.
- The key (`lk_…`) is shown **once**, together with the MCP address and a ready-made command for Claude Code. Copy it right away. Lernkicker only keeps a hash of it.
- The list shows when each key was last used. **Revoke** stops a key at once. A family can have up to 10 keys; deleting the parent who created a key removes the key too.

A key acts for the whole family, like a parent would, but only on `/mcp`: it does not open the parents' page or the HTTP API, and it can't invite parents, set PINs, pair phones, share with other families, subscribe calendars or touch Telegram. It can add children and let parents learn, but PINs and pairing phones stay with the parents in the app.

## 2. Connect an assistant

The key goes into the `Authorization` header: `Authorization: Bearer lk_…` (`X-Api-Key: lk_…` works too).

**Where the assistant runs matters.** A program inside your tailnet (Claude Code or Claude Desktop on a laptop that runs Tailscale) reaches Lernkicker at its tailnet address. An assistant that runs in the cloud (e.g. Claude Code on the web, a scheduled agent) needs Lernkicker to be public through Funnel (see [Operations](operations.md), section 5) and, if its environment restricts the network, your Lernkicker host on its list of allowed domains.

### Claude Code

```bash
claude mcp add --transport http lernkicker https://lernkicker.<tailnet>.ts.net/mcp \
  --header "Authorization: Bearer lk_…"
```

Or for a project, in `.mcp.json` next to the code, with the key from an environment variable so it never lands in git:

```json
{
  "mcpServers": {
    "lernkicker": {
      "type": "http",
      "url": "https://lernkicker.<tailnet>.ts.net/mcp",
      "headers": { "Authorization": "Bearer ${LERNKICKER_API_KEY}" }
    }
  }
}
```

In a cloud environment, set `LERNKICKER_API_KEY` as a secret environment variable of that environment.

### Claude Desktop

Claude Desktop starts local MCP servers, so it reaches a remote server through the `mcp-remote` bridge (needs Node.js). In *Settings → Developer → Edit Config* (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "lernkicker": {
      "command": "npx",
      "args": ["-y", "mcp-remote", "https://lernkicker.<tailnet>.ts.net/mcp", "--header", "Authorization:${LERNKICKER_AUTH}"],
      "env": { "LERNKICKER_AUTH": "Bearer lk_…" }
    }
  }
}
```

(The space in `Bearer lk_…` is in the environment variable on purpose: some systems split arguments with spaces.)

### Other clients and a quick check

Any MCP client that speaks Streamable HTTP and can send a header works. To see that the key works without a client:

```bash
curl -s https://lernkicker.<tailnet>.ts.net/mcp \
  -H "Authorization: Bearer lk_…" \
  -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

A wrong or revoked key gets `401`.

Connectors added in the claude.ai web or mobile app need OAuth, which Lernkicker does not offer; use Claude Code or Claude Desktop as above.

## 3. Tools

| Tool | Changes data | What it does |
| --- | --- | --- |
| `list_family_members` | | The parents (`parentId`, and `learnerId` once they learn themselves), children (with school year) and other adult learners. The `learnerId` is the id for assignments, results and the calendar (the `childId` argument of those tools) |
| `enable_learning` | yes | Lets a parent learn too (e.g. one who learns Dutch): gives them their own learner profile, like *Selbst lernen* in the app. They learn with their own parent login; nothing else to set up |
| `add_child` | yes | Adds a child with school year and look (`football`, `space`, `unicorn`). The PIN and the child's phone are set up by a parent in the app |
| `update_family_member` | yes | Changes a learner's name, school year or look; omitted values stay |
| `remove_family_member` | yes | Removes a learner profile with its assignments, results and calendar. For a parent it only ends their learning; the login stays |
| `get_authoring_guide` | | The rules for writing quizzes and lessons (`docs/ai-prompt`, `en` or `de`) followed by both JSON schemas |
| `list_quizzes`, `list_lessons` | | The family's quizzes and lessons |
| `get_quiz`, `get_lesson` | | The JSON file exactly as uploaded, optionally with its pictures (base64) |
| `validate_quiz`, `validate_lesson` | | Checks a file and its pictures like an upload, without storing it |
| `upload_quiz`, `upload_lesson` | yes | Stores a file (with pictures as `[{ "name": "<src>", "base64": "…" }]`). The same id replaces it: a quiz gets a new version and open assignments move along; a lesson keeps its assignments and progress |
| `delete_quiz`, `delete_lesson` | yes | Removes it with its assignments (a quiz with results is archived instead) |
| `list_assignments` | | A child's todo list: quizzes and lessons with due dates, lessons with progress |
| `assign_quiz`, `assign_lesson` | yes | Puts it on a child's list with an optional due date (`yyyy-MM-dd`), which also shows in the child's calendar and in the parents' Telegram reminder. Assigning again changes the date |
| `unassign_quiz`, `unassign_lesson` | yes | Takes it off the list (by assignment id) |
| `list_results`, `get_result` | | Handed-in quizzes with points, percent and mark; one of them in detail with answers and solutions |
| `get_calendar` | | A child's dates: events, imported dates (e.g. football) and due dates |
| `add_calendar_event`, `update_calendar_event`, `delete_calendar_event` | yes | A date such as the day of a test (`kind`: `test`, `football` or `other`; times in the family's time zone). Imported dates are read-only |

Results are JSON in the same shape as the HTTP API (camelCase). A failure is a tool error whose text holds the API's error body, e.g.

```json
{"code":"quiz.invalid","issues":[{"code":"schema","path":"/exercises/2","args":{"keyword":"required","message":"…"}}]}
```

so the assistant can fix the file and try again. The codes are the ones in `ErrorCodes` and `QuizIssueCodes`; [`docs/quiz-format.md`](quiz-format.md) and [`docs/lesson-format.md`](lesson-format.md) explain the content rules behind them.

A typical run: `get_authoring_guide` → write the file → `validate_quiz` until it passes → `upload_quiz` → `list_family_members` → `assign_quiz` with the test date → `add_calendar_event` for the test itself.

## 4. Settings

| Setting | Default | Meaning |
| --- | --- | --- |
| `Lernkicker:Mcp:Enabled` | `true` | `false` removes the `/mcp` endpoint (keys can still be managed, but do nothing) |
| `Lernkicker:Limits:ApiKeyRequestsPerMinute` | `120` | Requests to `/mcp` per key and minute |

## 5. Security

See the [security concept](security.md), section 2 (API keys). In short: keys are 256 random bits with the prefix `lk_` (so secret scanners can spot a leaked one), stored only as SHA-256 hash, scoped to one family, optionally read-only, rate limited per key, and revocable at once. The MCP endpoint only accepts API keys, never the parents' or children's cookies, so a web page can't make a browser call it. Every tool goes through the family query filter and the use cases' own checks; `McpTests` check that a key of one family neither sees nor changes another family's data.
