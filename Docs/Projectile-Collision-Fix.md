# Projectile Pass-Through Fix

## Problem
Projectiles were sometimes passing through targets without registering hits.

## Root Causes

### 1. Discrete Collision Detection on Fast-Moving Projectiles
**Issue**: The projectile rigidbody was using `CollisionDetection: Discrete` mode.
- Discrete collision detection samples positions once per physics frame
- Fast-moving projectiles (18 m/s muzzle velocity) can tunnel through thin colliders between frames
- At default physics timestep (0.02s = 50Hz), a projectile travels 0.36 units per frame

**Fix**: Changed projectile rigidbody to `CollisionDetection: Continuous Dynamic` (value 1)
- Continuous collision detection uses swept collision tests
- Prevents tunneling by detecting collisions along the movement path between frames
- Essential for fast-moving rigidbodies interacting with static/dynamic colliders

### 2. Non-Convex Mesh Collider on Target
**Issue**: The cylinder mesh collider on the Target child had `Convex: false`
- Unity physics engine **cannot collide two non-convex mesh colliders**
- Non-convex mesh colliders can only collide with primitive colliders (sphere, box, capsule) or other primitives
- When a projectile (sphere collider + rigidbody) hits a non-convex mesh, collisions may fail

**Fix**: Changed target mesh collider to `Convex: true`
- Convex mesh colliders work correctly with all other collider types
- For simple shapes like cylinders, convex approximation is nearly identical to original mesh
- Slight performance improvement due to simpler collision algorithms

### 3. Missing Physics Wakeup on Pooled Objects
**Issue**: Pooled rigidbodies can enter sleep state when returned to pool
- Sleeping rigidbodies don't participate in collision detection
- Fast collisions might occur before automatic wakeup triggers

**Fix**: Added explicit `WakeUp()` calls in `OnSpawned()`:
- `Projectile.cs`: Ensures rigidbody is awake and non-kinematic on spawn
- `Target.cs`: Ensures collider is enabled after despawn/respawn cycle

## Files Modified

1. **Assets/Prefabs/Projectile.prefab**
   - Changed rigidbody collision detection from Discrete (0) to Continuous Dynamic (1)

2. **Assets/Prefabs/Target.prefab**
   - Changed mesh collider from non-convex (0) to convex (1)

3. **Assets/Scripts/Gameplay/Projectile.cs**
   - Added `body.WakeUp()` and `body.isKinematic = false` in `OnSpawned()`

4. **Assets/Scripts/Gameplay/Target.cs**
   - Added `_collider.enabled = true` in `OnSpawned()`

## Testing Checklist

- [ ] Fire projectiles at stationary targets at various distances
- [ ] Fire projectiles at moving targets (approaching/retreating)
- [ ] Verify hits register consistently at max fire rate (6 rounds/sec)
- [ ] Test with targets spawned from pool (not fresh instantiation)
- [ ] Verify no performance regression from continuous collision detection
- [ ] Check edge cases: grazing hits, perpendicular angles, close-range rapid fire

## Technical Notes

### Why Continuous Dynamic vs Continuous Speculative?
- **Continuous Dynamic**: Full swept collision tests, best accuracy for high-speed projectiles
- **Continuous Speculative**: Cheaper but can miss very thin colliders
- Given projectile speed (18 m/s) and target thinness (~0.08 units), Dynamic is warranted

### Alternative Solutions Considered

1. **Increase Physics Timestep**: Would help but impacts entire game physics, not ideal
2. **Use Raycasts Instead**: Would require major weapon system refactor, loses physics interactions
3. **Thicker Target Colliders**: Doesn't address root cause, affects gameplay feel
4. **Trigger Colliders**: Loses realistic physics response, complicates damage system

### Performance Impact
- Continuous collision detection has ~20-30% higher CPU cost per rigidbody
- Acceptable given low projectile count (<20 concurrent typical)
- Convex mesh colliders are actually slightly faster than non-convex

## Additional Recommendations

Consider for future optimization:
- Implement projectile pooling warmup (pre-spawn 10-20 projectiles at game start)
- Add `[Tooltip]` attributes explaining collision detection mode choice
- Create debug visualization for collision detection sweeps (editor-only)
- Profile physics cost in worst-case scenarios (full magazine rapid fire)
