# Weapon Ammo Display Setup Guide

This guide explains how to set up the ammo display panel for the ProjectileWeapon system in Thrustslinger.


- **Location**: `WeaponAmmoDisplay.cs` (new component)
- **Feature**: A small UI panel that follows the right hand controller and displays current ammo

## Setup Instructions

### Quick Setup (In Unity Editor)

1. **Create the Display GameObject**
   - In the Hierarchy, create a new GameObject: `Right Click → UI → Canvas`
   - Name it "Weapon Ammo Display"
   - In the Canvas component:
     - Set `Render Mode` to **World Space**
     - Set `Width` to `200` and `Height` to `100`
     - Set `Scale` to `0.001` for all axes (X, Y, Z) - this makes it a reasonable size in VR

2. **Add UI Text Elements**
   - Right-click the Canvas → `UI → Text - TextMeshPro`
   - Name the first text "Ammo Text"
   - Configure Ammo Text:
     - Font Size: `36`
     - Alignment: Center/Middle
     - Color: White
     - Text: "12 / 12" (placeholder)
   
   - Optional: Create a second text for reload indicator
     - Right-click Canvas → `UI → Text - TextMeshPro`
     - Name it "Reload Text"
     - Font Size: `24`
     - Position it below the ammo text
     - Alignment: Center
     - Initial text: "RELOADING..."

3. **Add the WeaponAmmoDisplay Component**
   - Select the Canvas GameObject
   - In Inspector: `Add Component → Weapon Ammo Display`
   - Configure the component:
     - **Weapon**: Drag your ProjectileWeapon GameObject here
     - **Follow Target**: Drag the Right Hand Controller transform here
       - Typically found at: `XR Origin → Camera Offset → RightHand Controller`
     - **Local Offset**: Adjust position relative to controller
       - Default: `(0, 0.05, 0.1)` - slightly above and in front of the hand
       - Experiment with values to find comfortable position
     - **Face Away From Target**: `true` (displays on back of hand facing player)
     - **Smooth Speed**: `10` (adjust for more/less smoothing)
     - **Ammo Text**: Drag the "Ammo Text" TextMeshPro component here
     - **Reload Text**: Drag the "Reload Text" component (if created)

4. **Adjust Colors (Optional)**
   - **Normal Color**: White (when ammo is sufficient)
   - **Low Ammo Color**: Yellow (when ammo drops below threshold)
   - **Empty Color**: Red (when ammo is 0)
   - **Low Ammo Threshold**: `0.3` (30% of magazine)

5. **Enable Manual Rapid Fire (if needed)**
   - Select your ProjectileWeapon GameObject
   - In Inspector, find the Firing section
   - Check `Allow Manual Rapid Fire` (enabled by default)

## Configuration Tips

### Display Position Tuning
The `Local Offset` determines where the display appears relative to the controller:
- **X-axis**: Left/Right (-0.05 = left, 0.05 = right)
- **Y-axis**: Up/Down (0.05 = above controller, -0.05 = below)
- **Z-axis**: Forward/Back (0.1 = in front, -0.1 = behind)

### Recommended Positions
- **Back of hand** (facing player): Offset `(0, 0.05, 0.1)`, Face Away = true
- **Palm side** (facing away): Offset `(0, 0, -0.05)`, Face Away = false
- **Above hand** (like a watch): Offset `(0, 0.08, 0)`, Face Away = true

### Fire Rate Behavior
- **Fire Rate** in ProjectileWeapon: Controls automatic fire speed when trigger is held
- **Allow Manual Rapid Fire**: 
  - ✓ Enabled: New trigger pulls are immediate (spam-friendly)
  - ✗ Disabled: All shots respect fire rate limit (classic shooter feel)

## Troubleshooting

**Display not visible:**
- Check Canvas `Render Mode` is set to `World Space`
- Verify the Canvas scale is small (0.001 on all axes)
- Ensure Follow Target is assigned

**Display not moving:**
- Verify Follow Target is assigned to the Right Hand Controller transform
- Check that the controller GameObject is active in the scene

**Ammo not updating:**
- Ensure Weapon reference is assigned to your ProjectileWeapon instance
- Check that Ammo Text field points to a valid TMP_Text component

**Display position is wrong:**
- Adjust Local Offset values in inspector
- Toggle Face Away From Target to flip orientation
- Try different Smooth Speed values

## Script API

### WeaponAmmoDisplay Public Methods

```csharp
// Change which weapon to display
public void SetWeapon(ProjectileWeapon newWeapon)

// Change which transform to follow
public void SetFollowTarget(Transform newTarget)
```

### ProjectileWeapon Public Properties

```csharp
public int CurrentAmmo { get; }
public int MagazineCapacity { get; }
public bool IsReloading { get; }
public float ReloadTimeRemaining { get; }
```

## Design Notes

- The ammo display uses **world space** canvas for proper VR rendering
- Smoothing prevents jittery movement during controller motion
- Color coding provides quick visual feedback about ammo state
- Manual rapid fire respects magazine capacity and reload mechanics
- The system follows project conventions (namespace structure, DisallowMultipleComponent, inspector-based references)

## Future Enhancements

Possible additions:
- Reload progress bar
- Visual effects on low ammo (pulsing text)
- Audio cues integrated with display
- Multiple weapon support with weapon switching
- Customizable UI layouts/themes
