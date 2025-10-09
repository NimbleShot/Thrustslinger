# ObjectPool Architecture Diagram

## System Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Unity Scene Hierarchy                    │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ObjectPool (GameObject)                                     │
│  ├─ PoolService (Component, Singleton)                       │
│  │  └─ Manages pool lifecycle, spawn/release                 │
│  └─ PoolRegistrar (Component)                                │
│     └─ Configures prefabs + prewarm counts                   │
│                                                               │
│  [Pools] (Auto-created at runtime)                           │
│  ├─ projectiles.default_Pool                                 │
│  │  ├─ Projectile_Pooled (inactive)                          │
│  │  ├─ Projectile_Pooled (inactive)                          │
│  │  └─ Projectile_Pooled (inactive) ...                      │
│  └─ targets.default_Pool                                     │
│     ├─ Target_Pooled (inactive)                              │
│     ├─ Target_Pooled (inactive)                              │
│     └─ Target_Pooled (inactive) ...                          │
│                                                               │
│  ProjectileWeapon (GameObject)                               │
│  └─ ProjectileWeapon (Component)                             │
│     └─ Calls PoolService.Get("projectiles.default")          │
│                                                               │
│  TargetSpawner (GameObject)                                  │
│  └─ TargetSpawner (Component)                                │
│     └─ Calls PoolService.Get("targets.default")              │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

## Data Flow

### 1. Initialization (Scene Load)
```
Scene Awake()
    │
    ├─→ PoolService.Awake()
    │   └─→ Sets up singleton, DontDestroyOnLoad
    │
    └─→ PoolRegistrar.Awake()
        └─→ RegisterAllPools()
            ├─→ PoolService.RegisterPrefab("projectiles.default", prefab, 16)
            │   ├─→ Create container: "projectiles.default_Pool"
            │   └─→ Prewarm: Instantiate 16 inactive copies
            │
            └─→ PoolService.RegisterPrefab("targets.default", prefab, 8)
                ├─→ Create container: "targets.default_Pool"
                └─→ Prewarm: Instantiate 8 inactive copies
```

### 2. Spawning (Runtime)
```
ProjectileWeapon.TryFire()
    │
    └─→ PoolService.Get<Projectile>("projectiles.default", context)
        │
        ├─→ Dequeue available pooled object
        ├─→ Move to root (unparent)
        ├─→ Apply spawn context (position, rotation)
        ├─→ SetActive(true)
        └─→ Call Projectile.OnSpawned(context)
            └─→ Initialize velocity, damage, lifetime
```

### 3. Release (Hit or Lifetime Expired)
```
Projectile.OnCollisionEnter()
    │
    └─→ PoolService.Release(this)
        │
        ├─→ SetActive(false)
        ├─→ Call Projectile.OnDespawned()
        │   └─→ Zero velocities, clear references
        ├─→ Re-parent to pool container
        └─→ Enqueue back to available queue
```

### 4. Scene Transition (Main Menu → Gameplay)
```
SceneManager.LoadScene("MainMenu", LoadSceneMode.Single)
    │
    └─→ PoolService.OnSceneLoaded()
        └─→ ClearAllPools()
            ├─→ Deactivate all active pooled objects
            ├─→ Call OnDespawned() on all
            ├─→ Clear all queues/sets
            ├─→ Destroy pool containers
            └─→ Keep pool registrations ✓

SceneManager.LoadScene("Gameplay", LoadSceneMode.Single)
    │
    └─→ PoolService.OnSceneLoaded()
        └─→ ClearAllPools() (again, cleans old references)

PoolRegistrar.Awake()
    │
    └─→ RegisterAllPools()
        └─→ Skips already-registered pools ✓

First Spawn:
    │
    └─→ PoolService.CreateInstance()
        ├─→ Detect container == null
        ├─→ Recreate container ✓
        └─→ Instantiate new pooled object
```

## Component Responsibilities

