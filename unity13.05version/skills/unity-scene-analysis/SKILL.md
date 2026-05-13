---
name: unity-scene-analysis
description: Build a complete snapshot of a connected Unity project — packages, scenes, scene hierarchy, components, scripts, console state — using MCP tools, before making any edits. Trigger this whenever you start a new session against an unfamiliar project ("проанализируй сцену", "what's in this project", "tell me about the codebase").
---

# Unity scene & project analysis via MCP

Before changing anything in a user's Unity project, get a complete picture.
This skill batches the right MCP calls in the right order so the agent
ends up with a structured snapshot to plan from.

## Step 1: Editor state

```bash
./tools/mcp_call.sh editor-state '{}'
```

Returns: Unity version, target platform, play mode flag, compile state,
selection. Confirms the bridge is alive.

## Step 2: List installed packages

```bash
./tools/mcp_call.sh package-list '{}'
```

Returns: every package in `Packages/manifest.json` + transitives. Pay
attention to:

- Render pipeline (`com.unity.render-pipelines.universal` = URP,
  `...high-definition` = HDRP, none = built-in).
- Networking (`com.unity.netcode.gameobjects`, `com.unity.transport`).
- Input (`com.unity.inputsystem` vs old `UnityEngine.Input`).
- Animation (`com.unity.cinemachine`, `com.unity.animation.rigging`).
- AI tooling (`com.unity.ai.assistant` = cloud-bound, paid).
- Importers you may need: `com.unity.cloud.gltfast` for GLB import.

## Step 3: List opened + all scenes

```bash
./tools/mcp_call.sh scene-list-opened '{}'
./tools/mcp_call.sh scene-list-all '{}'
```

`opened` = the scene the user is staring at right now. `all` = every
.unity asset in the project. Tells you which scene is "important" and how
much content exists.

## Step 4: Scene hierarchy (root + children)

```bash
./tools/mcp_call.sh gameobject-find-root-objects '{}'
```

Then dig deeper. For Unity 6 + Brackeys-style projects expect groups like
`---Networking---`, `---Gameplay---`, `InteractableObjects`, `Environment`
prefixes (dummy separator GameObjects).

For each interesting root:

```bash
./tools/mcp_call.sh gameobject-get-component-info '{"path":"<gameobject_path>"}'
```

Returns the component list with serialized fields.

## Step 5: Inspect scripts

```bash
# All scripts in Assets/
./tools/mcp_call.sh assets-find '{"filter":"t:script","searchInFolders":["Assets"],"maxResults":200}'
```

Filter results to user code (`Assets/...` not `Packages/`). Group by
folder to detect architecture (Core / Player / UI / Network).

For individual scripts:

```bash
./tools/mcp_call.sh script-read '{"filePath":"Assets/Scripts/PlayerController.cs"}'
```

## Step 6: Console health

```bash
./tools/mcp_call.sh console-get-logs '{"lastMinutes":60,"maxEntries":100,"logTypeFilter":null}'
```

Filter out plugin-internal noise (auth handshake failures from previous
session attempts). What remains tells you whether the project itself
has compile errors or runtime spam.

## Step 7: Take screenshots

```bash
./tools/mcp_call.sh screenshot-scene-view '{}'   # 1920x1080 PNG, base64-encoded
./tools/mcp_call.sh screenshot-game-view  '{}'   # requires Game tab open
```

Decode the base64, write to disk, view with the `read` tool to actually
see the project.

## Output format for the user

Write the result as a structured Russian/English summary:

1. **Project & engine version** — Unity X.Y.Z + render pipeline.
2. **Installed packages of note** — networking / input / animation / AI tools.
3. **Open scene path + summary** — N root objects, named groups.
4. **Per-system breakdown** — networking, gameplay logic, level art, UI.
5. **Number of user scripts + folder layout**.
6. **Editor state warnings** — compile errors, console spam.
7. **Suggested directions** — 4-6 concrete things you could build next.
8. **Scene View screenshot** attached.

This sets the user's expectations and gives them a menu of next actions.

## Performance notes

- The above sequence runs in <10s on a normal project.
- The `assets-find` filter argument uses Unity's project search syntax
  (`t:script`, `t:prefab`, `t:texture2D`, `t:material`).
- `gameobject-find-root-objects` returns the whole hierarchy in one call
  — don't call it per-object.
