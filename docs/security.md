# Security concept

As of 2026-10-25. Lernkicker is public through **Tailscale Funnel**, e.g. at `https://lernkicker.<tailnet>.ts.net`, and stores data about children from several families. So: the address is no secret, every access needs a login, and each family only sees its own data.

Items marked *(planned)* are not built yet.

## Situation and threats

- **The address is public.** Every HTTPS certificate for `*.ts.net` is in the public Certificate Transparency logs; scanners find the name within minutes. Security must never depend on nobody knowing the address.
- **Attackers from the internet:** guessing passwords or PINs, exploiting known bugs, using the NAS as a way into the home network.
- **Curious users:** a parent trying to see other children's results.
- **Children:** a child trying to reach the parent view or the solutions (harmless, but worth considering).
- **Data protection:** data about other people's children; the operator (Robin) is responsible for it (GDPR).

## 1. Network and operations

| Measure | Implementation |
| --- | --- |
| Only the app is public | Funnel forwards only port 443 to the app container. DSM, SSH and other NAS services stay invisible. |
| Isolate the container | Its own Tailscale node with `tag:lernkicker`; the tailnet policy gives this tag **no** access to other devices. The app container publishes no ports and shares the Tailscale container's network, so it is not reachable from the LAN. Docker does not block *outgoing* connections to the home network; the app only makes them to the internet (calendar import; later Telegram) and refuses private addresses itself (section 6). A router or NAS firewall rule can block the home network completely. |
| Harden the container | Runs as a non-root user (chiseled .NET image, uid 1654), read-only file system (`read_only: true`), the only writable folder is `/data` (plus a `/tmp` tmpfs), `cap_drop: [ALL]`, `no-new-privileges`. |
| TLS | Tailscale terminates TLS in its own container. The app sends HSTS. |
| Real client IP | For rate limits the app reads `X-Forwarded-For` only from the local Tailscale proxy (ASP.NET Core trusts loopback by default). |
| Kill switch | `tailscale funnel off` in the sidecar (or removing `AllowFunnel` from the serve config) makes the app private again at once; it stays reachable inside the tailnet. |
| Updates | Dependabot (`.github/dependabot.yml`) proposes NuGet, base image and Actions updates weekly; pull the new image in Container Manager. |
| Backup | Hyper Backup encrypts the `data/` folder (SQLite database, data protection keys). |

## 2. Login

**No open registration.** Accounts only come from invitations.

| Who | How | Protection |
| --- | --- | --- |
| Admin | Email + password; passkey or TOTP 2FA *(planned)* | The admin API only answers inside the tailnet: requests through Funnel carry the `Tailscale-Funnel-Request` header, and `TailnetOnlyFilter` answers them with 404. |
| Parent | Invitation link (single use, valid 7 days), then email + password (at least 12 characters; check against leaked passwords *(planned)*) | Locked for 15 minutes after 5 failed attempts, rate limit per IP |
| Child | A parent shows a QR code; scanning it **pairs** the child's phone (single-use code, valid 10 minutes). Then a 4-digit PIN. | A PIN is useless without a paired device. After 5 wrong PINs the device is locked until a parent unlocks it. Parents see paired devices and can remove them; a removed or locked device ends the child's session at once. |

Technology: ASP.NET Core Identity. The device token is 256 random bits and stored only as a hash; invitation and pairing codes are stored as hashes too. Cookies are `Secure`, `HttpOnly`, `SameSite=Strict`, so another site cannot make the browser send them (this is the protection against cross-site requests; the API has no forms). The device cookie is limited to `/api/device`. Data protection keys live in `/data/keys`, so logins survive restarts and image updates.

**Forgotten password without email** *(planned)*: the admin creates a single-use reset link (valid 1 hour) for the parent and sends it the way the invitation went.

## 3. Permissions and family isolation

- Every table with family data has a `FamilyId`. A global EF Core query filter limits every query to the logged-in user's family; the use cases also check that every id belongs to that family.
- Every endpoint requires a policy (admin, parent, child). Child endpoints **never** return solutions: the PWA sends the answers, the server grades them and only then returns solutions and explanations.
- Ids exposed to the outside are random GUIDs, never sequential numbers.
- Integration tests (`FamilyIsolationTests`) check that family A cannot read or change family B's data. New endpoints need such a test.
- The admin manages families but sees no results of other families' children.
- Shared quizzes (class group) *(planned)* are copied into the other family. Results are never shared.

## 4. Input and uploads

