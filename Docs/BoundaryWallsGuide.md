# Boundary Walls Setup Guide

## Overview
The `PlanarRectWalls` component creates transparent boundary walls around your playable area that automatically match the dimensions of your player's plane. These walls are visual indicators that help players understand the movement boundaries.

## Quick Setup

### 1. Add the Component
1. In your scene hierarchy, find or create a GameObject to hold the walls (e.g., "ArenaWalls")
2. Add the `PlanarRectWalls` component to it

### 2. Configure References
The component has one required reference:
- **PlayerPlaneDefinition**: The component that defines your plane and its dimensions
  - If left empty, the system will auto-find it in your scene
  - For best performance, assign it manually in the Inspector

### 3. Customize Appearance
In the Inspector, you'll see these options:

#### Appearance Settings
- **Wall Height** (default: 2.2m): How tall the walls are (extending along the plane normal)
- **Tint** (default: cyan transparent): The color and transparency of the walls
  - Alpha channel controls transparency
  - Default is a light cyan at ~18% opacity
- **Smoothness** (0-1, default: 0.9): How glossy/reflective the surface appears
- **Metallic** (0-1, default: 0.05): Metallic property for the material
- **Material Override**: Leave empty to auto-generate, or assign a custom material

#### Options
- **Draw Edge Gizmos**: Shows colored wireframe outline in Scene view for debugging

## How It Works

### Automatic Updates
The walls automatically update in both **Edit Mode** and **Play Mode** when:
- The plane dimensions change (HalfExtents or CenterOffset)
- The plane position or orientation changes
- You modify the appearance settings in the Inspector

### Performance
The component uses:
- Four procedural quad meshes (one per wall)
- A single shared material (unless you provide an override)
- Efficient change detection to only update when needed

### Wall Positioning
The four walls are positioned at the edges of your plane rectangle:
- **Left/Right walls**: Aligned with the plane's Y-axis
- **Top/Bottom walls**: Aligned with the plane's X-axis
- All walls face **inward** toward the play area
- All walls extend upward along the plane normal

## Integration with Existing Systems

### PlayerPlaneDefinition
The walls read dimensions directly from your `PlayerPlaneDefinition` component:
```csharp
public Vector2 HalfExtents { get; }      // Width/height of plane
public Vector2 CenterOffset { get; }     // Center offset from plane point
public Vector3 Normal { get; }           // Plane orientation
public Vector3 PlanePoint { get; }       // Plane position
```

### PlanarRectBounds
Your `PlanarRectBounds` component (on the player) uses the same `PlayerPlaneDefinition` to enforce movement boundaries. This ensures the walls and the actual movement constraints are always in sync.

## Visual Customization Tips

### Subtle Boundaries (Default)
Keep the default settings for a subtle, sci-fi look:
- Light cyan tint with low opacity (~18%)
- High smoothness for a glossy "force field" appearance

### Solid Walls
For more visible boundaries:
- Increase the tint's alpha value to 0.3-0.5
- Reduce smoothness to 0.3-0.5 for a more matte appearance

### Custom Material
For full control:
1. Create a material with any shader you like
2. Assign it to the **Material Override** field
3. The component will use your material instead of auto-generating one

## Troubleshooting

### Walls Not Visible
- Check that the tint alpha is not too low (minimum ~0.1 to see anything)
- Ensure the **wallHeight** is appropriate for your scene scale
- Verify your camera can see the wall positions (check Scene view)

### Walls Don't Update
- Ensure `PlayerPlaneDefinition` reference is assigned
- Check that the `PlayerPlaneDefinition` component is enabled
- Look for errors in the Console

### Wrong Size/Position
- The walls follow the `PlayerPlaneDefinition` dimensions exactly
- If they don't match expectations, adjust the HalfExtents and CenterOffset on the `PlayerPlaneDefinition` component
- Use the gizmos (both components have them) to visualize the boundaries

## Example Scene Setup

```
PlayerPlane (GameObject)
  └─ PlayerPlaneDefinition component
       ├─ HalfExtents: (5, 3)
       ├─ CenterOffset: (0, 0)
       └─ Reference: PlayerPlane transform

Player (GameObject with Rigidbody)
  └─ PlanarRectBounds component
       └─ playerPlaneDefinition: → PlayerPlane/PlayerPlaneDefinition

ArenaWalls (GameObject)
  └─ PlanarRectWalls component
       └─ playerPlaneDefinition: → PlayerPlane/PlayerPlaneDefinition
```

## Notes
- The component uses `[ExecuteAlways]` so you can see walls in Edit Mode
- Walls are procedurally generated - no need to create mesh assets
- The system automatically handles plane orientation (not limited to XY/XZ planes)
- Walls are created as child GameObjects named "Wall_Left", "Wall_Right", "Wall_Top", "Wall_Bottom"
