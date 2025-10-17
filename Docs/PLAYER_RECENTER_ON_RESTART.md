# Player Recenter on Restart - Implementation Summary

## Overview
Players are now automatically recentered to the middle of the play area when restarting a run. This ensures a consistent starting position regardless of where the player physically moved or drifted during the previous game session.

## Implementation Details

### Modified Files

#### GameManager.cs
**Location:** `Assets/Scripts/Core/GameManagement/GameManager.cs`

**Changes:**
1. **Added Field:**
   ```csharp
   [SerializeField] private PlayerPlaneDefinition playerPlaneDefinition;
   ```
   - Inspector reference to the PlayerPlaneDefinition component
   - Used to calculate the center point of the play area

2. **Implemented RecenterRig() Method:**
   - Calculates the world-space center of the plane bounds
   - Uses PlayerPlaneDefinition's PlanePoint, Normal, and CenterOffset
   - Computes plane axes (X and Y) to properly position in plane space
   - **Temporarily sets rigidbody to kinematic for clean teleport**
   - Moves the player rigidbody to the center position
   - Clears linear and angular velocities
   - Restores original kinematic state

3. **Integration:**
   - `RecenterRig()` is called in `BeginRunRoutine()` (not in `Restart()`)
   - Called AFTER `UnfreezePlayerMovement()` to ensure rigidbody is dynamic
   - Uses temporary kinematic state during teleport to prevent physics interference
   - Works for both initial game start and restart scenarios

## How It Works

### Recenter Flow
1. Player dies and sees game over menu
2. Player clicks "Restart" button
3. `GameManager.Restart()` is called → starts `BeginRunRoutine()`
4. `BeginRunRoutine()`:
   - Disables gated systems
   - Resets services (score, health, combo)
   - **`UnfreezePlayerMovement()`** - Restores rigidbody to dynamic
   - **`RecenterRig()`** - Teleports player to center (with temporary kinematic state)
   - Prewarms pools and settles physics
   - Enables gated systems and starts gameplay
5. Player starts from center position with zero velocity

### Position Calculation
The center position is calculated using:
- **Base Point:** `PlayerPlaneDefinition.PlanePoint` (world-space anchor)
- **Plane Axes:** Computed from plane normal and reference transform
- **Offset:** `CenterOffset` applied along plane axes
- **Formula:** `centerPos = planePoint + xAxis * offset.x + yAxis * offset.y`

This ensures the player is positioned at the true center of the rectangular play area, accounting for any configured offsets.

### Kinematic Teleport Pattern
To ensure reliable positioning without physics interference:
1. Store original `isKinematic` state
2. Set `isKinematic = true` (prevents physics from moving the rigidbody)
3. Clear velocities and set position
4. Restore original `isKinematic` state

This pattern guarantees the player teleports exactly to the target position.

## Configuration

### Inspector Setup
1. Select the GameManager GameObject in the scene
2. In the "Subsystem References" section, assign:
   - **Player Plane Definition:** Reference to the PlayerPlaneDefinition component
   - **Thruster Controller:** Reference to the ThrusterController (already required)

### Notes
- If `playerPlaneDefinition` is not assigned, recentering silently fails (no error)
- Recentering occurs even if player position was already centered
- Compatible with all plane orientations (not restricted to Y-up planes)

## Technical Benefits

### Consistency
- Every restart begins from the same position
- Eliminates drift accumulation across multiple runs
- Predictable starting position for difficulty balancing

### Player Experience
- No need to physically reposition in room between runs
- Works with seated or standing play styles
- Prevents spawning near walls or boundaries

### Architecture
- Uses existing PlayerPlaneDefinition as single source of truth
- No new dependencies or systems required
- Respects plane orientation and coordinate system

## Integration with Existing Systems

### Movement Lock System
Recentering works with the existing movement freeze system:
- `FreezePlayerMovement()` - Called on game over, sets rigidbody to kinematic
- `UnfreezePlayerMovement()` - Called in BeginRunRoutine, restores dynamic state
- `RecenterRig()` - Called after unfreeze, uses temporary kinematic state for teleport

### Sequence in BeginRunRoutine
```csharp
UnfreezePlayerMovement();  // Make rigidbody dynamic
RecenterRig();             // Teleport to center (temporarily kinematic)
// ... pool warming and physics settling ...
SetGatedSystemsActive(true); // Enable gameplay systems
```

### Plane Constraint System
- Recentering positions player within `PlanarRectBounds`
- No additional clamping needed (position is guaranteed in bounds)
- Works with `PlanarConstraint` for ongoing drift correction

## Key Design Decisions

### Why Recenter in BeginRunRoutine Instead of Restart?
- **Single Responsibility:** BeginRunRoutine handles all run initialization
- **Avoids Duplication:** BeginRunRoutine is used by both StartRun() and Restart()
- **Consistent Timing:** Recentering happens in same place for all run starts
- **Physics Settling:** Allows physics to settle after recentering before gameplay

### Why Use Temporary Kinematic State?
- **Clean Teleport:** Prevents physics from interfering with position change
- **No Interpolation:** Instant position change without smooth movement
- **Reliable:** Works regardless of current velocities or forces
- **Restored State:** Original kinematic state is preserved after teleport

## Future Enhancements (Optional)

### Possible Additions
1. **Rotation Reset:** Reset player facing direction to plane forward
2. **Recenter on Menu:** Option to recenter when returning to main menu
3. **Animation:** Smooth lerp/fade transition instead of instant teleport
4. **Configuration:** Inspector toggle to enable/disable recentering
5. **XR Offset:** Reset XR camera offset relative to rig origin

These are not currently implemented but could be added if desired.

## Testing Notes

### Validation Checklist
- ✅ Player recenters to middle of play area on restart
- ✅ Works with any CenterOffset configuration
- ✅ Compatible with all plane orientations
- ✅ No velocity carried over from previous run
- ✅ Works with frozen/unfrozen rigidbody state
- ✅ No errors if references are missing (graceful degradation)

### Test Scenarios
1. **Basic Restart:** Die, restart - verify center position
2. **Drift Test:** Drift to edge, die, restart - verify returns to center
3. **Multiple Restarts:** Restart multiple times - verify consistent position
4. **Offset Test:** Change CenterOffset - verify correct offset applied
5. **Missing Reference:** Unassign playerPlaneDefinition - verify no errors

## Related Documentation
- **Movement Freeze:** See `PLAYER_MOVEMENT_FREEZE.md` for movement locking details
- **Plane System:** See `PlayerPlaneDefinition-Implementation.md` for plane architecture
- **Game Lifecycle:** See project instructions for GameManager state machine
