---
name: mesh-orient-scale
description: Auto-orient and scale a freshly imported mesh (typically from image-to-3D or arbitrary FBX/GLB import) so it stands upright with feet on the ground at human scale in Unity. Trigger after instantiating a prefab whose orientation/scale is unknown.
---

# Auto-orient and scale a freshly imported mesh

Generated meshes (especially from single-view LRM models like TripoSR) often
come in with an unknown orientation — Y-up by convention, but the model's
actual "head" may end up along +X if the input pose was arched / horizontal.

This skill is a deterministic procedure to (1) detect the right rotation,
(2) set a human-scale size, and (3) place the feet on the ground.

## Step 1: probe bounds at five candidate rotations

```csharp
// body-only via script-execute (isMethodBody=true)
var go = UnityEngine.GameObject.Find("ImportedCharacter");
if (go == null) { UnityEngine.Debug.LogError("not found"); return; }
foreach (var rot in new []{
    UnityEngine.Quaternion.Euler(0,0,0),
    UnityEngine.Quaternion.Euler(90,0,0),
    UnityEngine.Quaternion.Euler(-90,0,0),
    UnityEngine.Quaternion.Euler(0,0,90),
    UnityEngine.Quaternion.Euler(0,0,-90)
}) {
    go.transform.rotation = rot;
    var rs = go.GetComponentsInChildren<UnityEngine.Renderer>();
    var b = rs[0].bounds;
    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
    UnityEngine.Debug.Log($"[rot={rot.eulerAngles}] size={b.size}");
}
```

Then:

```bash
./tools/mcp_call.sh script-execute "$(cat /tmp/probe.json)"
./tools/mcp_call.sh console-get-logs '{"lastMinutes":1,"maxEntries":10}' \
    | grep "rot="
```

Pick the rotation whose `size.y` (vertical extent) is the largest — that's
the "standing upright" orientation. For a typical full-body photo the answer
is usually `(0,0,0)`. For an arched glamour photo it's often `(0,0,-90)` or
`(0,0,90)`.

## Step 2: apply rotation, set scale to human height

```csharp
var go = UnityEngine.GameObject.Find("ImportedCharacter");
go.transform.rotation = UnityEngine.Quaternion.Euler(0, 0, -90); // from step 1
go.transform.localScale = UnityEngine.Vector3.one * 2.5f;        // tunable

var rs = go.GetComponentsInChildren<UnityEngine.Renderer>();
var b = rs[0].bounds;
for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
UnityEngine.Debug.Log($"After scale: height={b.size.y}m");
```

TripoSR meshes are normalized to ~0.7 unity-units tall. Scale 2.5 → ~1.75 m
which is roughly an adult human. Adjust to taste.

## Step 3: place feet on a spawn point (or any anchor)

```csharp
var go = UnityEngine.GameObject.Find("ImportedCharacter");
var rs = go.GetComponentsInChildren<UnityEngine.Renderer>();
var b = rs[0].bounds;
for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

var anchor = UnityEngine.GameObject.Find("SpawnPoint_1");
var target = anchor != null ? anchor.transform.position : UnityEngine.Vector3.zero;

// Feet at anchor: center.y = anchor.y + extents.y
go.transform.position = new UnityEngine.Vector3(
    target.x,
    target.y + b.extents.y,
    target.z);

UnityEngine.Debug.Log($"placed at {go.transform.position}");
```

## Step 4: frame in Scene View, screenshot

```csharp
var go = UnityEngine.GameObject.Find("ImportedCharacter");
UnityEditor.Selection.activeGameObject = go;
var rs = go.GetComponentsInChildren<UnityEngine.Renderer>();
var b = rs[0].bounds;
for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
var sv = UnityEditor.SceneView.lastActiveSceneView;
if (sv != null) {
    sv.pivot = b.center;
    sv.rotation = UnityEngine.Quaternion.Euler(5, 200, 0); // front-left view
    sv.size = b.size.magnitude * 0.6f;
    sv.Repaint();
}
```

Then:

```bash
./tools/mcp_call.sh screenshot-scene-view '{}'
```

Decode the base64 PNG from `result.content[0].data`. Use the `read` tool
to actually look at the result and decide if more tweaking is needed.

## When to give up

If after probing the 5 cardinal rotations the largest `size.y` is still
less than ~1.3× the original max axis, the mesh is roughly cubic / blob-like
and the input image probably wasn't well-suited for LRM reconstruction.
Tell the user and offer:

- Re-generate with a higher mc-resolution.
- Try a clearer / less-obscured input image.
- Fall back to retexturing an existing humanoid model.

## Add a collider so the character interacts with physics

```csharp
var go = UnityEngine.GameObject.Find("ImportedCharacter");
var rs = go.GetComponentsInChildren<UnityEngine.Renderer>();
var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

// Use a CapsuleCollider for humanoids
var cap = go.AddComponent<UnityEngine.CapsuleCollider>();
cap.height = b.size.y;
cap.radius = UnityEngine.Mathf.Max(b.size.x, b.size.z) * 0.5f;
cap.center = b.center - go.transform.position;

// Optionally: Rigidbody for dynamic physics
var rb = go.AddComponent<UnityEngine.Rigidbody>();
rb.mass = 70f; // kg
rb.constraints = UnityEngine.RigidbodyConstraints.FreezeRotationX | UnityEngine.RigidbodyConstraints.FreezeRotationZ;
```
