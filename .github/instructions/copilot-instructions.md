# Copilot Instructions — Thrustslinger (Unity XR)

- Project snapshot: Unity 6 + URP; OpenXR + XR Interaction Toolkit (action-based rig); target Quest 3-class first. Aim for stable 72–90 Hz with Single-Pass Instanced, Dynamic Resolution, and optional Fixed Foveated Rendering.
- Repo layout (examples):
  - `Assets/Scripts/Core` bootstrap/services; `Assets/Scripts/XR` rig, locomotion, input; `Assets/Scripts/Gameplay` targets, spawner, scoring; `Assets/Scripts/UI` HUD/menus; `Assets/Scripts/Effects` haptics/audio/VFX; `Assets/Scripts/Data` ScriptableObjects; `Assets/Scripts/Tests` Edit/PlayMode tests.
  - Scenes: `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/Arena.unity`. Input: `Assets/InputSystem_Actions.inputactions` (action-based XRI).

## Architecture essentials
- Locomotion is plane-locked thrusters (no forward/backward translation). Each physics step: clamp forward velocity, apply forces from palm normals projected onto the plane. Read inputs in `Update`, apply in `FixedUpdate`.
- Shooting is hitscan from the dominant hand (prototype) with pooled impact VFX/audio. Abstract behind a weapon interface to allow projectile mode later.
- Enemies are pooled “targets” spawned over time with archetype data (speed/HP/size/breach damage/score mult). Breach events damage the player and despawn targets. Difficulty ramps continuously.
- Scoring = base × distance × accuracy bucket × multiplier. multiplier increases on each 10 consecutive hits untill x5, resets on miss/damage.

## Pooling & Player Singleton
- Object Pooling (required on device):
  - Use the centralized `PoolService` (singleton) to prewarm and reuse instances for: targets, impact VFX/audio, muzzle flashes, trail renderers, projectiles (future), floating score popups, and any short-lived props.
  - Never `Instantiate`/`Destroy` in the arena loop; allocate up-front during scene load or a brief warmup phase. Target zero allocations in `Update`/`FixedUpdate`.
  - Pooled object lifecycle: `OnSpawned(context)` → active usage → `OnDespawned()`; objects must self-reset and unregister callbacks.
  - Pools are registered under a hidden `[Pools]` root with per-key containers; pooling APIs are meant for runtime only (`Application.isPlaying`).
  - APIs (implemented):
    - `IPoolable { void OnSpawned(object context); void OnDespawned(); }`
    - `IPoolSpawnContext { void ApplySpawnTransform(Transform instanceTransform); }`
    - `PoolService` (`Singleton<PoolService>` implementing `IPoolService`):
      - `T Get<T>(string key = null, object context = null) where T : Component`
      - `GameObject Get(string key = null, object context = null)`
      - `void Release(object instance)`
      - `void Prewarm(string key, int count)`
      - `void RegisterPrefab(string key, GameObject prefab, int initialSize = 0, Transform containerParent = null)`
      - `bool Contains(string key)`
  - Typical spawner usage:
    - Register in `Awake/OnEnable`: `PoolService.Instance.RegisterPrefab("targets.default", targetPrefab, 0, transform);`
    - Prewarm: `PoolService.Instance.Prewarm("targets.default", prewarmCount);`
    - Spawn: `PoolService.Instance.Get<Target>("targets.default", new TargetSpawnContext { Position=pos, Rotation=rot, Parent=parent, Plane=plane, AssignPlane=true, SpeedOverride=speed });`
    - Despawn from gameplay: `PoolService.Instance.Release(instance)` or via the component’s own `Despawn()` method.
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

