# Copilot Instructions — Thrustslinger (Unity XR)

- Project snapshot: Unity 6 + URP; OpenXR + XR Interaction Toolkit (action-based rig); target Quest 3-class first. Aim for stable 72–90 Hz with Single-Pass Instanced, Dynamic Resolution, and optional Fixed Foveated Rendering.
- Repo layout (examples):
  - `Assets/Scripts/Core` bootstrap/services; `Assets/Scripts/XR` rig, locomotion, input; `Assets/Scripts/Gameplay` targets, spawner, scoring; `Assets/Scripts/UI` HUD/menus; `Assets/Scripts/Effects` haptics/audio/VFX; `Assets/Scripts/Data` ScriptableObjects; `Assets/Scripts/Tests` Edit/PlayMode tests.
  - Scenes: `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/Arena.unity`. Input: `Assets/InputSystem_Actions.inputactions` (action-based XRI).

## Architecture essentials
- Locomotion is plane-locked thrusters (no forward/backward translation). Each physics step: clamp forward velocity, apply forces from palm normals projected onto the plane. Read inputs in `Update`, apply in `FixedUpdate`.
- Shooting is hitscan from the dominant hand (prototype) with pooled impact VFX/audio. Abstract behind a weapon interface to allow projectile mode later.
- Enemies are pooled “targets” spawned over time with archetype data (speed/HP/size/breach damage/score mult). Breach events damage the player and despawn targets. Difficulty ramps continuously.
- Scoring = base × distance × accuracy bucket × combo. Combo increases on hits, resets on miss/damage.

## Pooling & Player Singleton
- Object Pooling (required on device):
  - Use a centralized PoolService to prewarm and reuse instances for: targets, impact VFX/audio, muzzle flashes, trail renderers, projectiles (future), floating score popups, and any short-lived props.
  - Never Instantiate/Destroy in the arena loop; allocate up-front in loading or a brief warmup phase. Target zero allocations in `Update`/`FixedUpdate`.
  - Pooled object lifecycle: `OnSpawned(context)` → active usage → `OnDespawned()`; objects must self-reset and unregister callbacks.
- Player Singleton:
  - Provide a single authoritative player service accessible via `Player.Instance` (or `IPlayerService` resolved from Core) marked `DontDestroyOnLoad`.
  - Responsibilities: expose XR rig references (hands/controllers, dominant hand), `IPlaneProvider`, health, `Haptics` routing, and key services wiring (Score, Combo).
  - Prefer service getters over scene lookups (no `FindObjectOfType` in gameplay code). Use the singleton to avoid cross-scene wiring for core systems.
  - Implementation: derive from the shared template `Singleton<Player>` using `Assets/Scripts/Templetes/Singleton.cs`. For other globally-accessible services (e.g., `PoolService`, `ScoreService`), consider `Singleton<T>` as well; prefer interfaces for testability and keep singletons as thin facades.

## Core contracts to follow (from AGENTS.md)
- Planar locomotion:
  - `IPlaneProvider { Vector3 Normal; Vector3 Project(Vector3 worldVec); }`
  - `ThrusterController.ApplyInput(hand, gripValue, palmNormal)`
  - `PlanarConstraint.Clamp(Rigidbody rb)`
  - Turn: `ISnapTurn`, `IContinuousTurn`
- Weapons & feedback:
  - `IWeapon.Fire(in Ray originDir)`; `HitResolver.Resolve(hit) -> HitInfo`; `Haptics.Pulse(hand, amplitude, durationMs)`
- Targets & spawning:
  - `TargetArchetype : ScriptableObject { hp, speed, size, breachDamage, scoreMult, aiParams }`
  - `SpawnWaveConfig : ScriptableObject { baseRate, mixOverTimeCurve }`
  - `Target { void OnHit(HitInfo); void OnBreach(); }`
- Score & health:
  - `ScoreService.AddKill(archetype, distance, hitOffset)`; `ComboTracker.RegisterHit()/RegisterBreak()`

