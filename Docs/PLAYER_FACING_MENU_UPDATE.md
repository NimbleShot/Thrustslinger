# Player-Facing Menu Positioning - Update Summary

## Problem Statement
When the player looked to the side, the menu would:
1. Position based on camera forward projection (not aligned with plane)
2. Face the plane orientation (not the camera)

This caused the menu to appear misaligned when the player's view direction didn't match the plane's orientation.

## Solution
Added two new configuration options to give control over positioning and facing behavior:

### 1. Use Plane Normal Direction (Positioning)
**Field:** `usePlaneNormalDirection` (bool)  
**Default:** `false`

**When FALSE (default - Natural VR Mode):**
- Positions menu using camera forward projection onto horizontal plane
- Menu appears in the direction the player is naturally looking
- Best for VR immersion and natural interaction

**When TRUE (Plane-Aligned Mode):**
- Positions menu along the plane's normal vector (perpendicular to plane)
- Menu always appears at a fixed position relative to the plane
- Useful for plane-centric gameplay mechanics

### 2. Face Camera Direction (Rotation)
**Field:** `faceCameraDirection` (bool)  
**Default:** `true`

**When TRUE (default - Natural VR Mode):**
- Menu rotates to face the camera directly
- Ignores plane orientation
- Always readable from player's perspective
- Uses world up vector for stability

**When FALSE (Plane-Aligned Mode):**
- Menu respects plane orientation when rotating
- Can use plane normal as up vector (when `lockYRotation` is false)
- Useful when menu should align with the game world/plane

## Code Changes

### PlayerFacingMenuPositioner.cs

#### Added/Modified Inspector Fields
```csharp
// NEW: Position control
[SerializeField] private bool usePlaneNormalDirection = false;

// CHANGED: Simplified from "facePlayer" + "faceCameraDirection" to just "faceCamera"
[SerializeField] private bool faceCamera = true;
```

#### Updated ComputeTargetPosition()
Now checks `usePlaneNormalDirection`:
- **FALSE**: Uses camera forward projection (original behavior)
- **TRUE**: Uses `planeDefinition.Normal` as forward direction

#### Updated UpdateRotation()
Now checks `faceCamera`:
- **TRUE**: Faces camera directly with world up vector
- **FALSE**: Respects plane orientation, optionally using plane normal as up vector

**Critical Fix:** Direction vector is negated (`-toCamera`) so menu front faces camera, not the back

## Usage Recommendations

### For Most VR Applications (Recommended - Default)
```
Use Plane Normal Direction: ✗ Disabled
Face Camera: ✓ Enabled
```
This gives natural VR behavior where menus appear in view and face the player correctly.

### For Plane-Centric Games
```
Use Plane Normal Direction: ✓ Enabled
Face Camera: ✗ Disabled
Lock Y Rotation: ✓ Enabled
```
This locks menus to the plane's coordinate system.

### Mixed Mode (Natural Position, Plane Facing)
```
Use Plane Normal Direction: ✗ Disabled
Face Camera: ✗ Disabled
Lock Y Rotation: ✓ Enabled
```
Menu appears in natural view direction but faces plane orientation.

### Mixed Mode (Plane Position, Camera Facing)
```
Use Plane Normal Direction: ✓ Enabled
Face Camera: ✓ Enabled
```
Menu positioned perpendicular to plane but faces camera directly.

## Technical Details

### Positioning Logic
```csharp
if (usePlaneNormalDirection && planeDefinition != null)
{
    forward = planeDefinition.Normal;
}
else
{
    forward = playerCamera.forward;
    forward.y = 0f; // Project onto horizontal plane
    forward.Normalize();
}
```

### Rotation Logic (Fixed)
```csharp
// Calculate direction TO camera
Vector3 toCamera = playerCamera.position - transform.position;

if (faceCamera)
{
    // Simple: face camera with world up
    toCamera.y = 0f;
    // NEGATE direction so menu FACES camera (not away from it)
    transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
}
else
{
    // Complex: respect plane orientation
    if (lockYRotation)
    {
        toCamera.y = 0f;
        transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
    }
    else if (planeDefinition != null)
    {
        Vector3 projectedDir = Vector3.ProjectOnPlane(toCamera, planeNormal);
        // NEGATE to face camera
        projectedDir = -projectedDir.normalized;
        transform.rotation = Quaternion.LookRotation(projectedDir, planeNormal);
    }
}
```

## Backward Compatibility & Breaking Changes

### New Defaults (Natural VR Behavior)
- `usePlaneNormalDirection = false` (camera forward)
- `faceCamera = true` (face camera directly)

### Simplified Interface (Breaking Change)
**REMOVED:**
- `facePlayer` (bool) - was redundant
- `faceCameraDirection` (bool) - was redundant

**REPLACED WITH:**
- `faceCamera` (bool) - single unified control

**Migration:** If you had custom settings:
- Old `facePlayer = true` + `faceCameraDirection = true` → New `faceCamera = true`
- Old `facePlayer = true` + `faceCameraDirection = false` → New `faceCamera = false`

### Critical Bug Fix
**Inverted Menu Issue:** Menus were facing backward when "Face Player" was enabled. Fixed by negating the direction vector in `Quaternion.LookRotation()`.

## Testing Checklist
- [ ] Menu appears in front when looking forward
- [ ] Menu appears in front when looking to the side
- [ ] **Menu faces camera correctly (NOT inverted/backward)**
- [ ] Menu text is readable from player position
- [ ] Menu aligns with plane when usePlaneNormalDirection = true
- [ ] Menu respects plane rotation when faceCamera = false
- [ ] Boundary constraints still work in all modes
- [ ] Debug gizmos visualize correctly
- [ ] No console warnings about missing fields

## Files Modified
1. `Assets/Scripts/UI/PlayerFacingMenuPositioner.cs` - Added fields and updated logic
2. `Docs/PLAYER_FACING_MENU_POSITIONING.md` - Updated full documentation
3. `Docs/PLAYER_FACING_MENU_POSITIONING_QUICK_REF.md` - Updated quick reference

## Performance Impact
None - these are simple boolean checks that only execute on menu show (not per-frame).
