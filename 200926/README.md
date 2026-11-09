# FORMA + MAC — Unity 6 project

## Supported editor and dependencies

- Unity **6000.3.9f1**
- Universal Render Pipeline **17.3.0**
- glTFast **6.10.0**

Open the repository root folder `200926` as the Unity project. Unity Package Manager restores the dependencies declared in `Packages/manifest.json`.

## First import

1. Open `Assets/FORMA/Scenes/FORMA_Studio.unity`.
2. Wait for the GLB assets under `Assets/FORMA/Sources` to finish importing through glTFast.
3. Run **FORMA → Bake GLB Animations** (or **Tools → FORMA → Setup Reference Avatars**).
4. Confirm that Unity created `Assets/FORMA/Resources/FORMA/FemaleAnimated.prefab` and `MaleAnimated.prefab`.
5. Enter Play mode.

The baker creates a controller with a default state for each imported GLB, then writes the generated prefabs into `Resources/FORMA`, which is the path used by `ReferenceAvatarLoader`.

## What is included

- `Assets/FORMA/Sources/FemaleAnimated.glb` and `MaleAnimated.glb`
- FORMA scripts, scene, resources, materials and editor utilities
- MAC adapter scripts and avatar prefabs
- Unity package manifest and version settings

## Limitations to verify locally

This repository does not include every mesh dependency referenced by the supplied MAC prefabs. The FORMA scene defaults to the MAC mode when those MAC assets are available, and the GLB workflow can be selected through the FORMA UI. Verify the scene in Unity after package restoration: GitHub cannot execute Unity or validate rendering, asset import, or Play mode.

## Do not use the legacy FBX setup

The old FBX/Rigged setup was removed from the editor entry point because the project uses GLB source avatars. Use the bake command above; it is repeatable and safe to run after reimporting either GLB file.
