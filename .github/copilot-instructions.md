# Thrustslinger Unity VR Project – AI Coding Agent Instructions

## Project Overview
Thrustslinger is a VR arena shooter featuring **planar locomotion** (thrust-based movement constrained to a 2D plane in 3D space). Players defend against targets using hitscan or projectile weapons while moving via hand-grip thruster controls.

## Core Architecture

### Lifecycle Management (Core.GameManagement)
- **GameManager** (Singleton): Single authority orchestrating all game state transitions (`Boot → MainMenu → Playing → Paused → GameOver → Results`).
  - Gates ALL gameplay systems via `MonoBehaviour[]` arrays (weapons, spawners, haptics) – systems are **disabled unless `State == Playing`**.
  - Owns timers: `RunTimeSeconds` (pause-safe), `DifficultyTimeSeconds` (excludes warmup).
  - Uses **interface-based service resolution** (`IRunScoreService`, `IPlayerHealth`, `IComboTracker`) from inspector-assigned `MonoBehaviour` fields.
  - Scene bindings applied via **GameSceneBootstrapper** which wires scene-specific UI/systems on load.

- **RunContext**: Player-selected parameters (mode, seed, comfort, difficulty) passed from MainMenu to GameManager.StartRun(). Cloneable for restarts.

- **RunSummary**: Post-run aggregation (score, kills, accuracy, kill log) built by score service, cached via `MenuRunContextStore` for results screen.

### Object Pooling (Core.Pooling)
- **PoolService** (Singleton): Zero-allocation spawn/despawn for projectiles and targets. Register prefabs with string keys before use.
- **IPoolable** interface: `OnSpawned(object context)` / `OnDespawned()` – context objects (`ProjectileSpawnContext`, `TargetSpawnContext`) provide type-safe spawn params.
- Pattern: Always return pooled objects via `PooledObject.Release()` – never `Destroy()` or disable directly.

### Planar Locomotion System (XR.Locomotion)
- **IPlaneProvider**: Defines a world-space plane via `Normal`, `PlanePoint`, `Project(v)`. Implementation: `StaticPlaneProvider` (fixed orientation).
- **ThrusterController**: Reads grip input from both hands, applies force along palm axis projected onto plane. Uses `ThrusterConfig` ScriptableObject for tuning.
- **PlanarConstraint**: Rigidbody component that removes velocity perpendicular to plane and corrects position drift each FixedUpdate.
- **PlanarRectBounds**: Clamps rigidbody position to rect bounds on the plane (arena walls). Also used by `TargetSpawner` to define spawn area.
- Design rule: All movement (player, targets) projects onto the plane – no free 3D flight.

### Gameplay Loop (Gameplay)
- **TargetSpawner**: Dynamically adjusts spawn delay/spread/speed over `DifficultyTimeSeconds`. Queries plane provider to spawn targets at `spawnDistance` in front of player. Tracks `_activeTargets` for concurrent limits.
- **Target**: Poolable with `TargetMover` component. On hit → reports kill to `GameManager.NotifyKill()` → despawns via pool. On breach → applies damage to player via `GameManager.NotifyPlaneBreach()`.
- **TargetMover**: Advances toward player plane along `-Normal` at fixed speed. Triggers `Target.OnBreach()` when signed distance ≤ `breachOffset`.
- **Weapons**: Two systems (both gated by GameManager):
  - `HitscanWeapon`: Raycasts from muzzle/hand, applies instant damage to Target colliders.
  - `ProjectileWeapon`: Spawns pooled `Projectile` rigidbodies with inherited carrier velocity. Magazine + reload mechanics.

## Project Conventions

### Namespace Structure
- `Thrustslinger.Core`: Game lifecycle, pooling, singletons.
- `Thrustslinger.Gameplay`: Weapons, targets, spawners.
- `Thrustslinger.XR`: Locomotion, plane providers, VR-specific systems.
- `Thrustslinger.UI`: HUD/menu presenters (use TMPro for text).

