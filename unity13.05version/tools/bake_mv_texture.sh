#!/usr/bin/env bash
# bake_mv_texture.sh — Bake vertex colors onto a 3D mesh from 4 view images.
#
# Use this to add color to an untextured mesh (e.g., output of
# providers/hf_hunyuan3d_mv.sh) by projecting input photos back onto the
# geometry from 4 cardinal directions.
#
# How it works:
#   1. Loads mesh + 4 input photos (front/back/left/right)
#   2. UV-unwraps the mesh via xatlas (2048 atlas, padding 4)
#   3. For each vertex, projects to each photo using orthographic camera from
#      that side and samples the pixel color
#   4. Weights each sample by alignment of vertex normal to camera direction
#      (max(0, dot)^1.5)
#   5. Blends weighted samples, writes vertex colors back to GLB
#
# Limitations:
#   - The 4 photos must be roughly aligned to ±X / ±Z axes of the mesh's
#     canonical pose (head at +Y, feet at -Y).
#   - Photos should have a dark or uniform background; we auto-detect figure
#     bbox by thresholding non-black pixels.
#   - The projection assumes orthographic camera; perspective distortion in
#     input photos will cause minor stretching near image edges.
#   - Vertex colors are coarse compared to a real UV-baked texture map. For
#     PBR-quality output, run Blender's bake operator on the result.
#
# Usage:
#   bake_mv_texture.sh <input_glb> <output_glb> <front.png> <back.png> <left.png> <right.png>
#
# Requires: python3, trimesh, xatlas, numpy, pillow

set -euo pipefail