- Quizzes: JSON only, at most 256 KB, validated against `schema/quiz-v1.schema.json` and the content rules (`QuizRules`). Invalid files are rejected with a list of issues.
- All quiz text is rendered as text, never as HTML (Blazor encodes by default; no `MarkupString` for quiz content). A quiz cannot bring script into someone else's browser, even when shared.
- Content Security Policy: `default-src 'self'`, scripts only from the app itself plus `'wasm-unsafe-eval'` for WebAssembly, no inline scripts or styles, no CDNs, no trackers, `frame-ancestors 'none'`.
- The server only returns error codes, never text built from input.
- Images (later, as ZIP) *(planned)*: size limit, PNG/JPEG/WebP only, re-encoded and stored without metadata.
- API tokens for uploads (e.g. for Claude or scripts) *(planned)*: per parent, only "upload quizzes", stored as a hash, revocable.

## 5. Telegram *(planned)*

- The bot only answers linked chat ids; everything else is ignored and not stored.
- Link codes are single use, valid 10 minutes and only created from the logged-in parent view.
- A child can only be linked when a parent has turned on "Telegram allowed"; turning it off unlinks at once.
- In BotFather: disable joining groups (`/setjoingroups` → Disable).
- The bot token is a Docker secret, never in the repository or image.
- Long polling: Telegram never needs to reach the app.

## 6. Calendar

**Feed for the parents' calendar app**

- Each child has at most one secret ICS address with a 256-bit random token; only its SHA-256 hash is stored, so the link is shown once. Calendar apps fetch it without a login. A new link replaces the old one, and the parent can turn it off.
- The feed only contains the child's first name (in the calendar name), titles, dates, times and places: no results, no PINs, no other children.
- It is rate limited per IP (`Lernkicker:Limits:CalendarRequestsPerMinute`) and an unknown token is a plain 404.

**Importing subscribed calendars (SSRF)**

A parent can paste any URL, and the server fetches it. That must not become a way into the home network or the tailnet:

- Only `http`, `https` and `webcal` (read as `https`) URLs, no user name or password in the URL, at most 2000 characters.
- Every connection, including each of at most 3 redirects, resolves the host and refuses loopback, private (10/8, 172.16/12, 192.168/16), carrier-grade NAT and Tailscale (100.64/10, fd7a:115c:a1e0::/48 via fc00::/7), link-local and cloud metadata (169.254/16, fe80::/10), multicast, reserved and NAT64 addresses, also as IPv4-mapped IPv6. The check runs in the socket connect callback, on the address actually used, so DNS rebinding does not get around it. No proxy is used.
- At most 2 MB, 15 seconds, and the answer must be an ICS calendar; at most 1000 imported dates, 5 subscriptions per child, and the import endpoints are rate limited.
- Parents only see the host of a subscription and an error code, never the response body.
- Imported text is shown as text, never as HTML, and clipped to the field lengths. Imported dates are read-only.

## 7. Rate limits and monitoring

- ASP.NET Core rate limiter per IP for parent logins and invitations (`Lernkicker:Limits:LoginsPerMinute`) and for pairing and PIN login on the child's device (`Lernkicker:Limits:ChildLoginsPerMinute`). Calendar imports and the ICS feed: `Lernkicker:Limits:CalendarRequestsPerMinute`. Limits for uploads *(planned)*.
- Failed logins and lockouts are logged, without passwords, PINs or children's answers.
- Alerts to the admin via Telegram on suspicious patterns *(planned)*, e.g. more than 20 failures in 10 minutes or a device lock.

## 8. Data protection

- **As little data as possible:** for children only a first name or nickname and the grade, no date of birth, no photo. For parents an email address (as login name), a display name and the UI language.
- **Consent:** when redeeming the invitation the parent accepts a short privacy notice (`/privacy`: who is responsible, what is stored, where, how to delete it).
- **Deletion:** parents can delete a child with all results; the admin can delete a whole family. Deletion removes everything from the database at once; backups expire after their retention time. Export as JSON *(planned)*.
- **Retention** *(planned)*: results older than two school years are deleted automatically.
- No third parties: no analytics, no external fonts. Telegram only for families who turn it on.
- A written privacy notice is advisable; this concept is not legal advice.

## Email

**Email is not needed to start.** The app creates invitation (and later reset) links, and the admin sends them personally (messenger, in person). Notifications will go through Telegram.

If the app should send email later (invitations, "forgot password", a weekly report for parents without Telegram), it needs an **SMTP account**:

| Option | What is needed | Notes |
| --- | --- | --- |
| Existing mailbox (e.g. Gmail, GMX, mailbox.org) | SMTP server, user, app password | Quickest. The sender is that mailbox. Mind daily limits. |
| Delivery service (e.g. Brevo, Mailjet, Postmark) | Account, API key or SMTP credentials, own domain with SPF and DKIM records | Most reliable delivery; the small free tiers are enough here. |
| Own mail server on the NAS | – | Not recommended: mail from home connections often ends up as spam. |

In the app: sending with MailKit, credentials as a Docker secret, sender e.g. `lernkicker@<your-domain>`. Without SMTP configured, the app shows the links for copying instead.
