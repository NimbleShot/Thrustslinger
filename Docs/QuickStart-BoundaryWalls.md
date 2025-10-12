# Quick Start: Add Boundary Walls to Your Scene

## What You Already Have
✅ `PlanarRectWalls` component - Creates transparent boundary walls  
✅ `PlayerPlaneDefinition` component - Defines your play area dimensions  
✅ `PlanarRectBounds` component - Enforces player movement boundaries  

All these components work together seamlessly!

## Add Walls to Your Scene (3 Steps)

### Step 1: Create a GameObject for the Walls
In your Game scene:
1. Right-click in Hierarchy → **Create Empty**
2. Name it "ArenaWalls" or "BoundaryWalls"
3. Position it at (0, 0, 0) or anywhere convenient

### Step 2: Add the PlanarRectWalls Component
1. Select your "ArenaWalls" GameObject
2. In Inspector, click **Add Component**
3. Search for `PlanarRectWalls`
4. Add it

### Step 3: Assign the PlayerPlaneDefinition Reference
1. With "ArenaWalls" selected, look at the Inspector
2. Find the `PlanarRectWalls` component
3. Under **References**, find the "Player Plane Definition" field
4. Drag your PlayerPlane GameObject (the one with `PlayerPlaneDefinition` component) into this field
   - Or use the circle picker to select it from the scene

**That's it!** The walls will automatically appear and match your plane dimensions.

## Customization (Optional)

### Make Walls More Visible
In the `PlanarRectWalls` Inspector:
- **Tint** → Increase the Alpha (A) value from 0.18 to 0.3 or higher
- **Wall Height** → Adjust from 2.2m to whatever fits your scene

### Change Color
- **Tint** → Pick any color you like
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
- Check the Tint alpha isn't too low (minimum ~0.15)
- Make sure your PlayerPlaneDefinition reference is assigned
- Check Scene view - walls might be there but camera not looking at them

**Wrong size/position?**
- The walls follow your `PlayerPlaneDefinition` exactly
- Adjust dimensions on the `PlayerPlaneDefinition` component, not the walls

## What's Happening Behind the Scenes
- The component creates 4 child GameObjects: Wall_Left, Wall_Right, Wall_Top, Wall_Bottom
- Each wall is a procedural quad mesh
- They update automatically when you change plane dimensions
- Works in both Edit Mode and Play Mode
- Uses URP transparent material for the acrylic effect

---

**For detailed information**, see: `Docs/BoundaryWallsGuide.md`