if [[ $# -lt 6 ]]; then
    echo "Usage: $0 <input_glb> <output_glb> <front.png> <back.png> <left.png> <right.png>" >&2
    exit 1
fi

IN_GLB="$1"
OUT_GLB="$2"
FRONT="$3"
BACK="$4"
LEFT="$5"
RIGHT="$6"

for f in "$IN_GLB" "$FRONT" "$BACK" "$LEFT" "$RIGHT"; do
    [[ -f "$f" ]] || { echo "[bake_mv_texture] missing: $f" >&2; exit 1; }
done

python3 -c "import trimesh, xatlas, numpy, PIL" 2>/dev/null || {
    echo "[bake_mv_texture] Installing deps..." >&2
    python3 -m pip install --user --quiet "trimesh>=4.0" "xatlas" "numpy" "pillow"
}

mkdir -p "$(dirname "$OUT_GLB")"

python3 - "$IN_GLB" "$OUT_GLB" "$FRONT" "$BACK" "$LEFT" "$RIGHT" << 'PY'
import sys, os
import numpy as np
import trimesh
import xatlas
from PIL import Image

in_glb, out_glb, p_front, p_back, p_left, p_right = sys.argv[1:7]

print(f"[bake_mv_texture] Loading {in_glb}...")
mesh = trimesh.load(in_glb, force="mesh", process=False)
# Normalize: center at origin, scale so largest extent = 2
mesh.apply_translation(-mesh.centroid)
mesh.apply_scale(2.0 / mesh.extents.max())
print(f"  verts={len(mesh.vertices)} faces={len(mesh.faces)} bounds={mesh.bounds.tolist()}")


def load_with_bbox(path, bg_threshold=30):
    """Detect bbox of foreground figure (background expected to be dark/black)."""
    img = np.array(Image.open(path).convert("RGB"))
    H, W = img.shape[:2]
    fg = ~np.all(img < bg_threshold, axis=-1)
    rows = np.any(fg, axis=1)
    cols = np.any(fg, axis=0)
    if not rows.any():
        return img, (0, H - 1, 0, W - 1)
    rmin, rmax = np.where(rows)[0][[0, -1]]
    cmin, cmax = np.where(cols)[0][[0, -1]]
    return img, (rmin, rmax, cmin, cmax)


print("[bake_mv_texture] Loading views and detecting bboxes...")
front_img, front_bb = load_with_bbox(p_front)
back_img, back_bb = load_with_bbox(p_back)
right_img, right_bb = load_with_bbox(p_right)
left_img, left_bb = load_with_bbox(p_left)
for name, bb, img in [("front", front_bb, front_img), ("back", back_bb, back_img),
                       ("right", right_bb, right_img), ("left", left_bb, left_img)]:
    rmin, rmax, cmin, cmax = bb
    print(f"  {name}: {img.shape[1]}x{img.shape[0]}  fg bbox y=[{rmin},{rmax}] x=[{cmin},{cmax}]")


print("[bake_mv_texture] Running xatlas UV unwrap (2048)...")
atlas = xatlas.Atlas()
atlas.add_mesh(mesh.vertices, mesh.faces)
opts = xatlas.PackOptions()
opts.resolution = 2048
opts.padding = 4
atlas.generate(pack_options=opts)
vmapping, indices, uvs = atlas[0]

verts_re = mesh.vertices[vmapping]
vnormals_re = mesh.vertex_normals[vmapping]
faces_re = indices
print(f"  unwrapped: verts={len(verts_re)} faces={len(faces_re)}")


def project(verts, vnorms, img, bbox, axis_idx, axis_sign):
    """Project verts onto image with normal-based weighting."""
    H, W = img.shape[:2]
    rmin, rmax, cmin, cmax = bbox
    if axis_idx == 2:
        u_world = verts[:, 0] * axis_sign
    else:  # X axis
        u_world = verts[:, 2] * (-axis_sign)
    v_world = verts[:, 1]
    u_min, u_max = u_world.min(), u_world.max()
    v_min, v_max = v_world.min(), v_world.max()
    if u_max - u_min < 1e-6 or v_max - v_min < 1e-6:
        return None, None
    u_norm = (u_world - u_min) / (u_max - u_min)
    v_norm = (v_world - v_min) / (v_max - v_min)
    px = (cmin + u_norm * (cmax - cmin)).astype(int).clip(0, W - 1)
    py = (rmin + (1.0 - v_norm) * (rmax - rmin)).astype(int).clip(0, H - 1)
    cols = img[py, px].astype(np.float32)
    cam_dir = np.zeros(3, dtype=np.float32)
    cam_dir[axis_idx] = axis_sign
    align = np.dot(vnorms, cam_dir)
    weights = np.clip(align, 0, 1) ** 1.5
    return cols, weights


N = len(verts_re)
total_color = np.zeros((N, 3), dtype=np.float32)
total_weight = np.zeros(N, dtype=np.float32)

views = [
    ("front", front_img, front_bb, 2, +1),
    ("back", back_img, back_bb, 2, -1),
    ("right", right_img, right_bb, 0, +1),
    ("left", left_img, left_bb, 0, -1),
]
for name, img, bb, ax, sign in views:
    cols, w = project(verts_re, vnormals_re, img, bb, ax, sign)
    if cols is None:
        continue
    total_color += cols * w[:, None]
    total_weight += w
    active = (w > 0.05).sum()
    print(f"  view {name:5}: {active:6} verts active")

mask_valid = total_weight > 1e-3
final_colors = np.full((N, 3), 100, dtype=np.uint8)
final_colors[mask_valid] = (
    total_color[mask_valid] / total_weight[mask_valid, None]
).astype(np.uint8)
missing = (~mask_valid).sum()
print(f"  missing colors: {missing}/{N} (filled with grey)")

new_mesh = trimesh.Trimesh(verts_re, faces_re, process=False)
new_mesh.visual = trimesh.visual.ColorVisuals(
    mesh=new_mesh,
    vertex_colors=np.column_stack([final_colors, np.full(N, 255, dtype=np.uint8)]),
)
new_mesh.export(out_glb)
print(f"[bake_mv_texture] OK -> {out_glb} ({os.path.getsize(out_glb)} bytes)")
PY
