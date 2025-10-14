# Quick Start: Add Boundary Walls to Your Scene

## What You Already Have
✅ `PlanarRectWalls` component - Creates transparent boundary walls  
✅ `PlayerPlaneDefinition` component - Defines your play area dimensions  
✅ `PlanarRectBounds` component - Enforces player movement boundaries  

All these components work together seamlessly!

## Add Walls to Your Scene (4 Steps)

### Step 1: Create a Material
First, create a material for your walls:
1. In Project window, right-click → **Create → Material**
2. Name it "WallMaterial" or similar
3. Configure it:
   - Set shader to **Universal Render Pipeline/Lit** (or any shader you prefer)
   - Set **Surface Type** to **Transparent**
   - Set **Base Color** with alpha ~0.2-0.5 for transparency
   - Adjust other properties to taste

### Step 2: Create a GameObject for the Walls
In your Game scene:
1. Right-click in Hierarchy → **Create Empty**
2. Name it "ArenaWalls" or "BoundaryWalls"
3. Position it at (0, 0, 0) or anywhere convenient

### Step 3: Add the PlanarRectWalls Component
1. Select your "ArenaWalls" GameObject
2. In Inspector, click **Add Component**
3. Search for `PlanarRectWalls`
4. Add it

### Step 4: Assign References
1. With "ArenaWalls" selected, look at the Inspector
2. Under **References**, drag your PlayerPlane GameObject (with `PlayerPlaneDefinition` component) into the "Player Plane Definition" field
3. Under **Material**, drag your WallMaterial into the "Wall Material" field

**That's it!** The walls will automatically appear and match your plane dimensions.

## Customization (Optional)

### Change Material Appearance
Edit your assigned wall material:
- Adjust **Base Color** and alpha for different colors/transparency
- Try **Smoothness** for glossy (1.0) or matte (0.0) appearance
- Try **Metallic** for metallic looks
- Add textures, emission, or other effects

### Adjust Wall Position
- **Wall Height** → Change how tall the walls are (default: 2.2m)
- **Normal Offset** → Shift walls along the plane normal
  - **Positive values** (e.g., 1.0): Move walls forward toward targets
  - **Negative values** (e.g., -1.0): Move walls backward behind player
  - **Zero** (default): Walls align exactly with the plane
- **Edge Offset** → Push walls outward from boundary edges (default: 0.5m)
  - **Prevents walls from disappearing** when player gets too close
  - Increases from 0.5m to 1.0m+ if walls still disappear
  - Set to 0 for walls exactly at boundary (may clip when near edge)

### Change Color
Edit your wall material:
- **Base Color** → Pick any color you like
- Try a light blue/cyan for sci-fi feel
- Try light red for danger zones
- Keep alpha between 0.15-0.5 for translucent effect

### Adjust to Your Plane Size
If you change the plane dimensions:
1. Select your GameObject with `PlayerPlaneDefinition`
2. Adjust **Half Extents** (width/height of play area)
3. Adjust **Center Offset** if needed
4. The walls will update automatically! ✨

## Troubleshooting

**Can't see walls?**
- Check the material's alpha isn't too low (minimum ~0.15)
- Make sure your Wall Material reference is assigned
- Check Scene view - walls might be there but camera not looking at them
- **Walls disappear when getting close?** Increase the **Edge Offset** value (try 1.0 or higher)

**Wrong size/position?**
- The walls follow your `PlayerPlaneDefinition` exactly
- Adjust dimensions on the `PlayerPlaneDefinition` component, not the walls
- Use **Normal Offset** to shift walls forward/backward along the plane normal

## What's Happening Behind the Scenes
- The component creates 4 child GameObjects: Wall_Left, Wall_Right, Wall_Top, Wall_Bottom
- Each wall is a procedural quad mesh with your assigned material
- They update automatically when you change plane dimensions or normal offset
- Works in both Edit Mode and Play Mode

---

**For detailed information**, see: `Docs/BoundaryWallsGuide.md`
