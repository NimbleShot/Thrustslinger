# ObjectPool Quick Setup Card

## 🎯 Goal
Centralize all pooled object configuration in one place.

## 📋 Steps

### 1. Create ObjectPool GameObject
```
Scene Hierarchy → Right-click → Create Empty
Name: "ObjectPool"
Location: Root of persistent/bootstrap scene
```

### 2. Add Components
```
ObjectPool GameObject:
  ✓ PoolService (auto-added via RequireComponent)
  ✓ PoolRegistrar
```

### 3. Configure PoolRegistrar Pools Array

#### Projectile Entry
```
Pool Key:         "projectiles.default"
Prefab:           Assets/Prefabs/Projectile.prefab
Prewarm Count:    16
Container Parent: (none)
```

#### Target Entry
```
Pool Key:         "targets.default"
Prefab:           Your target prefab
Prewarm Count:    8
Container Parent: (none)
```

### 4. Verify Spawner Keys Match

#### ProjectileWeapon Component
```
Projectile Pool Key: "projectiles.default" ✓ (must match PoolRegistrar)
```

#### TargetSpawner Component
```
Target Pool Key: "targets.default" ✓ (must match PoolRegistrar)
```

## ✅ Validation Checklist

- [ ] ObjectPool exists in persistent scene (not gameplay scene)
- [ ] PoolService component present
- [ ] PoolRegistrar component present
- [ ] PoolRegistrar has entry for `"projectiles.default"`
- [ ] PoolRegistrar has entry for `"targets.default"`
- [ ] Prefabs assigned (not null)
- [ ] Prewarm counts > 0
- [ ] Keys match exactly (case-sensitive)
- [ ] `logRegistrations` enabled on PoolRegistrar (for debugging)

## 🎮 Test in Play Mode

### Expected Hierarchy
```
ObjectPool
  [Pools]
    projectiles.default_Pool
      Projectile_Pooled (inactive) x16
    targets.default_Pool
      BasicTarget_Pooled (inactive) x8
```

### Expected Console Logs
```
[PoolRegistrar] Registered pool 'projectiles.default' with 16 prewarmed instances.
[PoolRegistrar] Registered pool 'targets.default' with 8 prewarmed instances.
[PoolRegistrar] Registration complete: 2 registered, 0 skipped.
```

### Test Actions
1. **Fire weapon** → projectiles spawn from pool (no Instantiate calls)
2. **Check Hierarchy** → active projectiles move to root, released ones return to pool
3. **Profiler** → no Instantiate/Destroy spikes during burst
4. **Quit to menu → Start new game** → no `MissingReferenceException`

## 🔧 Common Issues

| Issue | Solution |
|-------|----------|
| "No pool registered for key 'X'" | Add entry to PoolRegistrar with matching key |
| Objects not pooling | Check prefab has `Projectile`/`Target` component (implements `IPoolable`) |
| Prewarm not working | Verify prewarmCount > 0 and PoolRegistrar.Awake() ran |
| Scene transition errors | Ensure ObjectPool in persistent scene, not gameplay scene |

## 📝 Key Differences from Old Approach

| Old (Distributed) | New (Centralized) |
|-------------------|-------------------|
| Each spawner registers its pool | PoolRegistrar registers all pools |
| Prewarm counts in spawner inspector | Prewarm counts in PoolRegistrar array |
| Pool containers scattered | All under `ObjectPool/[Pools]` |
| Hard to find/tune settings | Single inspector for all pools |

## 🚀 Ready to Go?

Once configured:
- Spawners automatically use existing pools
- No code changes needed for spawning
- Easy to add new pooled objects (just add PoolRegistrar entry)
- Performance: zero Instantiate/Destroy during gameplay

---
For full documentation, see `OBJECT_POOL_SETUP.md`
