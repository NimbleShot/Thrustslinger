# Player-Facing Menu Positioning System

## Overview
The `PlayerFacingMenuPositioner` component allows UI menus (Pause and Game Over) to dynamically position themselves in front of the player's view when they appear, instead of staying at a fixed world position. The system includes boundary constraints based on the player's movement plane to prevent menus from clipping through walls.

## Component Details

### PlayerFacingMenuPositioner
**Location:** `Assets/Scripts/UI/PlayerFacingMenuPositioner.cs`  
**Namespace:** `Thrustslinger.UI`

A reusable component that positions any UI menu in front of the player camera with configurable distance and boundary constraints.

#### Features
- **Dynamic Positioning**: Places menu in front of player at configurable distance
- **Boundary Constraints**: Clamps menu position within plane bounds with adjustable insets
- **Auto-Discovery**: Automatically finds player camera and plane definition if not assigned
- **Vertical Offset**: Adjustable height offset from camera eye level
- **Face Player**: Optional rotation to always face the player
- **Debug Gizmos**: Visual debugging in Scene view

## Setup Instructions

### For Pause Menu
1. Select your Pause Menu UI GameObject in the hierarchy
2. Add the `PlayerFacingMenuPositioner` component
3. Configure in Inspector:
   - **Position In Front Of Player**: Enable this to activate positioning
   - **Distance From Player**: Set desired distance (default: 2.0m)
   - **Vertical Offset**: Adjust height relative to camera (default: 0.0m)
   - **Apply Boundary Constraints**: Enable to keep menu within plane bounds
   - **Boundary Inset**: Adjust safety margin from walls (default: 0.5m, 0.5m)

The component will automatically:
- Find `Camera.main` as the player camera
- Find `PlayerPlaneDefinition` for boundary constraints
- Update menu position when pause state is entered

### For Game Over Menu
Same setup as Pause Menu. The `GameOverPresenter` will automatically call `UpdatePosition()` when the game over state is entered.

## Inspector Parameters

### Player Reference
- **Player Camera** (Transform, Optional): Reference to VR camera/head transform. Auto-finds `Camera.main` if null.

### Positioning
- **Position In Front Of Player** (bool): Toggle to enable/disable dynamic positioning. If false, menu stays at original position.
- **Use Plane Normal Direction** (bool): If true, positions menu along plane normal (perpendicular to plane). If false, uses camera forward projection for natural view direction. Default: false
- **Distance From Player** (float): Distance from camera to place menu, in meters. Default: 2.0
- **Vertical Offset** (float): Offset from camera eye level. Positive = up, negative = down. Default: 0.0

### Boundary Constraints
- **Plane Definition** (PlayerPlaneDefinition, Optional): Reference to plane for bounds. Auto-finds if null.
- **Apply Boundary Constraints** (bool): Enable to clamp menu within plane boundaries.
- **Boundary Inset** (Vector2): Safety margin from plane edges (meters). Prevents wall clipping. Default: (0.5, 0.5)

### Orientation
- **Face Camera** (bool): If true, menu faces camera directly (natural VR behavior). If false, menu orientation respects plane alignment. Default: true
- **Lock Y Rotation** (bool): Keep menu upright (world up axis). Only applies when Face Camera is false. Default: true

### Debug
- **Draw Debug Gizmos** (bool): Show visualization in Scene view. Default: true
- **Gizmo Color** (Color): Color for debug visualization. Default: Yellow

## Integration with Existing Systems

### GameOverPresenter
The `GameOverPresenter` automatically integrates with `PlayerFacingMenuPositioner`:
- Detects positioner component in `Awake()`
- Calls `UpdatePosition()` when entering GameOver state via `SetPanelVisible()`

### PauseMenuPresenter
The `PauseMenuPresenter` automatically integrates with `PlayerFacingMenuPositioner`:
- Detects positioner component in `Awake()`
- Subscribes to `GameManager.OnStateChanged` event
- Calls `UpdatePosition()` when entering Paused state

### PlayerPlaneDefinition
The positioner reads boundary information from `PlayerPlaneDefinition`:
- **HalfExtents**: Maximum dimensions of the plane
- **CenterOffset**: Center point offset on the plane
- **Normal**: Plane orientation
- **PlanePoint**: Plane origin point

Boundaries are automatically applied with configurable insets to create a safe zone that accounts for menu size and distance from player.

## Coordinate System

The positioner uses the planar coordinate system defined by `PlayerPlaneDefinition`:
1. Gets plane normal, X-axis, and Y-axis from the plane definition
2. Computes target position in front of player (projected onto horizontal plane for natural forward direction)
3. Converts world position to plane-relative coordinates
4. Clamps X and Y coordinates within safe bounds (halfExtents - inset)
5. Reconstructs world position from clamped coordinates

This ensures the menu stays within the locomotion plane boundaries regardless of plane orientation.

## Usage Patterns