```
┌────────────────────────────────────────────────────────────────┐
│                        PoolService                             │
│  (Singleton, DontDestroyOnLoad)                                │
├────────────────────────────────────────────────────────────────┤
│  ✓ Manages pool lifecycle                                      │
│  ✓ Handles spawn (Get) and release (Release)                   │
│  ✓ Creates/destroys pool containers                            │
│  ✓ Clears pools on scene transitions                           │
│  ✓ Maintains _pools dictionary (key → entry)                   │
│  ✓ Maintains _lookup dictionary (GameObject → entry)           │
└────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────┐
│                       PoolRegistrar                            │
│  (Scene component, attached to ObjectPool)                     │
├────────────────────────────────────────────────────────────────┤
│  ✓ Configures all pool prefabs + prewarm counts                │
│  ✓ Registers pools on Awake()                                  │
│  ✓ Validates duplicate keys                                    │
│  ✓ Provides debug logging                                      │
│  ✗ Does NOT spawn objects                                      │
│  ✗ Does NOT manage pool lifecycle                              │
└────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────┐
│                   ProjectileWeapon / TargetSpawner             │
│  (Spawner components)                                          │
├────────────────────────────────────────────────────────────────┤
│  ✓ Spawns objects via PoolService.Get(key, context)            │
│  ✓ Provides spawn context (position, velocity, etc.)           │
│  ✗ Does NOT register pools                                     │
│  ✗ Does NOT prewarm pools                                      │
│  ✗ Does NOT manage pool containers                             │
└────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────┐
│                    Projectile / Target                         │
│  (Pooled objects, implement IPoolable)                         │
├────────────────────────────────────────────────────────────────┤
│  ✓ OnSpawned(context) - Initialize state                       │
│  ✓ OnDespawned() - Clean up state                              │
│  ✓ Calls PoolService.Release(this) when done                   │
│  ✗ Does NOT know about pool containers                         │
│  ✗ Does NOT manage pool lifecycle                              │
└────────────────────────────────────────────────────────────────┘
```

## Configuration Flow

```
Inspector (PoolRegistrar)
    │
    ├─→ Pool Entry 0
    │   ├─ Pool Key: "projectiles.default"
    │   ├─ Prefab: Projectile.prefab
    │   └─ Prewarm Count: 16
    │
    └─→ Pool Entry 1
        ├─ Pool Key: "targets.default"
        ├─ Prefab: Target.prefab
        └─ Prewarm Count: 8

              ↓ (Awake)

PoolService._pools Dictionary
    │
    ├─→ "projectiles.default" → PoolEntry
    │   ├─ Prefab: Projectile.prefab
    │   ├─ Container: projectiles.default_Pool
    │   ├─ Available: Queue<PooledObject> (16 items)
    │   └─ InUse: HashSet<PooledObject> (empty)
    │
    └─→ "targets.default" → PoolEntry
        ├─ Prefab: Target.prefab
        ├─ Container: targets.default_Pool
        ├─ Available: Queue<PooledObject> (8 items)
        └─ InUse: HashSet<PooledObject> (empty)

              ↓ (Spawn)

ProjectileWeapon
    └─ projectilePoolKey: "projectiles.default" ✓ (matches)

TargetSpawner
    └─ targetPoolKey: "targets.default" ✓ (matches)
```

## Key Matching

```
┌──────────────────┐         ┌──────────────────┐
│  PoolRegistrar   │         │ ProjectileWeapon │
│                  │         │                  │
│  poolKey =       │◄────────┤ projectilePoolKey│
│  "projectiles.   │  MUST   │  = "projectiles. │
│   default"       │  MATCH  │     default"     │
└──────────────────┘         └──────────────────┘
        │
        │ (Registers with PoolService)
        ↓
┌──────────────────┐
│   PoolService    │
│                  │
│  _pools[         │
│   "projectiles.  │
│    default"]     │
│  = PoolEntry     │
└──────────────────┘
        │
        │ (Spawner requests)
        ↓
┌──────────────────┐
│ PoolService.Get( │
│  "projectiles.   │
│   default",      │
│  context)        │
└──────────────────┘
```

## Scene Persistence

```
Persistent Scene (DontDestroyOnLoad)
    │
    └─→ ObjectPool (DontDestroyOnLoad via PoolService)
        ├─→ PoolService (persists across scenes)
        ├─→ PoolRegistrar (persists, but registrations cached in PoolService)
        └─→ [Pools] (destroyed on scene transitions, recreated on spawn)

Gameplay Scene (LoadSceneMode.Single, destroys on load)
    │
    ├─→ ProjectileWeapon (destroyed)
    ├─→ TargetSpawner (destroyed)
    ├─→ Active projectiles (destroyed)
    └─→ Active targets (destroyed)

Result:
    ✓ Pool registrations persist (in PoolService._pools)
    ✗ Pool containers destroyed (will be recreated)
    ✗ Active objects destroyed (scene unload)
    ✗ Spawner instances destroyed (scene unload)
```

---

## Summary

**Single Responsibility**:
- `PoolRegistrar` = Configuration
- `PoolService` = Pool management
- `Spawners` = Object spawning
- `Pooled Objects` = Object behavior

**Clear Hierarchy**:
```
ObjectPool
  └─ [Pools]
      ├─ pool1_Pool
      └─ pool2_Pool
```

**Simple Key Matching**:
```
PoolRegistrar.poolKey == Spawner.poolKey == PoolService._pools[key]
```

**Robust Scene Transitions**:
```
Clear pools → Keep registrations → Recreate containers → Resume spawning
```
