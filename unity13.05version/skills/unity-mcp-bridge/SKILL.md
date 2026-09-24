---
name: unity-mcp-bridge
description: Set up a working bidirectional connection between an AI agent (running on Linux/Mac) and a user's Unity Editor (typically on Windows). Uses Docker `unity-mcp-server`, Cloudflare quick-tunnel, and the Unity plugin "AI Game Developer" by Ivan Murzak. Trigger whenever you need to start (or recover from VM-restart) an MCP session against a real Unity Editor.
---

# Unity-MCP bridge — bring-up procedure

This skill describes how to stand up the network plumbing that lets an AI
agent invoke MCP tools against a *running* Unity Editor on the user's
desktop. The agent has no filesystem access to the user's machine — all
operations go through MCP over HTTPS.

## Architecture

```
+----------------+      streamableHttp        +-------------------+
| AI agent host  |  ---- POST /tools/call --> |  Docker container |
| (Linux VM)     |                            |  unity-mcp-server |
|                |  <-- response (JSON-RPC)-- |  port 443         |
+----------------+                            +-------------------+
       ^                                              ^
       | Cloudflare quick-tunnel                      |
       | (random subdomain.trycloudflare.com)         |
       v                                              |
+--------------------+                                |
| User's Unity Editor |  ----- SignalR longpoll -----+
| AI Game Developer   |        (the plugin reaches
| plugin (Bearer XXX) |        out to the tunnel URL
+--------------------+         and reuses the same
                               Bearer token)
```

## One-time prerequisites on agent host

```bash
# Docker
sudo apt-get install -y docker.io
sudo usermod -aG docker $USER   # then re-login or `newgrp docker`

# Cloudflared
curl -L https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64 \
    -o /usr/local/bin/cloudflared && chmod +x /usr/local/bin/cloudflared

# jq for mcp_call.sh
sudo apt-get install -y jq
```

The Docker image `ivanmurzakdev/unity-mcp-server:0.72.1` is pulled
automatically on first run.

## Per-session bring-up

1. **Generate the public URL first (server token unknown yet).**
   Run `start_unity_mcp_server.sh` with a *placeholder* token (it doesn't
   matter — Unity will use its own). Then `start_tunnel.sh` to publish
   port 443. Capture the URL printed.

   ```bash
   ./tools/start_unity_mcp_server.sh dummy-bootstrap-token
   ./tools/start_tunnel.sh 443
   # URL is in $HOME/.config/unity-mcp/url
   ```

2. **Tell the user the URL.** They have to:
   - Open the Unity project in Editor.
   - Install the plugin if missing:
     https://github.com/IvanMurzak/Unity-MCP/releases/latest/download/AI-Game-Dev-Installer.unitypackage
     → `Assets → Import Package → Custom Package`.
   - Open `Window → AI Game Developer`.
   - Set **Connection: Custom**, **Server URL: <tunnel URL>**,
     **Transport: http**, **Authorization Token: required** (don't press
     "New" again — leave the auto-generated token alone).
   - Scroll to section *3. Copy paste the json into your MCP Client* —
     copy the `"Authorization": "Bearer XXXX..."` value and send it back.

3. **Restart server with real token.**

   ```bash
   ./tools/start_unity_mcp_server.sh "<real_token_from_user>"
   echo "<real_token_from_user>" > $HOME/.config/unity-mcp/token
   ```

   (Script auto-removes the bootstrap container if the token differs.)

4. **Verify connectivity.** The plugin in Unity should flip its status to
   green within ~10 seconds. From the agent side:

   ```bash
   ./tools/mcp_call.sh editor-state '{}'      # should return Unity version, play mode, etc.
   ./tools/mcp_call.sh scene-list-opened '{}' # should return active scene path
   ```

## Recovery procedures

### A. Cloudflare tunnel dropped (URL changed)

Triggered when the VM was paused/restarted or `cloudflared` crashed. Symptom:
agent's `mcp_call.sh` returns connection errors, plugin shows red.

1. Run `./tools/start_unity_mcp_server.sh "<existing_token>"` (idempotent —
   no-op if container already runs with same token).
2. Run `./tools/start_tunnel.sh <port>` — new URL printed. The script already
   uses `--protocol http2` (see gotcha below). If the container is on a
   non-default port (e.g. 29422), pass it explicitly.