### Scenario 1: Natural VR Menu (Recommended - Default)
```csharp
// In Inspector:
Position In Front Of Player: ✓ Enabled
Use Plane Normal Direction: ✗ Disabled (uses camera forward)
Face Camera: ✓ Enabled (faces camera directly)
Distance From Player: 2.0
Apply Boundary Constraints: ✓ Enabled
Boundary Inset: (0.5, 0.5)
```
Result: Menu appears in player's natural view direction and faces camera directly. Best for VR immersion.

### Scenario 2: Plane-Aligned Menu
```csharp
// In Inspector:
Position In Front Of Player: ✓ Enabled
Use Plane Normal Direction: ✓ Enabled (perpendicular to plane)
Face Camera: ✗ Disabled (respects plane orientation)
Lock Y Rotation: ✓ Enabled (keeps upright)
Distance From Player: 2.0
Apply Boundary Constraints: ✓ Enabled
```
Result: Menu positioned along plane normal and rotates with plane orientation. Good for plane-centric gameplay.

### Scenario 3: Fixed Position (Original Behavior)
```csharp
// In Inspector:
Position In Front Of Player: ✗ Disabled
```
Result: Menu stays at its original world position (pre-existing behavior).

### Scenario 4: Custom Distance with No Boundary Constraints
```csharp
// In Inspector:
Position In Front Of Player: ✓ Enabled
Distance From Player: 3.5
Apply Boundary Constraints: ✗ Disabled
```
Result: Menu appears 3.5 meters in front of player without boundary clamping.

### Scenario 5: Large Menu with Extra Inset
```csharp
// In Inspector:
Position In Front Of Player: ✓ Enabled
Boundary Inset: (1.0, 1.0)  // Extra safety margin for large menu
```
Result: Menu stays at least 1 meter away from plane boundaries on all sides.

## Debug Visualization

When **Draw Debug Gizmos** is enabled (Scene view only):
- **Yellow Line**: Connects player camera to menu position
- **Yellow Wire Sphere**: Shows target menu position
- **Faded Rectangle**: Shows safe boundary area (plane bounds minus inset)

Use this to:
- Verify menu placement looks correct
- Ensure menu stays within boundaries
- Adjust distance and inset values visually

## API Reference

### Public Methods

```csharp
public void UpdatePosition()
```
Computes and applies menu position. Called automatically by menu presenters when menu becomes visible.

```csharp
public void SetHalfExtents(Vector2 newHalfExtents)
public void SetCenterOffset(Vector2 newCenterOffset)
```
Available on `PlayerPlaneDefinition` to adjust boundaries at runtime.

## Performance Considerations

- Position is only updated when menu becomes visible (state change)
- No per-frame updates during gameplay
- Boundary clamping uses efficient vector math
- Auto-discovery runs once in `Awake()`

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Menu not moving | Verify "Position In Front Of Player" is enabled |
| Menu facing backward/inverted | This is now fixed - menu always faces camera correctly |
| Menu appears to the side when looking sideways | Ensure "Face Camera" is enabled (default) |
| Menu not aligned with plane | Enable "Use Plane Normal Direction" and disable "Face Camera" |
| Menu at wrong distance | Adjust "Distance From Player" value |
| Menu clipping through walls | Increase "Boundary Inset" values |
| Menu too high/low | Adjust "Vertical Offset" value |
| Menu tilted (plane mode) | Enable "Lock Y Rotation" option (default) |
| No camera found warning | Assign Player Camera reference manually |
| Boundaries not working | Verify PlayerPlaneDefinition is in scene and assigned |

## Example Scene Setup

```
XR Origin (Player Root)
├─ Camera Offset
│  └─ Main Camera ← Referenced by PlayerFacingMenuPositioner.playerCamera
├─ PlayerPlane ← Has PlayerPlaneDefinition component
│  └─ (defines locomotion plane and boundaries)
└─ UI
   ├─ PauseMenu
   │  ├─ PauseMenuPresenter ← Handles pause logic
   │  └─ PlayerFacingMenuPositioner ← NEW: Positions menu
   └─ GameOverMenu
      ├─ GameOverPresenter ← Handles game over logic
      └─ PlayerFacingMenuPositioner ← NEW: Positions menu
```

## Implementation Notes

- Both `PauseMenuPresenter` and `GameOverPresenter` were updated to integrate with the positioner
- The positioner is **optional** - if not present, menus use original fixed-position behavior
- Position updates happen on state transitions (GameState.Paused, GameState.GameOver)
- The system follows project conventions: inspector-based interface resolution, optional components, debug tooling
- Boundary math reuses the same coordinate system and axes computation as `PlanarRectBounds` for consistency

## Future Enhancements

Potential improvements:
- Smooth position transitions with configurable easing
- Dynamic distance adjustment based on player movement speed
- Option to clamp vertical position within plane bounds
- Support for multiple camera references (spectator mode)
- Configurable fade-in animation when repositioning