- Pooling (implemented):
  - `IPoolable { void OnSpawned(object context); void OnDespawned(); }`
  - `IPoolSpawnContext { void ApplySpawnTransform(Transform instanceTransform); }`
  - `PoolService` (`IPoolService`): `Get<T>(key, context)`, `Get(key, context)`, `Release(instance)`, `Prewarm(key, count)`, `RegisterPrefab(key, prefab, initialSize = 0, containerParent = null)`, `Contains(key)`.
  - Keys map to prefab entries; `Prewarm` called at scene load or spawner enable based on config.
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
- Pooling system & Player singleton: `Assets/Scripts/Core/` (e.g., `Core/Pooling`: `PoolService`, `IPoolService`, `PooledObject`, `IPoolable`, `IPoolSpawnContext`; and `Player`/`PlayerService`).
  - Implement `Player` as `Singleton<Player>` using the template at `Assets/Scripts/Templetes/Singleton.cs`. `PoolService` is a `Singleton<PoolService>` MonoBehaviour with a hidden `[Pools]` root.
  - Spawners typically expose `targetPoolKey`, `targetPrewarmCount`, and `spawnParent`; register and prewarm in `Awake/OnEnable`.

## Build, test, and debug (developer workflow)
- Use the Unity Test Framework for EditMode/PlayMode tests under `Assets/Scripts/Tests`. Prefer small PlayMode smoke tests for locomotion, spawner timing, and scoring formulas.
- For input, keep the action-based rig consistent with `Assets/InputSystem_Actions.inputactions`. Don’t switch to device-based XRI.
- Profile on Quest: verify single-pass instancing, dynamic resolution, and keep per-frame allocations near zero; prefer GPU instancing for repeated meshes.
- Add minimal tests for pooling: prewarm creates the correct count; `Get/Release` reuses instances and does not allocate; transform context applies correctly; pooled `Target` resets HP and `TargetMover` plane/speed on `OnSpawned/OnDespawned`.

## Integration checklist when adding features
- ScriptableObject config created and wired; pooled objects registered/prewarmed; player singleton references assigned where needed; input bound in action maps (if needed); physics layers respected; haptics/audio routed; comfort settings honored; tests cover happy path + one edge case.
 - Use the shared singleton template (`Assets/Scripts/Templetes/Singleton.cs`) for `Player` (and optionally other global services). Ensure `DontDestroyOnLoad` is applied to the Player root.
  - Pooling integration checklist: pools registered (key→prefab), prewarmed to expected capacity, spawners request via `PoolService.Get<T>(key, context)`, despawn via `PoolService.Release(obj)` or `Target.Despawn()`, and avoid pool calls in edit mode.

## Game Manager Lifecycle (Current Implementation)
The authoritative run lifecycle is owned by `GameManager` located at
`Assets/Scripts/Core/GameManagement/GameManager.cs`. All gameplay systems must respect its state.

### States
Boot → MainMenu → Playing ↔ Paused → GameOver → Results

`Results` can transition to `Playing` (Restart) or back to `MainMenu` (Quit). Only the manager
changes state; other systems listen and react.

### Public API
- `StartRun(RunContext)` – Begin a new run from MainMenu/Results.
- `Pause()` / `Resume()` – Toggle paused state (optionally sets `Time.timeScale=0`).
- `EndRun()` – Force end (health depletion, manual, etc.).
- `Restart()` – Re-run last `RunContext` from Results/GameOver.
- `QuitToMenu()` – Abort run and return to MainMenu.
- `UpdateMenuContext(RunContext)` – Persist comfort/difficulty/menu selections before a run.
- `RegisterKill(RunKillData)` – Forward kills to score service (future implementation hook).
- `NotifyPlaneBreach(float damage)` – Apply breach damage + combo break & breach count.

### Events & Observability
- `OnStateChanged(old, new)` – Subscribe to know when to self-enable/disable.
- `OnRunStarted(RunContext)` – Raised after warmup when gameplay actually begins.
- `OnRunCompleted(RunSummary)` – Raised once per run after `EndRun()` finalizes score.
- UnityEvents in inspector: `onStateEntered`, `onResultsReady` (for UI wiring without code).

### Gated Systems
Spawner (`TargetSpawner`), thrusters (`ThrusterController`), weapon scripts, haptics routers, and
any additional gameplay Monobehaviours are enabled only while `State == Playing` (or intentionally
resumed from Paused). To integrate a new runtime component that should be active only during the
run, add it to one of:
- `weaponSystems`
- `additionalGameplaySystems`
- `hapticsSystems`
or introduce a new serialized list in `GameManager` if a distinct category is needed.

