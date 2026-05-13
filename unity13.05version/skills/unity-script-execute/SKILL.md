---
name: unity-script-execute
description: Run arbitrary C# code inside a connected Unity Editor via the `script-execute` MCP tool (Roslyn-backed). Use whenever no purpose-built MCP tool exists for what you need, or when an existing tool is too restrictive. Examples: rotate/scale/probe a GameObject, write binary files into Assets/, download from a URL, instantiate prefab with custom logic, batch-edit many components in one round-trip.
---

# `script-execute` — the escape hatch

`script-execute` is the most powerful tool exposed by Unity-MCP. It
compiles arbitrary C# via Roslyn against the project's assemblies and
executes the result in the editor. Whatever Unity's API can do, this
tool can do.

## Two modes

### Mode A: full code (`isMethodBody=false`, default)

You supply a complete C# compilation unit with usings, class, and a
static method. Best for cases where you need multiple helper classes
or custom imports.

```csharp
using UnityEngine;
public class Script {
    public static void Main() {
        Debug.Log("Hello from Unity Editor");
    }
}
```

Call signature:

```json
{
  "csharpCode": "using UnityEngine; public class Script { public static void Main() { Debug.Log(\"hi\"); } }",
  "className": "Script",
  "methodName": "Main"
}
```

### Mode B: body-only (`isMethodBody=true`) — preferred

The tool auto-generates the usings (`System`, `UnityEngine`, `AIGD`,
`com.IvanMurzak.Unity.MCP.Runtime.Extensions`, `UnityEditor`), the class
shell, and a `void Run()` signature. You only write the body.

```json
{
  "csharpCode": "var go = UnityEngine.GameObject.Find(\"Player\"); if (go != null) go.transform.position = UnityEngine.Vector3.zero;",
  "isMethodBody": true,
  "className": "MoverScript",
  "methodName": "Run"
}
```

### When to use which

| Scenario | Mode |
|---|---|
| One-shot Unity-API call | B (body-only) |
| Need custom struct / nested class | A (full) |
| Want to use external nuget assembly | A (full, with explicit using) |
| Quick probe / read | B |

## Gotchas

1. **Output capture.** The tool's response body is empty even on success.
   To get values back, write them to `Debug.Log` and read with
   `console-get-logs '{"lastMinutes":1,"maxEntries":50}'` afterwards.

2. **No top-level statements.** Mode A requires a class declaration.
   Mode B requires statements only (no class). Mixing fails.

3. **Async / await.** Don't use `async` in the entry method. Use
   `.GetAwaiter().GetResult()` instead. Async return types confuse Roslyn
   wrapper.

4. **AssetDatabase.Refresh + Domain reload.** If your code adds/modifies
   assets, call `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)`
   then `AssetDatabase.Refresh()`. The next MCP call will wait through
   domain reload automatically.

5. **Selection / SceneView focus.** `UnityEditor.Selection.activeGameObject = go;`
   followed by `UnityEditor.SceneView.lastActiveSceneView?.FrameSelected()`
   is the standard "look at the thing I just made" pattern. Useful when
   you're about to take a screenshot.

6. **Errors come back as text in `result.content`.** Compile errors,
   exceptions, `Debug.LogError` — all surfaced. Always check `isError`
   in the response.

## Recipes

### Place a GameObject at a spawn point with feet on the ground

```csharp
var go = UnityEngine.GameObject.Find("GeneratedCharacter");
if (go == null) { UnityEngine.Debug.LogError("not found"); return; }
var rs = go.GetComponentsInChildren<UnityEngine.Renderer>();
var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
var spawn = UnityEngine.GameObject.Find("SpawnPoint_1");
var p = spawn != null ? spawn.transform.position : UnityEngine.Vector3.zero;
go.transform.position = new UnityEngine.Vector3(p.x, p.y + b.extents.y, p.z);
```

### Download a file from HTTPS into Assets/

```csharp
var http = new System.Net.Http.HttpClient();
var bytes = http.GetByteArrayAsync("https://example.com/asset.glb").GetAwaiter().GetResult();
if (!System.IO.Directory.Exists("Assets/Imported")) System.IO.Directory.CreateDirectory("Assets/Imported");
var path = "Assets/Imported/asset.glb";
System.IO.File.WriteAllBytes(path, bytes);
UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate);
UnityEditor.AssetDatabase.Refresh();
UnityEngine.Debug.Log("Wrote " + bytes.Length + " bytes to " + path);
```

### Probe object bounds at multiple rotations

```csharp
var go = UnityEngine.GameObject.Find("MyMesh");
foreach (var rot in new []{
    UnityEngine.Quaternion.Euler(0,0,0),
    UnityEngine.Quaternion.Euler(90,0,0),
    UnityEngine.Quaternion.Euler(-90,0,0),
    UnityEngine.Quaternion.Euler(0,0,90),
    UnityEngine.Quaternion.Euler(0,0,-90)
}) {
    go.transform.rotation = rot;
    var rs = go.GetComponentsInChildren<UnityEngine.Renderer>();
    var b = rs[0].bounds; for (int i=1; i<rs.Length; i++) b.Encapsulate(rs[i].bounds);
    UnityEngine.Debug.Log($"[rot={rot.eulerAngles}] size={b.size}");
}
```

### Read scene hierarchy by depth

```csharp
foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) {
    WalkRecursive(root.transform, 0);
}
void WalkRecursive(UnityEngine.Transform t, int depth) {
    UnityEngine.Debug.Log(new string(' ', depth*2) + t.name);
    foreach (UnityEngine.Transform child in t) WalkRecursive(child, depth+1);
}
```

### Bulk-rename children matching a pattern

```csharp
var root = UnityEngine.GameObject.Find("CollectiblesParent");
int n = 0;
foreach (UnityEngine.Transform c in root.transform) {
    if (c.name.StartsWith("Pfb_coin")) { c.name = $"Coin_{n++:000}"; }
}
UnityEngine.Debug.Log($"renamed {n}");
```

## Helper invocation pattern

Write the body to a file, wrap with python to produce JSON, send via
`mcp_call.sh`:

```bash
cat > /tmp/script.cs <<'EOF'
... C# body here ...
EOF

python3 -c "
import json
print(json.dumps({
    'csharpCode': open('/tmp/script.cs').read(),
    'isMethodBody': True,
    'className': 'TmpRunner',
    'methodName': 'Run'
}))
" > /tmp/script.json

./tools/mcp_call.sh script-execute "$(cat /tmp/script.json)"
```

Then read back any logs:

```bash
./tools/mcp_call.sh console-get-logs '{"lastMinutes":1,"maxEntries":50}'
```

## When NOT to use script-execute

If a purpose-built MCP tool exists (`gameobject-set-active`,
`assets-prefab-instantiate`, `assets-material-create`), prefer it.
Reasons:

1. **Schema validation.** Built-in tools validate parameters; raw C#
   crashes at runtime instead.
2. **Idempotence.** Many built-in tools handle "already exists" gracefully.
3. **Discoverability.** Future agents can find named tools via
   `tools/list`; ad-hoc C# is invisible.
