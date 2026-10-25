# Operations: Synology with Tailscale

Lernkicker runs as two containers from [`deploy/compose.yaml`](../deploy/compose.yaml):

- **tailscale** joins your tailnet as the node `lernkicker` with the tag `tag:lernkicker`, gets an HTTPS certificate for `lernkicker.<tailnet>.ts.net` and forwards port 443 to the app (`tailscale serve`, optionally `funnel`).
- **lernkicker** is the app image `ghcr.io/dapplo/lernkicker` (amd64 and arm64). It shares the Tailscale container's network and publishes no ports, so it is not reachable from your LAN or the internet except through Tailscale.

The app container runs as uid 1654 with a read-only file system; it only writes to `/data` (SQLite database and cookie keys).

## What you need

- A Synology with **DSM 7.2 or newer** and the **Container Manager** package (Package Center). The image exists for x86-64 (Intel/AMD, e.g. DS920+, DS923+) and arm64 models; 32-bit ARM models (j series, older value series) cannot run it.
- About 200 MB of disk space and 150 MB of RAM.
- A **Tailscale** account (the free plan is enough). The NAS itself does not need the Tailscale package: the sidecar container brings its own.
- Nothing else: no open ports on the router, no own domain, no certificate. Tailscale provides `https://lernkicker.<tailnet>.ts.net` with a valid certificate.
- Everyone who uses the app (you, Calvin's phone) needs the Tailscale app and access to the node, unless you turn on Funnel later (step 5).

## 1. Tailnet policy (once)

In the [Tailscale admin console](https://login.tailscale.com/admin/acls) → Access controls, make sure that:

- HTTPS certificates and MagicDNS are on (DNS page).
- The tag exists and may use Funnel, and the tag itself may not reach anything:

```jsonc
"tagOwners": {
  "tag:lernkicker": ["autogroup:admin"],
},
"nodeAttrs": [
  { "target": ["tag:lernkicker"], "attr": ["funnel"] },
],
"grants": [
  // Your own devices may open Lernkicker ...
  { "src": ["autogroup:member"], "dst": ["tag:lernkicker"], "ip": ["443"] },
  // ... and there is deliberately no grant with "src": ["tag:lernkicker"]
],
```

If your policy still has the default "allow everything" rule, the tag could reach your other devices; replace that rule with explicit grants.

Then create an **auth key** (Settings → Keys): reusable off, ephemeral off, tag `tag:lernkicker`.

## 2. Files on the NAS

Create a folder, e.g. `/volume1/docker/lernkicker`, with

- `compose.yaml` and `serve.json` from [`deploy/`](../deploy/),
- `.env`, copied from [`deploy/.env.example`](../deploy/.env.example) and filled in (the Tailscale auth key).

That is all: the data lives in the Docker volume `lernkicker-data`, which Docker creates on the first start, already writable for the app (no folder, no `chown`). The `tailscale` folder for the node state is created automatically too.

## 3. Start

Container Manager → Project → Create: name `lernkicker`, path `/volume1/docker/lernkicker`, "Use existing docker-compose.yml". Start it.

After a minute the node `lernkicker` shows up in the Tailscale console. Open `https://lernkicker.<tailnet>.ts.net` on a device in your tailnet. A fresh Lernkicker shows the **setup page**: enter your name, your family's name, email and password. You become the admin, and the address you used is kept for links in QR codes, invitations and the feed. The setup page only works from inside the tailnet (never through Funnel) and only until the first account exists.

Then remove `TS_AUTHKEY` from `.env`: the node state is stored and the key is not needed any more.

For an unattended setup (scripts, tests), `Admin__Email` and `Admin__Password` (plus `Admin__Name`, `Admin__Family`, `Admin__Language`) create the admin at start instead.

## Option B: an existing TSDProxy

If the NAS already runs [TSDProxy](https://almeidapaulopt.github.io/tsdproxy/), use [`deploy/compose.tsdproxy.yaml`](../deploy/compose.tsdproxy.yaml) instead of `compose.yaml` (no `serve.json`, no `tailscale` folder, no `TS_AUTHKEY`):

1. Create the project from it (paste it or copy it as `compose.yaml` into the project folder). A `.env` is only needed if 8080 is taken on the NAS (`LERNKICKER_PORT`).
2. Start the project. TSDProxy sees the `tsdproxy.*` labels and creates the node `lernkicker`; it shows up in its dashboard and in the Tailscale console.
3. Open `https://lernkicker.<tailnet>.ts.net` and fill in the setup page (step 3). Open it through that address, not the NAS port: the address is kept for links.

Differences to the sidecar:

- The node gets TSDProxy's tags or none: `tsdproxy.tags` only works when TSDProxy logs in with OAuth. Give the node the restricted tag from step 1 that way, or check in the Tailscale console that it cannot reach your other devices.
- The app publishes a port on the NAS, so it is reachable over plain HTTP in the LAN. Logging in only works over HTTPS, so this gives nobody access, but a NAS firewall rule (Control Panel → Security → Firewall) can limit the port to Docker's networks (`172.16.0.0/12`).
- `Lernkicker__TrustedProxies` (default `172.16.0.0/12`) lets the app see the real client address behind TSDProxy, for the rate limits. If TSDProxy runs in `network_mode: host`, the requests come from the NAS's own address; set it to that address.
- Funnel: set the label `tsdproxy.funnel` to `"true"` (read the [security concept](security.md) first). TSDProxy does not pass Tailscale's `Tailscale-Funnel-Request` header, so with TSDProxy and Funnel the admin pages, the backup download and the setup page of a fresh server are reachable from the internet too, protected by the admin login only. Do the setup before turning Funnel on.

## 4. Invite families

As admin, open `/admin` (only works inside the tailnet) and create an invitation link for a new family or another parent. Send the link yourself; it is valid for 7 days and only once.

Other families need to reach the app: either share the node with them in Tailscale, or turn on Funnel.

## 5. Funnel (public access)

Read the [security concept](security.md) first. To make the app public, set `AllowFunnel` in `serve.json` to `true` and restart the project. Turning it back to `false` (and restarting) makes the app private again at once. The admin API stays tailnet-only either way.

## 6. Telegram (optional)

The bot sends parents their children's results after each quiz and, at 18:00, the dates of the next day (tests, due dates, football). It uses long polling, so Telegram never needs to reach the NAS.

1. In Telegram, talk to [@BotFather](https://t.me/BotFather): `/newbot`, pick a name and a username (e.g. `LernkickerKromBot`). It answers with the token.
2. Still in BotFather: `/setjoingroups` → your bot → **Disable**, so nobody can add it to a group. The bot only answers private chats anyway.
3. As admin, open `/admin` → **Telegram-Bot**, paste the token and save. The app checks it with Telegram, stores it encrypted (with the same keys as the cookies, in `data/keys`) and starts the bot at once. The token is never shown again.
4. Each parent opens **Eltern → Telegram → Mit Telegram verbinden** and presses *Start* in the bot. The link works once and for 10 minutes. `/next` shows the next 7 days, `/stop` disconnects.

"Token ersetzen" switches to another bot, "Bot stoppen" turns Telegram off; both take effect without a restart. If the token leaks, `/revoke` in BotFather makes it useless; paste the new one.

To change the hour of the reminder, set `Lernkicker__Telegram__ReminderHour` (0–23, default 18).

**Token as a file instead:** if you prefer to keep the token out of the database, mount a file readable by uid 1654 (e.g. `./secrets:/run/secrets:ro`) and set `Lernkicker__Telegram__BotTokenFile: /run/secrets/telegram-bot-token`. A token from the configuration wins; the admin page then only shows the bot's status.

## Updates

Container Manager → Project → `lernkicker` → Action → Build (pulls `latest`), or pin `LERNKICKER_TAG` to a version and change it. The database is migrated on start.

## Backup

As admin, open `/admin` → **Sicherung herunterladen**. The zip holds everything: the database (all families, children, quizzes, results), the Data Protection keys (logins, the encrypted Telegram token). Keep it somewhere safe, e.g. in an encrypted Hyper Backup task of the folder you save it to: whoever has the file can read all the data. (Hyper Backup cannot see Docker volumes directly, hence the download.)

**Restore or move:** start a new, empty Lernkicker (a new project, or delete the volume `lernkicker-data` of the old one), open it, and on the setup page choose **Mit einer Sicherung starten**. The app checks the file, restarts once (the restart policy brings it back) and is then exactly where the backup was. The data it replaced is kept in the volume as `before-restore-<time>`. Restoring only works on a server without an account, from inside the tailnet.

**With a folder instead of the volume:** replace `lernkicker-data:/data` by `./data:/data` and give the folder to the app user once (`chown -R 1654:1654 data`, e.g. as a root task in Task Scheduler). Then Hyper Backup can back up the folder directly.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| App container restarts, log says "unable to open database file" or access denied in `/data` | With a folder instead of the volume: it does not belong to uid 1654 (see Backup) |
| Node does not appear in Tailscale | Auth key expired or not tagged; check the tailscale container's log |
| `https://lernkicker…` has a certificate error | HTTPS certificates not enabled in the tailnet |
| `/admin` says it only works in the tailnet | You came through Funnel; use a device in your tailnet |
| Too many attempts | Rate limit per IP; wait a minute (configurable, see README) |
