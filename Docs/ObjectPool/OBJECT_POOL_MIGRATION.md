# ObjectPool Migration Checklist

Use this checklist to migrate your existing Thrustslinger scenes to the new centralized ObjectPool system.

## Pre-Migration Notes

**Current Values to Record** (you'll need these):
- [ ] Open your scene with `ProjectileWeapon`
  - Current `projectilePrewarmCount` value: _______ (default was 16)
  - Current `projectilePoolKey` value: _______ (default is "projectiles.default")
  
- [ ] Open your scene with `TargetSpawner`
  - Current `targetPrewarmCount` value: _______ (default was 6-8)
  - Current `targetPoolKey` value: _______ (default is "targets.default")

## Step-by-Step Migration

### ✅ Phase 1: Create ObjectPool GameObject

- [ ] **1.1** Open your persistent/bootstrap scene (the scene that loads first and contains managers)
  - Scene name: _________________________

- [ ] **1.2** Create new empty GameObject at root
  - Right-click in Hierarchy → Create Empty
  - Name it exactly: **`ObjectPool`**

- [ ] **1.3** Add PoolService component
  - Select ObjectPool GameObject
  - Add Component → search "Pool Service"
  - Verify it appears in Inspector

- [ ] **1.4** Add PoolRegistrar component
  - With ObjectPool still selected
  - Add Component → search "Pool Registrar"
  - Verify it appears in Inspector (should be below PoolService)

- [ ] **1.5** Enable debug logging
  - PoolRegistrar Inspector → expand "Debug" section
  - Check ✓ "Log Registrations"

### ✅ Phase 2: Configure Projectile Pool

- [ ] **2.1** Expand PoolRegistrar "Pool Registrations" array
  - Click arrow next to "Pools" to expand
  - Should show "Element 0" and "Element 1" by default

- [ ] **2.2** Configure Element 0 (Projectiles)
  - **Pool Key**: Enter `projectiles.default` (or your custom key from 1.1 above)
  - **Prefab**: Drag `Assets/Prefabs/Projectile.prefab` into slot
  - **Prewarm Count**: Enter value from 1.1 above (or 16)
  - **Container Parent**: Leave as `None`

- [ ] **2.3** Verify projectile prefab assignment
  - Click the prefab slot to highlight it in Project window
  - Confirm it's the same prefab used by ProjectileWeapon

### ✅ Phase 3: Configure Target Pool

- [ ] **3.1** Configure Element 1 (Targets)
  - **Pool Key**: Enter `targets.default` (or your custom key from 1.2 above)
  - **Prefab**: Drag your target prefab into slot (find it in Project window)
  - **Prewarm Count**: Enter value from 1.2 above (or 8)
  - **Container Parent**: Leave as `None`

- [ ] **3.2** Verify target prefab assignment
  - Click the prefab slot to highlight it in Project window
  - Confirm it's the same prefab used by TargetSpawner

### ✅ Phase 4: Add More Pools (Optional)

If you have additional pooled objects (impact effects, explosions, etc.):

- [ ] **4.1** Increase array size
  - Change "Pools" → Size to 3, 4, etc.

- [ ] **4.2** For each additional pool entry:
  - **Pool Key**: Unique key (e.g., "effects.impact")
  - **Prefab**: The effect prefab
  - **Prewarm Count**: Expected concurrent instances (4-8 typical)
  - **Container Parent**: None

### ✅ Phase 5: Verify Spawner Key Matches

- [ ] **5.1** Find ProjectileWeapon GameObject in scene
  - Select it in Hierarchy
  - Expand Inspector → ProjectileWeapon component

- [ ] **5.2** Verify pool key matches
  - Look at "Projectile Pool Key" field
  - Should match PoolRegistrar Element 0 Pool Key exactly ✓
  - If different, update one or the other to match

- [ ] **5.3** Note prefab field (optional, kept for reference)
  - "Projectile Prefab" field still exists (for validation)
  - Should be same prefab as PoolRegistrar Element 0
  - You'll see inspector may show "Missing" for prewarmCount (expected - field removed)

- [ ] **5.4** Find TargetSpawner GameObject in scene
  - Select it in Hierarchy
  - Expand Inspector → TargetSpawner component

- [ ] **5.5** Verify target pool key matches
  - Look at "Target Pool Key" field
  - Should match PoolRegistrar Element 1 Pool Key exactly ✓
  - If different, update one or the other to match

- [ ] **5.6** Note prefab field (optional, kept for reference)
  - "Target Prefab" field still exists (for validation)
  - Should be same prefab as PoolRegistrar Element 1
  - You'll see inspector may show "Missing" for prewarmCount (expected - field removed)

### ✅ Phase 6: Save and Test

- [ ] **6.1** Save scene
  - File → Save or Ctrl+S

- [ ] **6.2** Enter Play Mode
  - Click Play button

- [ ] **6.3** Check Console for registration logs
  - Should see:
    ```
    [PoolRegistrar] Registered pool 'projectiles.default' with 16 prewarmed instances.
    [PoolRegistrar] Registered pool 'targets.default' with 8 prewarmed instances.
    [PoolRegistrar] Registration complete: 2 registered, 0 skipped.
    ```
  - ✓ If you see these logs: SUCCESS
  - ✗ If you see errors: Check Step 7 (Troubleshooting)

- [ ] **6.4** Check Hierarchy for pool containers
  - Expand ObjectPool → [Pools]
  - Should see:
    ```
    ObjectPool
      [Pools]
        projectiles.default_Pool
          Projectile_Pooled (inactive) x16
        targets.default_Pool
          Target_Pooled (inactive) x8
    ```

- [ ] **6.5** Test projectile firing
  - Fire weapon (trigger input)
  - Watch Hierarchy: active projectiles move to root, then return to pool
  - Check Console: no errors

- [ ] **6.6** Test target spawning
  - Wait for targets to spawn
  - Watch Hierarchy: targets move from pool → root → back to pool
  - Check Console: no errors

- [ ] **6.7** Open Profiler (Window → Analysis → Profiler)
  - CPU module → expand "Others" → look for "Instantiate" / "Destroy"
  - Fire sustained burst (hold trigger)
  - ✓ Should see NO new Instantiate calls after initial prewarm
  - ✗ If you see continuous Instantiate calls: Check Step 7

- [ ] **6.8** Test scene transition
  - Pause game → Quit to Main Menu
  - Start new game
  - ✓ Should transition smoothly, no errors
  - ✗ If you see MissingReferenceException: Check Step 7

- [ ] **6.9** Exit Play Mode
  - Click Play button again

### ✅ Phase 7: Troubleshooting

#### Problem: "No pool registered for key 'X'"

- [ ] Check PoolRegistrar has entry with matching key
- [ ] Verify key spelling/case matches exactly (case-sensitive)
- [ ] Confirm ObjectPool GameObject exists and is active
- [ ] Check PoolRegistrar component is enabled

#### Problem: Prefab slot shows empty in PoolRegistrar

- [ ] Drag prefab from Project window (not scene instance)
- [ ] Verify prefab is in Assets folder (not Library)
- [ ] Check prefab has Projectile/Target component

#### Problem: No registration logs in Console

- [ ] Check "Log Registrations" is enabled in PoolRegistrar
- [ ] Verify Console "Collapse" is unchecked (to see all logs)
- [ ] Confirm PoolRegistrar.Awake() ran (set breakpoint if needed)

#### Problem: Instantiate calls still happening (Profiler)

- [ ] Verify PoolService singleton is active
- [ ] Check pool keys match exactly
- [ ] Confirm prefabs implement IPoolable (Projectile/Target do by default)
- [ ] Check spawners aren't using Instantiate() directly (they shouldn't after migration)

