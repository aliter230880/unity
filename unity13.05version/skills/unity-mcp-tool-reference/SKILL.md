---
name: unity-mcp-tool-reference
description: Quick reference for finding the right Unity-MCP tool out of the 82 available. Maps common tasks to specific tool names. Trigger before invoking a tool whose exact name you don't remember.
---

# Unity-MCP tool reference

Full catalog with descriptions is in [`context/mcp-tool-catalog.md`](../../context/mcp-tool-catalog.md).
This skill is a *task-oriented* index — given what you want to do, find the
tool name.

## Recipe: list all tools dynamically

```bash
./tools/mcp_tools_list.sh > /tmp/tools.json
python3 -c "
import json
j=json.load(open('/tmp/tools.json'))
for t in j['result']['tools']:
    print(f\"{t['name']:40s} {t.get('description','')[:80]}\")
"
```

## Task → tool

### Project & editor

| I want to… | Tool |
|---|---|
| Know Unity version, play mode, target platform | `editor-state` |
| Enter / exit play mode | `editor-set-play-mode` |
| Force recompile scripts | `editor-recompile` |
| Save all dirty assets | `editor-save-all` |
| List installed Unity packages | `package-list` |
| Install a package | `package-add` |
| Remove a package | `package-remove` |
| Upgrade a package | `package-update` |

### Scenes

| I want to… | Tool |
|---|---|
| Find which scene is open | `scene-list-opened` |
| List all .unity files in project | `scene-list-all` |
| Open a scene | `scene-open` |
| Create a new scene | `scene-create` |
| Save current scene | `scene-save` |
| Get scene hierarchy | `gameobject-find-root-objects` |

### GameObjects

| I want to… | Tool |
|---|---|
| Find an object by path | `gameobject-find` |
| Get components on an object | `gameobject-get-component-info` |
| Create empty GameObject | `gameobject-create` |
| Create primitive (cube/sphere/…) | `gameobject-create-primitive` |
| Add component (Rigidbody, Collider, MeshRenderer…) | `gameobject-component-add` |
| Modify component fields | `gameobject-component-modify` |
| Remove component | `gameobject-component-remove` |
| Set active / inactive | `gameobject-set-active` |
| Set transform (position/rotation/scale) | `gameobject-transform-set` |
| Set parent | `gameobject-set-parent` |
| Destroy GameObject | `gameobject-destroy` |

### Assets

| I want to… | Tool |
|---|---|
| Search the asset database | `assets-find` |
| Search built-in resources | `assets-find-built-in` |
| Read asset's serialized data | `assets-get-data` |
| Modify asset's serialized fields | `assets-modify` |
| Create new folder | `assets-create-folder` |
| Copy assets | `assets-copy` |
| Move/rename assets | `assets-move` |
| Delete assets | `assets-delete` |
| Refresh database after external file change | `assets-refresh` |
| Create new Material | `assets-material-create` |
| Inspect a shader | `assets-shader-get-data` |
| List all shaders | `assets-shader-list-all` |
| Create prefab from scene GameObject | `assets-prefab-create` |
| Instantiate prefab into scene | `assets-prefab-instantiate` |
| Edit a prefab | `assets-prefab-open`, `assets-prefab-save`, `assets-prefab-close` |

### Scripts

| I want to… | Tool |
|---|---|
| Read a .cs file | `script-read` |
| Write / create a .cs file | `script-update-or-create` |
| Delete a .cs file | `script-delete` |
| Execute arbitrary C# (Roslyn) | `script-execute` |

### Animations

| I want to… | Tool |
|---|---|
| List animation clips | `animation-list-clips` |
| Get clip data | `animation-get-clip-data` |
| Modify clip data | `animation-modify-clip` |
| Get Animator state machine | `animator-get-state-machine` |
| Modify Animator state machine | `animator-modify-state-machine` |
| Get Animator parameters | `animator-get-parameters` |

### Particles

| I want to… | Tool |
|---|---|
| Get ParticleSystem state | `particle-system-get` |
| Modify ParticleSystem modules | `particle-system-modify` |

### ProBuilder (procedural meshes)

| I want to… | Tool |
|---|---|
| Create a ProBuilder primitive | `probuilder-create-shape` |
| Get / set mesh vertices | `probuilder-get-vertices`, `probuilder-set-vertices` |
| Get / set faces | `probuilder-get-faces`, `probuilder-set-faces` |
| Extrude faces | `probuilder-extrude-faces` |
| Bevel edges | `probuilder-bevel-edges` |
| Subdivide / merge | `probuilder-subdivide-faces`, `probuilder-merge-vertices` |
| Triangulate / flip normals | `probuilder-triangulate`, `probuilder-flip-normals` |
| Bake mesh to asset | `probuilder-bake` |

### Console & screenshots

| I want to… | Tool |
|---|---|
| Read recent console logs | `console-get-logs` |
| Clear the console | `console-clear-logs` |
| Screenshot Scene View | `screenshot-scene-view` |
| Screenshot Game View | `screenshot-game-view` |
| Screenshot specific Editor window | `screenshot-editor-window` |
| Screenshot full screen | `screenshot-screen` |

### Reflection (for advanced cases)

| I want to… | Tool |
|---|---|
| Find a C# method in any assembly | `reflection-method-find` |
| Invoke any C# method (incl. private) | `reflection-method-call` |
| Get JSON schema of a C# type | `type-get-json-schema` |

### Object utilities

| I want to… | Tool |
|---|---|
| Get instance ID of any UnityEngine.Object | `object-get-instance-id` |
| Set instance ID on a serialized ref | `object-set-instance-id` |

### Tests

| I want to… | Tool |
|---|---|
| Run the project's Test Runner | `tests-run` |

### Meta

| I want to… | Tool |
|---|---|
| List MCP tools availble | `tool-list-all` (via JSON-RPC `tools/list`) |

## Schema discovery

When in doubt, get the input schema:

```bash
./tools/mcp_tools_list.sh > /tmp/tools.json
python3 -c "
import json,sys
j=json.load(open('/tmp/tools.json'))
for t in j['result']['tools']:
    if t['name']=='gameobject-component-add':
        print(json.dumps(t.get('inputSchema',{}), indent=2))
"
```

This tells you the exact argument structure expected. Don't guess.
