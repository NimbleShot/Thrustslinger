# Player-Facing Menu Positioning - Quick Reference

## What Was Added

### New Component: `PlayerFacingMenuPositioner`
A reusable component that positions UI menus in front of the player with boundary constraints.

**Location:** `Assets/Scripts/UI/PlayerFacingMenuPositioner.cs`

## Quick Setup

1. **Add to Pause Menu**:
   - Select your Pause Menu GameObject
   - Add Component → Thrustslinger → UI → Player Facing Menu Positioner
   - Enable "Position In Front Of Player"
   - Adjust "Distance From Player" (default: 2.0m)

2. **Add to Game Over Menu**:
   - Same steps as Pause Menu

3. **Configure Boundaries** (Optional but Recommended):
   - Enable "Apply Boundary Constraints"
   - Adjust "Boundary Inset" to prevent wall clipping (default: 0.5m)

## Key Features

✓ **Dynamic Positioning**: Menu appears in front of player when shown  
✓ **Natural VR Behavior**: Follows camera view direction (configurable)  
✓ **Correct Facing**: Menu always faces player (no longer inverted)  
✓ **Plane-Aligned Option**: Can align with plane normal instead of camera  
✓ **Boundary Safety**: Stays within plane bounds, won't clip through walls  
✓ **Auto-Discovery**: Finds camera and plane automatically  
✓ **Optional**: Works with or without the component  
✓ **Configurable**: Distance, offset, and safety margins adjustable  

## Inspector Quick Settings

### Natural VR Mode (Recommended - Default)
- **Position In Front Of Player**: ✓ Enable
- **Use Plane Normal Direction**: ✗ Disable (uses camera forward)
- **Face Camera**: ✓ Enable (faces camera directly)
- **Distance From Player**: 2.0
- **Apply Boundary Constraints**: ✓ Enable

### Plane-Aligned Mode
- **Position In Front Of Player**: ✓ Enable
- **Use Plane Normal Direction**: ✓ Enable (perpendicular to plane)
- **Face Camera**: ✗ Disable (respects plane orientation)
- **Lock Y Rotation**: ✓ Enable (keeps upright)
- **Distance From Player**: 2.0
- **Apply Boundary Constraints**: ✓ Enable

### For Larger Menus
- **Boundary Inset**: (1.0, 1.0) for extra safety

### For Fixed Position (Original Behavior)
- **Position In Front Of Player**: ✗ Disable

## Integration

Both `PauseMenuPresenter` and `GameOverPresenter` automatically detect and use the positioner when present. No additional code required.

## See Full Documentation

For detailed information, see: `Docs/PLAYER_FACING_MENU_POSITIONING.md`