#### Problem: Scene transition errors (MissingReferenceException)

- [ ] Verify ObjectPool is in persistent scene (NOT gameplay scene)
- [ ] Check PoolService has DontDestroyOnLoad (automatic)
- [ ] Review POOL_SERVICE_SCENE_FIX.md for expected behavior

#### Problem: Inspector shows "Missing" warnings on spawner components

- [ ] **This is expected** - the prewarmCount fields were removed
- [ ] Unity shows "Missing" for serialized fields that no longer exist in code
- [ ] These warnings are harmless and will disappear next time you save the scene/prefab
- [ ] If you want to clear them immediately: re-save the scene/prefab

### ✅ Phase 8: Migrate Additional Scenes (if applicable)

If you have multiple scenes (Main Menu, Gameplay, Tutorial, etc.):

- [ ] **8.1** Identify which scene is persistent
  - The scene with ObjectPool should be the **first scene loaded**
  - Usually your bootstrap/manager scene

- [ ] **8.2** For each additional scene:
  - Open the scene
  - Verify it does NOT have its own ObjectPool GameObject
  - Verify spawners reference the same pool keys as PoolRegistrar
  - Test scene transition to/from this scene

- [ ] **8.3** Multi-scene setup (Additive loading)
  - If using LoadSceneMode.Additive, ObjectPool should be in the persistent base scene
  - Additive scenes can safely have spawners (they'll use the persistent pool)

### ✅ Phase 9: Cleanup (Optional)

- [ ] **9.1** Disable debug logging (if desired)
  - PoolRegistrar → Debug → uncheck "Log Registrations"

- [ ] **9.2** Document your pool keys
  - Write down custom keys if you changed defaults
  - Add note to project README or design doc

- [ ] **9.3** Commit to version control
  - Stage modified scene file
  - Stage PoolRegistrar.cs and related scripts
  - Commit with message: "Migrate to centralized ObjectPool system"

## ✅ Migration Complete!

Once all checkboxes are ticked:
- ✓ ObjectPool GameObject created and configured
- ✓ All pools registered in PoolRegistrar
- ✓ Spawner keys verified to match
- ✓ Play mode tested successfully
- ✓ Scene transitions tested successfully
- ✓ Profiler confirms no Instantiate spikes

You're now using the centralized ObjectPool system!

## Quick Reference

**Pool Keys (defaults)**:
- Projectiles: `"projectiles.default"`
- Targets: `"targets.default"`

**Prewarm Counts (recommended)**:
- Projectiles: 16-32 (burst fire capacity)
- Targets: 6-12 (concurrent active targets)
- Effects: 4-8 (typical concurrent impacts)

**GameObject Hierarchy**:
```
ObjectPool (persistent scene root)
  └─ [Pools] (runtime only)
      ├─ projectiles.default_Pool
      └─ targets.default_Pool
```

**Key Inspector Fields**:
- PoolRegistrar: Pools array (pool configs)
- ProjectileWeapon: Projectile Pool Key (string)
- TargetSpawner: Target Pool Key (string)

---

## Need Help?

See full documentation:
- `OBJECT_POOL_QUICK_START.md` - Quick setup guide
- `OBJECT_POOL_SETUP.md` - Comprehensive guide
- `OBJECT_POOL_ARCHITECTURE.md` - System diagrams
- `OBJECT_POOL_IMPLEMENTATION.md` - Technical details

Or check the inline help:
- Right-click PoolRegistrar → "Re-register All Pools" (debug tool)
- Inspector tooltips on all PoolRegistrar fields
