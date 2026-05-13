# Session log — 2026-05-13

Chronological record of what was tried during the multi-day session that
produced this foundation. Useful for understanding *why* certain decisions
were made, especially dead-ends that future sessions shouldn't re-explore.

## Phase 1: bridge bring-up (early in the day)

Cloudflare tunnel and Docker container needed restart after ~20h VM pause.
Procedure was already established in prior sessions:

1. `docker start unity-mcp-server-443` (with existing token).
2. `cloudflared tunnel --url https://localhost:443 --no-tls-verify` —
   new URL `cds-playback-impact-workstation.trycloudflare.com`.
3. User confirmed they were still using project `tps1`; bridge re-established.

Key gotcha: the user switched from a prior `test_tools` project to `tps1`,
which meant a different plugin instance and therefore a different Bearer
token. User pasted `docker run` line with the new token:
`7nG-PXAU-KKjkUfRF_NgidYWFJIRPRe1gHtS8cnTt78`.

## Phase 2: scene analysis

Requested "проанализируй сцену" — produced the snapshot now in
`tps1-project-snapshot.md`. No surprises: clean Brackeys Bundle multiplayer
platformer, Unity 6.3, URP, NGO networking.

## Phase 3: user uploads photo, asks for 3D character

Steps:

1. User added a glamour photo PNG into `Assets/1/`.
2. Requested: "Make a 3D volumetric character from this picture in the
   same colors / textures — should be a beautiful woman."

### Decision tree at this point

Three options offered:

- **A.** AI image-to-3D service (Tripo3D / Meshy / Rodin) — best quality
  but paid (free tier limited).
- **B.** Retexture existing humanoid prefab in project with the photo —
  free, fast, not really "3D from photo".
- **C.** Billboard plane in 3D space — fastest but not really 3D.

User selected A but then asked to use open-source tools only — "no paid
subscriptions, take a real good open-source tool from Hugging Face and
make a skill for yourself, then do everything independently."

## Phase 4: Hugging Face Spaces — anonymous access exploration (DEAD END)

Attempted ~25 public HF Spaces with anonymous Gradio API access. All
failed for one of:

| Space | Failure |
|---|---|
| VAST-AI/TripoSG | `GPU duration (90s) is larger than maximum allowed` for anon |
| tencent/Hunyuan3D-2 | Same: GPU duration limit |
| stabilityai/stable-fast-3d | Internal `AppError`, no `show_error` info |
| JeffreyXiang/TRELLIS | RUNTIME_ERROR / PAUSED |
| Unique3D, InstantMesh, CharacterGen, +~15 more | RUNTIME_ERROR / PAUSED / 404 Repo |

**Conclusion in 2026-05:** anonymous HF Spaces for image-to-3D are
unusable. All require an HF token (free, but requires user-supplied
secret) to unlock GPU quota.

## Phase 5: Local TripoSR — SUCCESS

Pivoted to running TripoSR locally on Devin's CPU-only VM:

- Hardware: 2 vCPU, 7 GB RAM, no GPU.
- Initial fear: TripoSR's reference timings are ~0.5s on A100; on CPU
  this could be minutes.

### Setup steps

1. `git clone https://github.com/VAST-AI-Research/TripoSR.git ~/TripoSR`.
2. `pip install torch==2.6.0+cpu` (NOTE: 2.1.2 from requirements.txt is
   not available on PyTorch CPU index; latest available was 2.6.0+cpu).
3. `pip install omegaconf einops transformers==4.45.2 trimesh rembg[cpu]
   onnxruntime huggingface-hub xatlas==0.0.9 imageio[ffmpeg] moderngl
   PyMCubes`.
4. `sudo apt-get install -y libgl1 libegl1 libegl1-mesa libgles2-mesa`
   (needed only if `--bake-texture` is used).
5. **Patched** `tsr/models/isosurface.py` — TripoSR ships hardcoded
   `from torchmcubes import marching_cubes` which requires CUDA. Replaced
   with a `try/except ImportError` fallback to PyMCubes (CPU). Same
   signature transformation:
   ```python
   def marching_cubes(level, threshold):
       v, f = mcubes.marching_cubes(level.detach().cpu().numpy(), threshold)
       return torch.from_numpy(v.copy()).float(), torch.from_numpy(f.copy()).long()
   ```

### Performance result

Way better than expected — TripoSR is surprisingly CPU-friendly:

| Stage | Time on 2-core CPU |
|---|---|
| Model init | ~5–10 s |
| Image preprocessing (rembg background removal) | ~1.5–3 s |
| Inference (LRM forward pass) | ~14 s |
| Mesh extraction (PyMCubes, resolution 256) | ~23 s |
| GLB export | <1 s |
| **Total at mc-resolution 256** | **~45 s** |
| Total at mc-resolution 128 | ~25 s |

No OOM. No torch / numpy errors after the patch. PyMCubes is a reasonable
1:1 replacement for `torchmcubes` for the topology TripoSR produces.

### Output

GLB file with vertex colors (no PBR maps). 17 878 verts, 35 760 faces.
Body shape recognizable, especially from the input camera angle. Back side
hallucinated (typical of single-view LRMs).

## Phase 6: Importing into Unity (no filesystem access)

Devin's VM cannot write to the user's Windows disk directly. Workaround:

1. Install `com.unity.cloud.gltfast 6.18.0` in the project via
   `package-add`. (Took ~60s; included successful domain reload.)
2. Start `python3 -m http.server 8765 --directory /tmp/serve`.
3. Devin tool `deploy expose port=8765` returned a Basic-Auth-protected
   public URL.
4. Wrote a C# snippet (via `script-execute`, `isMethodBody=true`) that
   `HttpClient.GetByteArrayAsync`'d the file and `File.WriteAllBytes`'d
   it to `Assets/1/woman.glb`. Then `AssetDatabase.ImportAsset` +
   `Refresh`. Verified via `assets-find` — Unity reported the GLB as
   `UnityEngine.GameObject` (= imported as prefab).

### Gotcha encountered

The Basic Auth credentials in the URL (`https://user:pass@host`) were
stripped by C#'s `HttpClient`. Workaround: set the `Authorization: Basic
<base64>` header explicitly. After that, download worked first try.

## Phase 7: Placement and orientation

Instantiated via `assets-prefab-instantiate` at world (0,2,0). First
screenshot showed the model lying horizontally — single-view LRM had
output the mesh with its main axis along X (because the input photo was
an arched pose).

Probed bounds at five candidate rotations via `script-execute`:

| Euler rotation | Bounds size (X, Y, Z) |
|---|---|
| (0, 0, 0) | (2.36, 0.75, 0.63) |
| (90, 0, 0) | (2.36, 0.63, 0.75) |
| (-90, 0, 0) | (2.36, 0.63, 0.75) |
| (0, 0, 90) | (0.75, 2.36, 0.63) ← largest Y, upright |
| (0, 0, -90) | (0.75, 2.36, 0.63) ← largest Y, upright |

Picked `(0, 0, -90)` so the front of the character faced +Z. Scaled to
`2.5x` (~1.6 m tall). Placed at `SpawnPoint_1` with `position.y =
spawn.y + bounds.extents.y` so the feet sit on the spawn pad.

Screenshot via `screenshot-scene-view` confirmed: woman with long hair,
dark torso, lighter midriff, dark legs — recognizable as the input subject,
standing upright on the platformer level.

## Phase 8: User asked to organize everything into a repo

That's this directory.

## Things that DID NOT work (don't try these again unless something changes)

1. **Hugging Face anonymous Spaces for image-to-3D.** All gated behind
   either HF token or "model paused" — see Phase 4.
2. **TripoSR's `--bake-texture` on a headless VM with `moderngl`.** Needs
   a working GL context; libGL was installed but EGL still failed. Use
   vertex colors instead.
3. **Inline `https://user:pass@host` URLs in C# HttpClient.** Strips
   credentials. Use explicit `Authorization: Basic` header.
4. **`pip install torch==2.1.2`** from the PyTorch CPU index — version
   isn't available, has to be 2.2.0+cpu through 2.12.0+cpu. Used 2.6.0+cpu.
5. **`assets-prefab-instantiate` without `gameObjectPath`** — schema marks
   it required; without it the call errors. Pass an explicit name like
   `"gameObjectPath":"GeneratedWoman"`.
6. **MCP `package-add '{"packageName":"..."}'`** — wrong key. Schema uses
   `packageId`, not `packageName`. The error message is non-obvious.
7. **`assets-get-info`** — does not exist as a tool name. Actual tool is
   `assets-get-data`.

## Things that DID work (the foundation)

- `tools/mcp_call.sh` as a single entry point to all 82 MCP tools.
- `tools/img_to_3d.sh` wrapping local TripoSR at 256-res in ~45 s.
- `tools/serve_file.sh` + `deploy expose` + `script-execute` HttpClient
  pattern for "upload binary to Unity Assets/".
- Pre-probe-then-apply rotation pattern for unknown-orientation meshes.
- Explicit `Authorization: Basic` header for Basic Auth tunnels.