3. Tell the user the new URL → they paste it into the plugin → reconnect.
4. **Token unchanged.** No need to re-fetch.

### B. User clicked "New" on the token

Plugin regenerates token, server still uses the old one. Symptom: server
logs `auth-required mode: plugin token does not match server token,
disconnecting`.

1. Ask user to send the new Bearer token (same procedure as step 2 above).
2. `./tools/start_unity_mcp_server.sh "<new_token>"` — recreates the
   container with the new token.
3. `echo "<new_token>" > $HOME/.config/unity-mcp/token`.

### C. Switching to a different Unity project

Each Unity project has its own plugin instance and its own token. Switching
project = same procedure as a fresh bring-up (token will be different even
on the same machine).

### D. Tunnel hangs in "failed to dial quic" loop (⚠️ important)

**Symptom:** `cloudflared tunnel --url ...` prints `Failed to dial a quic connection error="failed to dial to edge with quic: timeout: no recent network activity"` and never produces a working URL (or produces a URL that returns Cloudflare error 530 "Tunnel error").

**Cause:** By default cloudflared uses **QUIC** (UDP/443) to reach the Cloudflare edge. Many environments — containers, CI runners, corporate VPNs, restricted firewalls — block outbound UDP on port 443. The tunnel control plane never comes up.

**Fix:** Force HTTP/2 (TCP/443) which works anywhere outbound HTTPS works:

```bash
cloudflared tunnel --url http://localhost:<port> --protocol http2
```

`tools/start_tunnel.sh` in this repo already passes `--protocol http2` by default. If you wrote a one-off `cloudflared` invocation, add the flag manually.

**Diagnostic:** look at `/tmp/cloudflared.log`. If you see `Initial protocol quic` followed by a chain of `ERR Failed to dial a quic connection` lines and never an `INF Registered tunnel connection` line, this is the issue.

### E. Tunnel returns 530 "Tunnel error" with `--protocol http2`

The control plane is up (you saw `INF Registered tunnel connection`), but requests through the URL return Cloudflare error 530.

**Cause:** cloudflared can't reach the *origin* (your local server). Either:
- The port is wrong (e.g. tunnel points to localhost:443 but container is on localhost:29422).
- The scheme is wrong (e.g. tunnel uses `https://localhost:443` but the container speaks plain HTTP — you'll see 502 Bad Gateway). The unity-mcp-server container speaks plain HTTP, so always use `http://localhost:<port>`.
- The container isn't actually running on that port (check with `docker ps`).

**Diagnostic:** before publishing the URL to the user, smoke-test locally:
```bash
curl -i -X POST http://localhost:<port>/ \
    -H "Authorization: Bearer <token>" \
    -H "Content-Type: application/json" \
    -H "Accept: application/json, text/event-stream" \
    -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"devin","version":"1.0"}}}'
```
If this returns `HTTP/1.1 200 OK` with `Mcp-Session-Id`, the origin is healthy and the issue is in cloudflared config. If this returns a connection-refused, fix the container first.

## Common errors

| Error in server logs | Meaning | Fix |
|---|---|---|
| `Connection rejected: the provided token does not match the server token` | Plugin token ≠ server token | See recovery B |
| `Version handshake failed: No response from server` | Tunnel down or cloudflared not reachable | See recovery A |
| `MCP_PLUGIN_TOKEN environment variable is not set` | Container started without token | Always pass token as `-e MCP_PLUGIN_TOKEN=...` |

| Error in agent | Meaning | Fix |
|---|---|---|
| `Failed to get mcp-session-id from initialize response` | The HTTPS endpoint is unreachable or returns non-MCP | Check tunnel URL; check `--resolve` works (Cloudflare subdomains often need explicit DoH resolve) |
| `Tool with Name 'X' not found` | Misnamed tool | Run `mcp_tools_list.sh` to list available tools |

## Don'ts

- **Don't push directly to the user's main branch** in any associated git
  repo. MCP work is in the live Editor; commits are a separate concern.
- **Don't tell the user to save the scene** while you're modifying it —
  you'll race and overwrite each other. Coordinate explicitly.
- **Don't share the Bearer token publicly.** It grants full editor control.
  Store in `$HOME/.config/unity-mcp/token` (mode 600), not in the repo.
