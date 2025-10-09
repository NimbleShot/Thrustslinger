# ObjectPool Setup Guide

## Overview
The Thrustslinger project now uses a **centralized ObjectPool GameObject** to manage all pooled objects (projectiles, targets, effects, etc.). This replaces the previous distributed registration approach where each spawner managed its own pool.

## Architecture Changes

### Before (Distributed)
- `ProjectileWeapon` registered its own projectile pool
- `TargetSpawner` registered its own target pool
- Each spawner had `prewarmCount` fields and pool setup logic
- Pool containers scattered in hierarchy

### After (Centralized)
- Single `ObjectPool` GameObject with `PoolService` + `PoolRegistrar` components
- All prefabs and prewarm counts configured in one place (PoolRegistrar inspector)
- Spawners only reference pool keys (strings) and spawn objects
- All pool containers appear as children of `[Pools]` under ObjectPool

## Setup Instructions

### 1. Create the ObjectPool GameObject

1. In your persistent/bootstrap scene (the scene that contains managers):
   - Create a new empty GameObject
   - Name it **"ObjectPool"**
   - Position it at the root of the scene hierarchy

2. Add the `PoolService` component:
   - Select the ObjectPool GameObject
   - Click "Add Component"
   - Search for "Pool Service"
   - Add it

3. Add the `PoolRegistrar` component:
   - With ObjectPool still selected
   - Click "Add Component"
   - Search for "Pool Registrar"
   - Add it

The `PoolService` component will:
- Mark itself as `DontDestroyOnLoad` (persists across scenes)
- Create a `[Pools]` child GameObject for pool containers
- Handle scene transitions (clears pools on Single mode scene loads per POOL_SERVICE_SCENE_FIX.md)

### 2. Configure Pool Entries in PoolRegistrar

The `PoolRegistrar` component has a `Pools` array where you define all pooled objects:

#### For Projectiles:
1. Expand the "Pool Registrations" section
2. Find or add a pool entry with:
   - **Pool Key**: `"projectiles.default"` (must match `ProjectileWeapon.projectilePoolKey`)
   - **Prefab**: Drag `Assets/Prefabs/Projectile.prefab` here
   - **Prewarm Count**: `16` (or your preferred amount)
   - **Container Parent**: Leave null (uses PoolService root)

#### For Targets:
1. Add another pool entry with:
   - **Pool Key**: `"targets.default"` (must match `TargetSpawner.targetPoolKey`)
   - **Prefab**: Drag your target prefab here (e.g., `Assets/Prefabs/Targets/BasicTarget.prefab`)
   - **Prewarm Count**: `8` (or your preferred amount)
   - **Container Parent**: Leave null

#### For Additional Pooled Objects:
Add more entries for:
- Impact effects (if using `Projectile.impactEffectPoolKey`)
- Additional projectile variants
- Different target types
- Any other pooled GameObjects

### 3. Update Spawner References

#### ProjectileWeapon
- Select your weapon GameObject (e.g., RightHand controller)
- In the `ProjectileWeapon` component:
  - **Projectile Prefab**: Still assign the prefab (for validation/reference)
  - **Projectile Pool Key**: Ensure it's `"projectiles.default"` (or matches your PoolRegistrar entry)
  - ~~**Projectile Prewarm Count**: REMOVED~~ (now configured in PoolRegistrar)

#### TargetSpawner
- Select your TargetSpawner GameObject
- In the `TargetSpawner` component:
  - **Target Prefab**: Still assign the prefab (for validation/reference)
  - **Target Pool Key**: Ensure it's `"targets.default"` (or matches your PoolRegistrar entry)
  - ~~**Target Prewarm Count**: REMOVED~~ (now configured in PoolRegistrar)

### 4. Runtime Hierarchy

When the game runs, the hierarchy will look like:

```
ObjectPool
  [Pools]                                    // Created by PoolService
    projectiles.default_Pool                // Created on first spawn
      Projectile_Pooled (inactive)
      Projectile_Pooled (inactive)
      Projectile_Pooled (inactive)
      ...
    targets.default_Pool                    // Created on first spawn
      BasicTarget_Pooled (inactive)
      BasicTarget_Pooled (inactive)
      ...
```

Active pooled objects (in use) are temporarily unparented (root level) and re-parented to their pool container when released.

## Key Benefits

1. **Single Source of Truth**: All pool configurations in one inspector
2. **Easy Tuning**: Adjust prewarm counts without hunting through spawner components
3. **Better Organization**: All pool containers grouped under ObjectPool
4. **Reduced Coupling**: Spawners don't need to know about pool registration
5. **Centralized Debug**: Toggle `logRegistrations` in PoolRegistrar to see all pool setup logs

## Pool Key Naming Convention