### Component Design Patterns
1. **Inspector-Based Interface Resolution**: Serialize `MonoBehaviour` fields, cast to interface in `Awake()`. Example: `[SerializeField] private MonoBehaviour planeProviderBehaviour; _plane = planeProviderBehaviour as IPlaneProvider;`
2. **DisallowMultipleComponent**: Used on all custom MonoBehaviours to prevent duplicate manager/system instances.
3. **Singleton Services**: `GameManager`, `PoolService` inherit from `Singleton<T>` base class (`Assets/Scripts/Templetes/Singleton.cs`).

### Lifecycle Gates
- Never call `enabled = true` on gameplay systems directly – let GameManager control via `SetGatedSystemsActive()`.
- Systems requiring GameManager events: subscribe in `OnEnable()`, unsubscribe in `OnDisable()`.
- Example: `_gameManager.OnStateChanged += HandleStateChanged;`

### Input Handling
- Use Unity's new Input System (`InputActionProperty`, `InputActionReference`).
- Weapons/thruster provide fallback `InputAction` creation if inspector references are null (for fast prototyping).
- XR Input: Map to `<XRController>{LeftHand}/{Grip}` or use XRI's `ActionBasedController` activate actions.

### ScriptableObject Configuration
- Tuning data stored in SO assets (e.g., `ThrusterConfig` in `Assets/Configs/`).
- Use `[CreateAssetMenu(menuName = "Thrustslinger/...")]` for discoverability.

### Debug Tooling
- Most systems expose `[Header("Debug")]` serialized flags (`showDebugStartButton`, `drawGizmos`, `logSpawns`).
- GameManager includes `[ContextMenu("Start Run (Debug)")]` for editor testing.

## Key Integration Points

### Adding a New Weapon System
1. Implement firing logic in `Gameplay` namespace MonoBehaviour.
2. Add to GameManager's `weaponSystems[]` inspector array.
3. Subscribe to `GameManager.OnStateChanged` to handle pause/resume if needed.
4. Use `InputActionReference` for trigger input; provide fallback for editor testing.

### Extending the Pooling System
1. Register prefab via `PoolService.RegisterPrefab(key, prefab, prewarmCount)` in `Awake()`.
2. Implement `IPoolable` on the pooled component.
3. Spawn: `PoolService.Instance.Get<T>(key, position, rotation, context)`.
4. Despawn: `GetComponent<PooledObject>().Release()` (never `Destroy()`).

### Custom Score/Health Services
- Implement `IRunScoreService` or `IPlayerHealth` in a MonoBehaviour.
- Assign to GameManager's `scoreServiceBehaviour` / `playerHealthBehaviour` inspector fields.
- GameManager calls lifecycle methods (`BeginRun()`, `ResetScore()`, `FinalizeRun()`) automatically.

## Critical Gotchas
- **Plane Coordinate System**: Always use `IPlaneProvider.Project(v)` for in-plane vectors. Don't assume Y=up – plane can have arbitrary rotation.
- **Pool Context Objects**: Must be allocated once and reused (e.g., `TargetSpawner._spawnContext`). Don't new() per spawn.
- **Time.unscaledDeltaTime**: Used by GameManager run timer to handle pause with `Time.timeScale = 0` (controlled by `pauseUsesTimeScale` setting).
- **Scene Bindings**: Multi-scene setups require `GameSceneBootstrapper` in gameplay scenes to wire UI/systems to persistent GameManager singleton.

## Files to Reference
- Lifecycle orchestration: `Assets/Scripts/Core/GameManagement/GameManager.cs` (762 lines)
- Pooling service: `Assets/Scripts/Core/Pooling/PoolService.cs`
- Plane locomotion: `Assets/Scripts/XR/Locomotion/ThrusterController.cs`, `IPlaneProvider.cs`
- Spawner with difficulty curve: `Assets/Scripts/Gameplay/TargetSpawner.cs`
- Service contracts: `Assets/Scripts/Core/GameManagement/LifecycleContracts.cs`
