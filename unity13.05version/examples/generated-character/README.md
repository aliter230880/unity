# Example: glamour photo → 3D mesh → Unity scene

This example documents the end-to-end flow that was executed during the
2026-05-13 session, *without including the user's personal photograph
or the derived mesh* (privacy).

## Reproducible run (using TripoSR's own sample image)

```bash
# 0. Make sure TripoSR is set up (one-time)
./tools/patch_triposr_cpu.sh

# 1. Generate from a neutral sample
./tools/img_to_3d.sh ~/TripoSR/examples/police_woman.png /tmp/example_out 256
# -> /tmp/example_out/0/mesh.glb

# 2. Add glTFast to your Unity project (one-time)
./tools/mcp_call.sh package-add '{"packageId":"com.unity.cloud.gltfast"}'

# 3. Serve the file
./tools/serve_file.sh /tmp/example_out/0/mesh.glb 8765
#    Then publish port 8765 via Cloudflare quick-tunnel or `deploy expose`.

# 4. Download into Unity Assets/ via script-execute
cat > /tmp/dl.cs <<'EOF'
var http = new System.Net.Http.HttpClient();
var bytes = http.GetByteArrayAsync("https://<YOUR_TUNNEL_HOST>/mesh.glb").GetAwaiter().GetResult();
if (!System.IO.Directory.Exists("Assets/Generated"))
    System.IO.Directory.CreateDirectory("Assets/Generated");
var path = "Assets/Generated/police_woman.glb";
System.IO.File.WriteAllBytes(path, bytes);
UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate);
UnityEditor.AssetDatabase.Refresh();
UnityEngine.Debug.Log("Wrote " + bytes.Length + " bytes to " + path);
EOF
python3 -c "
import json
print(json.dumps({
    'csharpCode': open('/tmp/dl.cs').read(),
    'isMethodBody': True,
    'className': 'Importer',
    'methodName': 'Run'
}))
" > /tmp/dl.json
./tools/mcp_call.sh script-execute "$(cat /tmp/dl.json)"

# 5. Verify import succeeded
./tools/mcp_call.sh assets-find '{"filter":"","searchInFolders":["Assets/Generated"],"maxResults":10}'

# 6. Instantiate in the active scene
./tools/mcp_call.sh assets-prefab-instantiate '{
    "prefabAssetPath":"Assets/Generated/police_woman.glb",
    "gameObjectPath":"ExampleCharacter",
    "position":{"x":0,"y":2,"z":0},
    "rotation":{"x":0,"y":0,"z":0},
    "scale":{"x":2.5,"y":2.5,"z":2.5}
}'

# 7. Auto-orient (probe + apply) — see skills/mesh-orient-scale/SKILL.md
```

## Expected result

For TripoSR's `examples/police_woman.png` reference image at `mc-resolution=256`:

- File size: ~700 KB GLB.
- Verts / faces: ~18 k / ~36 k.
- Visible result: standing humanoid figure with the input photo's colors
  (uniform / hair / skin / pose) baked into vertex colors. Recognizable
  from the front; back hallucinated.
- Reasonable scale once `2.5x` is applied (~1.6 m tall).

## Quality expectations

Single-view image-to-3D is **PS2 / early-PS3 era** quality. Don't expect:
- Detailed faces (heavy stylisation, eyes/nose often blurred).
- Crisp clothing seams.
- Working back-of-head detail (always hallucinated).
- PBR materials (output is vertex colors only).

Acceptable for:
- Background NPCs.
- "Stylised diorama" set dressing.
- Rough placeholder for later replacement.
- Demonstrating an end-to-end agent pipeline.

Not acceptable for:
- Hero characters / main-cast game models.
- Animation rigs (TripoSR output is static mesh with no skeleton).
