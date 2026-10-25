# Operations: Synology with Tailscale

Lernkicker runs as two containers from [`deploy/compose.yaml`](../deploy/compose.yaml):

- **tailscale** joins your tailnet as the node `lernkicker` with the tag `tag:lernkicker`, gets an HTTPS certificate for `lernkicker.<tailnet>.ts.net` and forwards port 443 to the app (`tailscale serve`, optionally `funnel`).
- **lernkicker** is the app image `ghcr.io/dapplo/lernkicker` (amd64 and arm64). It shares the Tailscale container's network and publishes no ports, so it is not reachable from your LAN or the internet except through Tailscale.

The app container runs as uid 1654 with a read-only file system; it only writes to `/data` (SQLite database and cookie keys).

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
- `.env`, copied from [`deploy/.env.example`](../deploy/.env.example) and filled in (auth key, admin email and password),
- two empty folders `data` and `tailscale`.

`data` must belong to the app user. DSM has no UI for numeric owners, so run this once as root: Control Panel → Task Scheduler → Create → Scheduled task → User-defined script, user `root`, script:

```bash
chown -R 1654:1654 /volume1/docker/lernkicker/data
```

Run it once ("Run") and delete the task afterwards. (Over SSH: `sudo chown -R 1654:1654 …`.)

## 3. Start

Container Manager → Project → Create: name `lernkicker`, path `/volume1/docker/lernkicker`, "Use existing docker-compose.yml". Start it.

After a minute the node `lernkicker` shows up in the Tailscale console. Open `https://lernkicker.<tailnet>.ts.net` on a device in your tailnet, go to **I am a parent / Ich bin ein Elternteil**, and log in with the admin email and password from `.env`.

Then remove `ADMIN_PASSWORD` (and `TS_AUTHKEY`) from `.env`: the account and the Tailscale node state are stored, and the values are not needed any more.

## 4. Invite families

As admin, open `/admin` (only works inside the tailnet) and create an invitation link for a new family or another parent. Send the link yourself; it is valid for 7 days and only once.

Other families need to reach the app: either share the node with them in Tailscale, or turn on Funnel.

## 5. Funnel (public access)

Read the [security concept](security.md) first. To make the app public, set `AllowFunnel` in `serve.json` to `true` and restart the project. Turning it back to `false` (and restarting) makes the app private again at once. The admin API stays tailnet-only either way.

## Updates

Container Manager → Project → `lernkicker` → Action → Build (pulls `latest`), or pin `LERNKICKER_TAG` to a version and change it. The database is migrated on start.

## Backup

Back up `/volume1/docker/lernkicker/data` with Hyper Backup (encrypted). It holds everything: database and the cookie keys. Without the keys, everybody just has to log in again.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| App container restarts, log says "unable to open database file" or access denied in `/data` | `data` does not belong to uid 1654 (step 2) |
| Node does not appear in Tailscale | Auth key expired or not tagged; check the tailscale container's log |
| `https://lernkicker…` has a certificate error | HTTPS certificates not enabled in the tailnet |
| `/admin` says it only works in the tailnet | You came through Funnel; use a device in your tailnet |
| Too many attempts | Rate limit per IP; wait a minute (configurable, see README) |