Gameplay components should not self-start in `Awake`/`OnEnable`; rely on being enabled by the
manager. If a component must know state immediately on start-up, query `GameManager.Instance.State`.

### Run & Difficulty Timing
- `RunTimeSeconds` – Unscaled time while in Playing (excludes pause + pre-warmup).
- `DifficultyTimeSeconds` – `RunTimeSeconds - warmupSeconds` (never negative). Use for dynamic
  difficulty curves.

### Warmup
During `StartRun` a warmup sequence executes: (a) optional pool prewarm passes, (b) physics settle
delay, (c) comfort settings application & recenter. Only after this does `Playing` state begin.

### Debug Start (Temporary Before Real Menu)
While building the actual Main Menu, a temporary start control exists:
- Inspector toggles: `showDebugStartButton`, `allowDebugStartHotkey`.
- On-screen GUI button (Game view only) or hotkey (default F5) when in `MainMenu` or `Results`.
- Context menu item: right-click the `GameManager` component → `Start Run (Debug)`.
Remove or disable these once the real menu flow is implemented.

### Health Integration
`GameManager` optionally resolves an `IPlayerHealth` (see `PlayerHealth` implementation) via the
`playerHealthBehaviour` slot. On plane breach, `NotifyPlaneBreach` applies damage; health change
events break combo on any damage; depletion triggers `EndRun()` exactly once. If no health service
is assigned, breaches only break combo (no GameOver).

### Breach Tracking
Each breach increments an internal counter `_breachCount`; stored in `RunSummary.breaches` on
finalization. Use this for analytics, difficulty adjustments, or end-screen breakdown.

### Scoring / Combo / Haptics (Interfaces)
The manager talks to optional services via interfaces:
- `IRunScoreService` – `BeginRun`, `RegisterKill`, `BuildSummary`, `FinalizeRun`, `SubmitResults`.
- `IComboTracker` – `ResetCombo`, `BreakCombo`.
- `IRunHapticsRouter` – `SetGameplayEnabled(bool)`.
Future feature work should implement these separately and assign via inspector; avoid baking
scoring logic into `GameManager` directly.

### Adding New Gameplay Systems – DOs & DON'Ts
DO:
- Subscribe to `OnStateChanged` and enable/disable internal behaviour accordingly if not already
  in a gated list.
- Use `RunTimeSeconds` / `DifficultyTimeSeconds` for progressive scaling.
- Query `CurrentRun` for difficulty or comfort settings instead of storing duplicates.

DON'T:
- Manually change `GameManager.State` (always call the public API).
- Start coroutines that assume continuous execution across pause without checking state.
- Apply damage directly to `PlayerHealth`; funnel through `GameManager.NotifyPlaneBreach` unless
  it is a non-breach damage source (then still consider centralizing for consistency).

### UI / HUD Integration
HUD elements should listen to `OnStateChanged` to hide when not Playing/Paused. Results / pause /
main menu panels are toggled centrally; avoid duplicate show/hide logic. For temporary prototyping,
hook scoreboard panels into `onResultsReady` UnityEvent.

### Migration Path to Real Menu
When implementing the proper Main Menu:
1. Build UI that edits a `RunContext` clone (using existing `MenuContext`).
2. Call `GameManager.UpdateMenuContext(newContext)` when the player changes settings.
3. Invoke `StartRun` on play button.
4. Disable `showDebugStartButton` & hotkey.

### Testing Guidelines
- Add PlayMode tests validating: state transitions, pause/resume time exclusion, health depletion
  triggers GameOver, breach counter increments, and gated components disabled outside Playing.
- Mock or lightweight stub of `IRunScoreService` to verify `FinalizeRun` is called once.

### Future Extensions (Reserved Hooks)
- Regeneration: implement on `PlayerHealth` and ensure regeneration suspends while Paused.
- Difficulty injection: spawners to read `GameManager.DifficultyTimeSeconds` instead of `Time.time`.
- Persistence: `LoadMenuContext` / `SaveMenuContext` placeholders currently no-op.
