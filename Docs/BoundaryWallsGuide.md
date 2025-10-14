# Boundary Walls Setup Guide

## Overview
The `PlanarRectWalls` component creates transparent boundary walls around your playable area that automatically match the dimensions of your player's plane. These walls are visual indicators that help players understand the movement boundaries.

## Quick Setup

### 1. Create a Material
Before adding the component, create a material for the walls:
1. In Project window: Right-click → **Create → Material**
2. Name it something like "WallMaterial" or "BoundaryWalls"
3. Configure the material:
   - **Shader**: Universal Render Pipeline/Lit (or any shader)
   - **Surface Type**: Transparent
   - **Base Color**: Set RGB for color, adjust Alpha for transparency (0.2-0.5 recommended)
   - **Smoothness**: 0.9 for glossy, 0.3 for matte
   - **Metallic**: Adjust to taste (0.0-0.1 for non-metallic)

### 2. Add the Component
1. In your scene hierarchy, find or create a GameObject to hold the walls (e.g., "ArenaWalls")
2. Add the `PlanarRectWalls` component to it

### 2. Add the Component
1. In your scene hierarchy, find or create a GameObject to hold the walls (e.g., "ArenaWalls")
2. Add the `PlanarRectWalls` component to it

### 3. Configure References
The component has two required references:
- **PlayerPlaneDefinition**: The component that defines your plane and its dimensions
  - If left empty, the system will auto-find it in your scene
  - For best performance, assign it manually in the Inspector
- **Wall Material**: The material to use for all four walls
  - **Required** - you must assign this or walls will not render

### 4. Customize Appearance
In the Inspector, you'll see these options:

#### Material Settings
- **Wall Material**: Your assigned material (required)
  - All four walls share this single material
  - Edit the material asset to change appearance

#### Dimensions
- **Wall Height** (default: 2.2m): How tall the walls are (extending along the plane normal)
- **Normal Offset** (default: 0): Shifts the walls along the plane normal axis
  - **Positive values**: Move walls forward (toward targets/enemies)
  - **Negative values**: Move walls backward (behind the player)
  - Useful for making boundaries asymmetric relative to the player's plane
- **Edge Offset** (default: 0.5m): Pushes walls outward from the boundary edges
  - **Prevents camera clipping**: Keeps walls visible when player moves to boundary edge
  - **Recommended values**: 0.5m to 1.5m depending on camera setup
  - Set to 0 for walls exactly at boundary (may disappear when player is at edge)

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

### Material Creation Examples

#### Subtle Sci-Fi Boundaries
Create a material with these settings:
- **Shader**: URP/Lit
- **Surface Type**: Transparent
- **Base Color**: Light cyan (RGB: 165, 242, 255) with Alpha: 45 (~0.18)
- **Smoothness**: 0.9 (glossy "force field" look)
- **Metallic**: 0.05
- **Rendering Mode**: Transparent

#### Solid Warning Walls
For more visible boundaries:
- **Shader**: URP/Lit
- **Surface Type**: Transparent  
- **Base Color**: Light red or orange with Alpha: 77-128 (~0.3-0.5)
- **Smoothness**: 0.3 (more matte appearance)
- **Metallic**: 0.0

#### Holographic/Grid Effect
For a more stylized look:
- **Shader**: Create custom shader or use Shader Graph
- Add grid texture or fresnel effects
- Animate properties via script or shader animation

### Using Any Shader
The component works with any material/shader:
- Standard shader materials
- Custom Shader Graph materials  
- Particle shaders for special effects
- Unlit shaders for performance
- Custom HLSL shaders

### Positioning Tricks

#### Preventing Wall Disappearance
Use **Edge Offset** to keep walls visible:
- **Problem**: Walls disappear when player gets too close (camera near clip plane)
- **Solution**: Set Edge Offset to 0.5m or higher
- **Effect**: Walls are pushed outward beyond the actual movement boundary
- **Result**: Player sees walls before hitting the boundary constraint

**Example values**:
- **0.5m** (default): Good for most VR setups
- **1.0m**: Better for aggressive movement or close-up cameras
- **1.5m+**: Maximum visibility, but walls appear further from actual boundary

#### Asymmetric Boundaries
Use **Normal Offset** to create asymmetric play areas:
- **Example**: Set to `2.0` to push walls 2 meters toward targets
- This gives players more space behind them and tighter space toward enemies
- Useful for defending-style gameplay where enemies approach from one direction

#### Testing in Editor
- Enable **Draw Edge Gizmos** to see wall positions in Scene view
- When **Normal Offset** is non-zero, a yellow line shows the offset direction
- Adjust in real-time and see immediate feedback

## Troubleshooting

### Walls Not Visible
- **Check material assignment**: Ensure Wall Material field is not empty (required)
- Check that the material's alpha is not too low (minimum ~0.1 to see anything)
- Ensure the **wallHeight** is appropriate for your scene scale
- Verify your camera can see the wall positions (check Scene view)
- Check material's Rendering Mode is set correctly (Transparent for see-through walls)

### Walls Disappear When Getting Close
**Symptom**: Walls vanish when player approaches the boundary edge  
**Cause**: Camera's near clip plane cuts off the wall geometry when too close

**Solutions**:
1. **Increase Edge Offset** (recommended): Set to 1.0m or higher
2. Reduce camera near clip plane (may affect depth precision)
3. Adjust wall height if walls are too tall for the camera FOV

**How Edge Offset Works**:
```
Without Edge Offset (0):           With Edge Offset (1.0m):
Wall at boundary edge              Wall pushed 1m outward
Player can get very close          Buffer zone keeps wall visible
Wall may clip/disappear            Wall stays in view
```

### Walls Don't Update
- Ensure `PlayerPlaneDefinition` reference is assigned
- Check that the `PlayerPlaneDefinition` component is enabled
- Look for errors in the Console

### Wrong Size/Position
- The walls follow the `PlayerPlaneDefinition` dimensions exactly
- If they don't match expectations, adjust the HalfExtents and CenterOffset on the `PlayerPlaneDefinition` component
- Use **Normal Offset** to shift walls forward/backward along the plane normal
- Use the gizmos (both components have them) to visualize the boundaries

### Material Issues
- If walls appear black/purple, check the shader is compatible with your render pipeline (URP vs Built-in)
- If transparency doesn't work, ensure Surface Type is set to Transparent in the material
- Check the material's Render Queue is set to Transparent (3000+)

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
