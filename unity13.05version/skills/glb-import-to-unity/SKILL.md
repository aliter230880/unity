---
name: glb-import-to-unity
description: Deliver a binary asset file (GLB, FBX, PNG, MP4, …) from the agent's VM into the user's Unity project Assets/ folder without filesystem access. Uses a local HTTP server + public tunnel + `script-execute` to download from inside Unity. Trigger whenever you generated an asset locally and need it inside the Editor.
---

# Bringing binary assets into Unity over MCP

The MCP transport is JSON-RPC; there is no built-in tool to upload arbitrary
binary blobs into the user's `Assets/` folder. We side-step this by having
Unity itself download the file via a one-shot C# snippet (`script-execute`).

## Pre-requisite

For `.glb` files, install `com.unity.cloud.gltfast` once per project:

```bash
./tools/mcp_call.sh package-add '{"packageId":"com.unity.cloud.gltfast"}'
```

Wait for the response `Package add completed: Unity glTFast vX.Y.Z. Domain
reload finished successfully.` After this, `.glb` files in `Assets/` are
auto-imported as prefab-like GameObjects.

`.fbx`, `.png`, `.jpg`, `.mp3`, `.wav`, `.mp4` etc. are imported natively
by Unity — no package needed.

## Pipeline

### 1. Serve the file locally

```bash
./tools/serve_file.sh /tmp/out/0/mesh.glb 8765
# now http://localhost:8765/mesh.glb is up
```

### 2. Expose the port publicly

Devin: call `deploy expose port=8765` → returns a URL like
`https://user:<password>@<random>.devinapps.com`.

Other agents: `cloudflared tunnel --url http://localhost:8765` (no auth)
or any other public reverse proxy.

### 3. Have Unity download the file via `script-execute`

```bash
cat > /tmp/dl.cs <<'EOF'
var http = new System.Net.Http.HttpClient();

// If the tunnel has Basic Auth in the URL, set the header explicitly —
// UnityWebRequest sometimes strips user:pass@ from the URL.
var auth = System.Convert.ToBase64String(
    System.Text.Encoding.ASCII.GetBytes("user:<PASSWORD>"));
http.DefaultRequestHeaders.Add("Authorization", "Basic " + auth);

var bytes = http.GetByteArrayAsync(
    "https://<TUNNEL_HOST>/mesh.glb").GetAwaiter().GetResult();

if (!System.IO.Directory.Exists("Assets/1"))
    System.IO.Directory.CreateDirectory("Assets/1");

var path = "Assets/1/mesh.glb";
System.IO.File.WriteAllBytes(path, bytes);
UnityEditor.AssetDatabase.ImportAsset(path,
    UnityEditor.ImportAssetOptions.ForceUpdate);
UnityEditor.AssetDatabase.Refresh();
UnityEngine.Debug.Log("Downloaded " + bytes.Length + " bytes to " + path);
EOF

python3 -c "
import json
print(json.dumps({
    'csharpCode': open('/tmp/dl.cs').read(),
    'isMethodBody': True,
    'className': 'GlbDownloader',
    'methodName': 'Run'
}))
" > /tmp/dl.json

./tools/mcp_call.sh script-execute "$(cat /tmp/dl.json)"
```

### 4. Verify

```bash
./tools/mcp_call.sh assets-find '{"filter":"","searchInFolders":["Assets/1"],"maxResults":50}'
```

You should see your new asset, e.g.

```json
{"instanceID":73804,"assetType":"UnityEngine.GameObject",
 "assetPath":"Assets/1/mesh.glb","assetGuid":"b38c0bbb235677541a0c5cdf0c978a79"}
```

For a `.glb`, the asset shows up as a `UnityEngine.GameObject` (a prefab
ready to instantiate). For a `.png`, it shows up as `UnityEngine.Texture2D`.

### 5. Instantiate (for prefab-like assets)

```bash
./tools/mcp_call.sh assets-prefab-instantiate '{
    "prefabAssetPath":"Assets/1/mesh.glb",
    "gameObjectPath":"ImportedThing",
    "position":{"x":0,"y":2,"z":0},
    "rotation":{"x":0,"y":0,"z":0},
    "scale":{"x":1,"y":1,"z":1}
}'
```

## Gotchas

1. **Asset path must start with `Assets/`** — Unity refuses to import
   anything outside the project's `Assets/` tree.
2. **Domain reload.** After `AssetDatabase.Refresh()` the editor may
   recompile / reload domain. Subsequent MCP calls wait through this
   automatically (the server is patient).
3. **Large files.** HTTP timeout in `script-execute` is generous, but
   for >50 MB files consider chunking or pre-compressing.
4. **Basic Auth URLs.** `https://user:pass@host/...` works in C# Uri
   parser but UnityWebRequest sometimes strips credentials. Use the
   `Authorization: Basic <b64>` header explicitly (recipe above).
5. **HTTPS required by Unity.** Modern Unity blocks plain HTTP in
   `UnityWebRequest` by default. The tunnel should be HTTPS (Cloudflare
   and devinapps both are).

## Reverse direction: pull a file FROM Unity to the agent

Use `script-execute` to read bytes and base64-encode them into a log line:

```csharp
var bytes = System.IO.File.ReadAllBytes("Assets/Captures/screenshot.png");
UnityEngine.Debug.Log("BASE64_START:" + System.Convert.ToBase64String(bytes) + ":BASE64_END");
```

Then on agent side:

```bash
./tools/mcp_call.sh console-get-logs '{"lastMinutes":1,"maxEntries":1}' \
    | python3 -c "import sys,json,base64,re; \
        body=json.load(sys.stdin)['result']['structuredContent']['result'][0]['Message']; \
        b=re.search('BASE64_START:(.*):BASE64_END',body).group(1); \
        open('/tmp/from_unity.bin','wb').write(base64.b64decode(b))"
```

This works for files <100 KB or so (log payload limit). For larger files,
have Unity write the file to its `outputDir` and upload from there.
