# Project snapshot — `tps1` (Brackeys Bundle Platformer Multiplayer)

Snapshot taken 2026-05-13 against a live Unity Editor connected via MCP.
This is *fact-state*, not aspiration — what the project actually contained
at the time of analysis. Useful for future sessions starting against the
same project so the agent doesn't re-derive everything from scratch.

## Engine

- **Editor**: Unity 6000.3.9f1 (Unity 6.3 line).
- **Path on user's machine**: `D:\Work\Unity\Projects\tps1`.
- **Project base**: Brackeys Bundle (Unity's official multiplayer sample
  templated around 3D platformer + multiplayer session UI).

## Installed packages of note

| Package | Version | Why it matters |
|---|---|---|
| `com.unity.netcode.gameobjects` | 2.9.1 | Authoritative networking layer (NGO). |
| `com.unity.services.multiplayer` | 2.1.1 | Lobby / Relay / Sessions (Unity Game Services). |
| `com.unity.transport` | 2.6.0 | UTP, used by NGO. |
| `com.unity.multiplayer.playmode` | 2.0.1 | Multi-instance Play Mode. |
| `com.unity.multiplayer.tools` | 2.2.8 | Network profiler. |
| `com.unity.cinemachine` | 3.1.5 | Camera rig for follow-cam. |
| `com.unity.inputsystem` | 1.18.0 | New Input System (not legacy `UnityEngine.Input`). |
| `com.unity.render-pipelines.universal` | — | URP rendering. |
| `com.unity.timeline` | — | Cutscene / sequence playback. |
| `com.unity.animation.rigging` | — | Procedural rig constraints. |
| `com.unity.postprocessing` | — | Volume-based post FX. |
| `com.ivan-murzak.unity-mcp` (+addons) | 0.72.x | The plugin we use to talk to the editor. |
| `com.unity.cloud.gltfast` | 6.18.0 | **Added by us 2026-05-13** to import .glb files. |

## Active scene

`Assets/Platformer/TestScenes/[BB] Platformer MultiplayerSession.unity`

13 root GameObjects, organised by dummy separator GameObjects with `---X---`
naming:

- `---Networking---`
  - `SessionBrowser` (UI Document — UI Toolkit MVVM for lobby/join UI)
  - `UnityServicesWithName`
  - `NetworkManager` (Netcode + UnityTransport)
- `---Gameplay---`
  - `Main Camera` (Camera + CinemachineBrain + URP data)
  - `EventSystem`
  - `InteractableObjects` (Teleporters ×2, MovingPlatforms ×3, Spinners ×13,
    Collectibles ×26, CircularTrampoline, Pfb_jumpPad, DamageArea,
    Gravity Volume ×2, FinishLineEffect)
  - `PlatformerEnvironment` (Lighting: 14 Spot Lights + Directional + Global
    Volume + Adaptive Probe Volume; Floors, Walls, Railings, Stairs,
    RoundColumns, GlowingBlocks, Pipe/RoundPlatforms, Fog; 3 VFX_WindTrail)
  - `SpawnPoints` (4 spawns for up to 4 players)
- `GameManager` (Blocks.Gameplay.Core.GameManager + CoreDirector +
  CinemachineImpulseSource)
- `ProbeVolumePerSceneData`

## All scenes in the project

| Scene | Purpose |
|---|---|
| `[BB] Platformer.unity` | Single-player platformer |
| `[BB] Platformer MultiplayerSession.unity` | Multiplayer platformer (current) |
| `[BB] Shooter.unity` | Single-player shooter sample |
| `[BB] Shooter MultiplayerSession.unity` | Multiplayer shooter |
| `[BB] Core.unity` | Shared core gameplay |
| `[BB] Core MultiplayerSession.unity` | Core in MP variant |
| `MultiplayerSession/JoinByBrowsing.unity` | Lobby UI |
| `MultiplayerSession/JoinByCode.unity` | Join by code UI |
| `MultiplayerSession/QuickJoin.unity` | Quick-join UI |
| `MultiplayerSession/QuickJoinDebug.unity` | Quick-join debug UI |

## User scripts (154 total)

### `Assets/Core/Scripts/Runtime` (60 scripts)

Core gameplay framework — engine-agnostic platformer + shooter primitives:

**Top-level components:**
- `GameManager`, `CoreDirector`, `CorePlayerManager`, `CorePlayerState`,
  `CoreMovement`, `CoreCameraController`, `CoreAnimator`, `CoreInputHandler`,
  `CoreHUD`, `CoreStatsHandler`

**Framework interfaces:** `IMovementAbility`, `IInteractable`, `IHittable`

**Effects & ability system:**
- `EffectBuilder`, `HitProcessor`
- Abilities: `JumpAbility`, `WalkAbility`, `DoubleJump`
- Add-ons: `Visuals`, `NamePlate`, `Interaction`
- Sound: `SoundSystem`, `SoundEmitter`, `SoundMixer`
- Stats: `StatDefinition`, `StatsConfig`
- Networking glue: `GameNetworkManager`

### `Assets/Platformer/Scripts` (20 scripts)

Platformer-specific mechanics:

- **Components**: `PlatformerLocomotionAbility` (WASD + jump with buffer +
  variable height + coroutine timing), `PlatformerMovingPlatformAbility`,
  `PlatformerHUD`, `MovementCameraFeelExtension`
- **Framework effects**: `JumpArc`, `ApplyForce`, `ApplyRepulsionForce`,
  `CameraShake`, `DoubleJump`, `FinishLine`, `GravityChange`, `Teleport`,
  `PlayVfx`, `PlaySound`, `PlayTimeline`, `ModifyStat`, `SendNotification`

### `Assets/Shooter/Scripts` (36 scripts)

Shooter module — weapons, projectiles, hit detection (separate domain
from platformer).

### `Assets/Blocks/...` (29 scripts)

UI Toolkit MVVM for session screens:

- `CreateSession`, `JoinSessionByCode`, `QuickJoin`, `SessionBrowser`,
  `PlayerList`, `PlayerCount`, `LeaveSession`, `CopySessionCode`, …

## `Assets/1/` (custom user folder)

Created by user for asset experiments:

- `grok-image-6c3f729d-cd4a-40fb-8d79-2af9671ed8bf.png` — reference photo
  the user uploaded (1010 KB Texture2D).
- `woman.glb` (**added by us 2026-05-13**) — 3D mesh generated from the
  above photo via local TripoSR (~716 KB, 17 878 verts, 35 760 tris,
  vertex colors only, no PBR).

The user also briefly had a `.mp4` here which they deleted before replacing
with the PNG.

## Console state at snapshot time

Project itself: **clean** (no compile errors, no runtime errors).

Noise: 17 errors / 3 exceptions, all from previous failed MCP connection
attempts (`token does not match`). Safe to ignore for project analysis;
not project code.

## Editor state at snapshot time

- **Not in Play Mode**.
- Currently compiling: no.
- Scene dirty bit: no (when we connected; user may have made changes since).
- Game View: not open (so `screenshot-game-view` returns "render texture
  not available" until user clicks the Game tab).

## What we changed on this project during 2026-05-13

1. Installed `com.unity.cloud.gltfast 6.18.0` package.
2. Wrote `Assets/1/woman.glb` (~716 KB).
3. Instantiated `GeneratedWoman` GameObject from the GLB into the active
   scene at SpawnPoint 1, rotated `(0,0,-90)` and scaled `2.5x` (~1.6 m
   total height with feet on the ground).

The user has **not yet saved the scene** as of the snapshot — those
runtime edits exist only in the Editor's in-memory state. Save happens
when the user explicitly tells us to.