Use descriptive, hierarchical keys:
- `"projectiles.default"` - default projectile type
- `"projectiles.explosive"` - explosive variant
- `"targets.default"` - default target
- `"targets.fast"` - fast-moving variant
- `"effects.impact"` - impact particle effect
- `"effects.explosion"` - explosion effect

Keys are case-sensitive and must match exactly between PoolRegistrar and spawner code.

## Scene Transition Behavior

Per `POOL_SERVICE_SCENE_FIX.md`, when a scene loads in `LoadSceneMode.Single`:

1. `PoolService.OnSceneLoaded()` is called
2. All active pooled objects are deactivated and despawned
3. All pool queues/sets are cleared
4. Pool containers are destroyed
5. **Pool registrations persist** (no need to re-register)
6. Containers are automatically recreated when spawning resumes

This prevents `MissingReferenceException` errors when transitioning between gameplay and main menu.

## Troubleshooting

### Error: "No pool registered for key 'X'"
- Check that PoolRegistrar has an entry with matching `poolKey`
- Ensure the ObjectPool GameObject exists in the scene
- Verify PoolRegistrar ran `Awake()` before the spawner tried to spawn

### Pooled objects not appearing under ObjectPool
- They won't appear until first spawn (pools lazy-create containers)
- Check that spawners are actually spawning (call `TryFire()` or trigger spawn loop)
- Active objects are temporarily at root level (by design)

### Prewarm not working
- Check `prewarmCount` > 0 in PoolRegistrar
- Verify `logRegistrations` is true and check Console for registration logs
- Ensure PoolRegistrar.Awake() runs (component must be enabled)

### Scene transitions causing errors
- Ensure ObjectPool is in the **persistent/bootstrap scene**, not the gameplay scene
- If in gameplay scene, it will be destroyed on scene unload
- PoolService requires `DontDestroyOnLoad` to persist

## Migration Checklist

If migrating from the old distributed approach:

- [ ] Create ObjectPool GameObject in persistent scene
- [ ] Add PoolService component
- [ ] Add PoolRegistrar component
- [ ] Configure projectile pool entry (key, prefab, prewarm count)
- [ ] Configure target pool entry (key, prefab, prewarm count)
- [ ] Remove old prewarm count fields from ProjectileWeapon inspector values
- [ ] Remove old prewarm count fields from TargetSpawner inspector values
- [ ] Test: fire projectiles, verify pool reuse in Hierarchy
- [ ] Test: spawn targets, verify pool reuse in Hierarchy
- [ ] Test: quit to main menu → start new game → verify no errors
- [ ] Profiler test: no Instantiate/Destroy spikes during burst firing

## Code Reference

### PoolRegistrar.cs
Location: `Assets/Scripts/Core/Pooling/PoolRegistrar.cs`

Key methods:
- `RegisterAllPools()` - Called in `Awake()`, registers all entries
- `ReregisterPools()` - Context menu debug method

### PoolService.cs
Location: `Assets/Scripts/Core/Pooling/PoolService.cs`

Key methods:
- `RegisterPrefab(key, prefab, initialSize, containerParent)` - Register a pool
- `Get<T>(key, context)` - Spawn from pool
- `Release(instance)` - Return to pool
- `ClearAllPools()` - Called on scene transitions

### Updated Spawners
- `ProjectileWeapon.cs` - Removed `EnsurePoolSetup()`, `_poolRegistered`, `_poolPrewarmed`, `projectilePrewarmCount`
- `TargetSpawner.cs` - Removed `EnsurePoolSetup()`, `DoPrewarm()`, `_poolConfigured`, `_poolPrewarmed`, `targetPrewarmCount`

Both now only call `PoolService.Instance.Get<T>(poolKey, context)` to spawn.

## Advanced: Multiple Pool Variants

You can create multiple PoolRegistrar components or multiple ObjectPool GameObjects for different contexts:

### Example: Weapon-Specific Pools
```
ObjectPool_Weapons
  - PoolService (shared)
  - PoolRegistrar (weapon pools only)

ObjectPool_Environment
  - PoolService (different instance? no - singleton)
  - PoolRegistrar (environment pools)
```

**Note**: Since PoolService is a singleton, only one instance exists. Use a single ObjectPool with one PoolRegistrar that registers all pools, or use multiple PoolRegistrar components on the same GameObject (they'll all register to the same PoolService singleton).

## Performance Notes

- Prewarm counts should cover typical burst scenarios (e.g., 3-5 seconds of sustained fire)
- Pools grow dynamically if prewarm is exceeded (no hard cap by default)
- Pooled objects use `HideFlags.DontSave` containers (won't pollute saved scenes)
- Scene transitions clear pools but don't GC prefab references (registrations persist)

## Summary

The new centralized ObjectPool approach provides better organization, easier tuning, and clearer separation of concerns. Spawners spawn, PoolService manages, and PoolRegistrar configures—each component has a single responsibility.