- Pooling (new):
  - `IPoolable { void OnSpawned(object context); void OnDespawned(); }`
  - `PoolService { T Get<T>(string key=null); void Release(object instance); void Prewarm(string key, int count); }`
  - Keys map to prefab entries; `Prewarm` called at scene load based on config.
- Player access (new):
  - `Player.Instance` (or `IPlayerService` via Core) exposes: `Transform Head`, `Transform DominantHand`, `IPlaneProvider Plane`, `Health Health`, `Haptics Haptics`, `ComboTracker Combo`, `ScoreService Score`.
  - Use the shared template for implementation: `public sealed class Player : Singleton<Player> { ... }` from `Assets/Scripts/Templetes/Singleton.cs`.

## Conventions & patterns (project-specific)
- Update order: cache input in `Update`, consume in `FixedUpdate`. Physics-layer matrix should avoid unnecessary collisions (e.g., projectiles vs projectiles, UI vs everything).
- Comfort defaults ON: snap/smooth turn options, tunneling vignette, seated/standing toggle, recenter support. Expose via Settings UI and `ComfortConfig` SO.
- Pool everything in the arena loop (targets, VFX, audio, popups, projectiles). Avoid `Instantiate/Destroy` per-frame; aim for zero GC allocations in `Update/FixedUpdate`.
- Data-driven configs live under `Assets/Configs` as ScriptableObjects: `ThrusterConfig`, `ComfortConfig`, `TargetArchetype`, `SpawnWaveConfig`, `HapticsConfig`.
- Non-goals: no room-scale puzzles, no forward/back translation, no multiplayer/economy.
 - Singleton usage: when a MonoBehaviour needs global access (Player, PoolService bootstrap), derive from `Singleton<T>` (`Assets/Scripts/Templetes/Singleton.cs`). Prefer accessing behavior via interfaces (`IPlayerService`, `IPoolService`) for decoupling and tests; keep singletons as service locators only.

## Where to put new code
- Locomotion/input: `Assets/Scripts/XR/` (action-based XRI). Example: implement `ThrusterController` and `PlanarConstraint` here.
- Weapons/hit resolution/haptics: `Assets/Scripts/Gameplay/` and `Assets/Scripts/Effects/` (route to `Haptics` service). Pool impact FX.
- Targets/spawner/difficulty: `Assets/Scripts/Gameplay/`; define per-archetype data as SOs in `Assets/Configs/`, and reference them from spawner configs.
- Scoring/combo/health: services in `Assets/Scripts/Gameplay/` or `Core/` if shared.
- UI/HUD: world-space canvases in `Assets/Scripts/UI/`; use XR-compatible ray/laser for menus.
- Pooling system & Player singleton: `Assets/Scripts/Core/` (e.g., `PoolService`, `IPoolable`, `Player`/`PlayerService`).
  - Implement `Player` as `Singleton<Player>` using the template at `Assets/Scripts/Templetes/Singleton.cs`. If `PoolService` is a MonoBehaviour bootstrap, `Singleton<PoolService>` is acceptable; otherwise keep it as a plain service instantiated at boot.

## Build, test, and debug (developer workflow)
- Use the Unity Test Framework for EditMode/PlayMode tests under `Assets/Scripts/Tests`. Prefer small PlayMode smoke tests for locomotion, spawner timing, and scoring formulas.
- For input, keep the action-based rig consistent with `Assets/InputSystem_Actions.inputactions`. Don’t switch to device-based XRI.
- Profile on Quest: verify single-pass instancing, dynamic resolution, and keep per-frame allocations near zero; prefer GPU instancing for repeated meshes.
- Add minimal tests for pooling: prewarm creates the correct count; `Get/Release` reuses instances and does not allocate; pooled `Target` resets state on `OnDespawned`.

## Integration checklist when adding features
- ScriptableObject config created and wired; pooled objects registered/prewarmed; player singleton references assigned where needed; input bound in action maps (if needed); physics layers respected; haptics/audio routed; comfort settings honored; tests cover happy path + one edge case.
 - Use the shared singleton template (`Assets/Scripts/Templetes/Singleton.cs`) for `Player` (and optionally other global services). Ensure `DontDestroyOnLoad` is applied to the Player root.
