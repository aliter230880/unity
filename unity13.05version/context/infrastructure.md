# Infrastructure — Devin VM ↔ Unity Editor bridge

This is a fact-record of how the bridge between an AI agent (Devin, on a Linux
VM) and a running Unity Editor (on the user's Windows machine) was wired up
during the 2026-05-13 session. It exists so any future AI starting a fresh
session can re-create the same setup in a few minutes.

## Two physically separate machines

```
+----------------------------------+                +-----------------------------+
| User's Windows desktop           |                | AI agent VM (Linux)         |
| - Unity Editor 6000.3.9f1        |                | - Docker engine             |
| - Project: D:\Work\Unity\        |                | - cloudflared              |
|   Projects\tps1                  |                | - Python 3.12 + PyTorch CPU |
| - Plugin "AI Game Developer"     |                | - TripoSR @ ~/TripoSR       |
|   (com.ivan-murzak.unity-mcp)    |                | - mcp_call.sh helper        |
+----------------------------------+                +-----------------------------+
            ^                                                       |
            |                                                       |
            |    HTTPS + Bearer token via Cloudflare quick-tunnel   |
            +-------------------------------------------------------+
                            (random *.trycloudflare.com host)
```

The agent never touches the user's filesystem directly. All edits to the
project go through the plugin → MCP server → tools.

## Components, top to bottom

### 1. Unity plugin (AI Game Developer)

- Author: Ivan Murzak, MIT-licensed.
- Releases: https://github.com/IvanMurzak/Unity-MCP/releases
- Distribution: `.unitypackage` "AI-Game-Dev-Installer".
- Installs as `com.ivan-murzak.unity-mcp` plus add-ons
  (Animation, ParticleSystem, ProBuilder).
- Each Unity project gets its own plugin instance and **its own auto-generated
  Bearer token**. Tokens are project-scoped, not machine-scoped.
- Configuration is in `Window → AI Game Developer`:
  - **Connection**: Custom
  - **Server URL**: where the plugin should POST/longpoll. Must be HTTPS.
  - **Transport**: `http` (= JSON-RPC over streamableHttp)
  - **Authorization Token**: `required` mode + the auto-generated token.

### 2. unity-mcp-server (Docker container)

- Author: Ivan Murzak, image `ivanmurzakdev/unity-mcp-server:0.72.1`.
- Acts as the **MCP server endpoint** the plugin connects to.
- Runs in `streamableHttp` mode (HTTP long-poll JSON-RPC).
- Listens on TCP `443` inside the container.
- Required environment variables (set when starting):
  - `MCP_PLUGIN_CLIENT_TRANSPORT=streamableHttp`
  - `MCP_PLUGIN_PORT=443`
  - `MCP_PLUGIN_CLIENT_TIMEOUT=10000`
  - `MCP_AUTHORIZATION=required`
  - `MCP_PLUGIN_TOKEN=<must match plugin token exactly>`

Bring-up command equivalent to `tools/start_unity_mcp_server.sh`:

```bash
docker run -d \
    -p 443:443 \
    -e MCP_PLUGIN_CLIENT_TRANSPORT=streamableHttp \
    -e MCP_PLUGIN_PORT=443 \
    -e MCP_PLUGIN_CLIENT_TIMEOUT=10000 \
    -e MCP_AUTHORIZATION=required \
    -e MCP_PLUGIN_TOKEN=<TOKEN> \
    --name unity-mcp-server-443 \
    ivanmurzakdev/unity-mcp-server:0.72.1
```

### 3. Cloudflare quick-tunnel

- Provided by `cloudflared`.
- Creates an ephemeral HTTPS endpoint `https://<random>.trycloudflare.com`
  forwarding to `localhost:443`.
- **No authentication of its own** — the security is entirely the Bearer
  token (anyone with the URL can attempt to connect; only the right token
  is accepted).
- URL is regenerated every time `cloudflared` is restarted (e.g. after a
  VM reboot or `cloudflared` crash).

Bring-up equivalent to `tools/start_tunnel.sh`:

```bash
cloudflared tunnel --url https://localhost:443 --no-tls-verify
# parse stdout for "https://<random>.trycloudflare.com"
```

### 4. mcp_call.sh — agent-side helper

- See `tools/mcp_call.sh`.
- Performs the 3-step JSON-RPC handshake: `initialize` →
  `notifications/initialized` → `tools/call`.
- Manual `--resolve <host>:443:<ip>` via Google DoH lookup because the
  agent VM's local DNS resolver doesn't always know random
  `*.trycloudflare.com` subdomains.
- Reads `MCP_URL` and `MCP_TOKEN` from env or from
  `$HOME/.config/unity-mcp/{url,token}`.

## Failure modes & how to spot them

### Symptom: server logs `auth-required mode: plugin token does not match server token`

- Cause: someone pressed "New" on the plugin's token, or the wrong token
  was passed to Docker.
- Fix: ask user for current token, restart container with matching token.

### Symptom: agent's `mcp_call.sh` hangs or fails to get `mcp-session-id`

- Cause: tunnel down, or Cloudflare subdomain not resolving locally.
- Fix: restart tunnel (`tools/start_tunnel.sh`), update plugin URL,
  verify `--resolve` is working (cache in `/tmp/.mcp_ip_<host>`).

### Symptom: plugin in Unity shows red `Disconnected`

- Cause: server not up, or URL stale.
- Fix: confirm Docker container running (`docker ps`), confirm tunnel URL
  matches what's in the plugin, click "Connect" in plugin.

### Symptom: server logs `Version handshake failed: No response from server`

- Cause: the server cannot reach back to the plugin client. Usually
  transient (waiting for the plugin to long-poll).
- Fix: usually resolves in seconds. If persistent, check Unity Editor is
  not frozen / not in a modal dialog.

## Lifetime / persistence notes

- **Docker container** persists across cloudflared restarts. Don't
  recreate it unless the token changes.
- **Cloudflare quick-tunnel URL** is ephemeral. Expect it to change after
  VM pause/restart.
- **Plugin token** persists per Unity project (stored in user prefs
  inside Unity). Don't regenerate it unless necessary.
- **`$HOME/.config/unity-mcp/{url,token}`** is the agent's "where am I
  connected?" record. Treat as session state.

## Security

- The Bearer token is a long-random secret. Treat it like an API key.
- Don't commit `$HOME/.config/unity-mcp/token` to any repo.
- The Cloudflare URL itself is **not secret-by-obscurity** — Cloudflare
  may log the hostname; only the token matters.
- Anyone with both URL and token can drive the Unity Editor remotely.
  Rotate the plugin token if you suspect leakage.
