# Pause System Quick Reference

## Quick Setup (5 Steps)

1. **Add PauseInputController**
   - Create empty GameObject → Add `PauseInputController` component
   - Optionally configure Input Action (default: left menu button)

2. **Create Pause Menu UI**
   - Add Panel to World Space Canvas → Name it "PauseMenu"
   - Add "Resume" and "Quit to Main Menu" buttons
   - Add `PauseMenuPresenter` component → Assign buttons in Inspector
   - Set `Main Menu Scene Name` field (default: "MainMenu")
   - Disable the PauseMenu GameObject

3. **Wire to Scene Bindings**
   - Open `GameSceneBootstrapper` Inspector
   - Assign PauseInputController to `Scene Bindings > Pause Input Controller`
   - Assign PauseMenu GameObject to `Scene Bindings > Pause UI`

4. **Test in Editor**
   - Press Play → Start a run
   - Use Context Menu: PauseInputController → "Test Pause Toggle"
   - Or press the configured VR button in VR preview

5. **Deploy to VR**
   - Build and deploy to headset
   - Press left controller Menu button to pause
   - Use ray to interact with pause menu

## Key Classes

### PauseInputController (XR Namespace)
```csharp
// Location: Assets/Scripts/XR/PauseInputController.cs
// Purpose: Listens for XR button press, toggles pause state
// Not gated: Stays active during Playing and Paused
```

### PauseMenuPresenter (UI Namespace)
```csharp
// Location: Assets/Scripts/UI/PauseMenuPresenter.cs
// Purpose: Handles pause menu button clicks
// Methods: HandleResumeClicked(), HandleQuitToMainMenuClicked()
```

## GameManager API

```csharp
// Pause the game
GameManager.Instance.Pause();

// Resume the game
GameManager.Instance.Resume();

// Quit to main menu (with cleanup)
GameManager.Instance.QuitToMenu();

// Check if paused
bool isPaused = GameManager.Instance.IsPaused;

// Check if playing
bool isPlaying = GameManager.Instance.IsPlaying;

// Get current state
GameState state = GameManager.Instance.State;
```

## Common VR Button Bindings

| Controller | Button | Binding Path |
|------------|--------|--------------|
| All | Left Menu | `<XRController>{LeftHand}/{Menu}` |
| All | Right Menu | `<XRController>{RightHand}/{Menu}` |
| All | Left Start | `<XRController>{LeftHand}/{Start}` |
| Quest | X Button | `<XRController>{LeftHand}/{PrimaryButton}` |
| Quest | Y Button | `<XRController>{LeftHand}/{SecondaryButton}` |
| PlayStation | Square | `<XRController>{LeftHand}/{PrimaryButton}` |
| PlayStation | Triangle | `<XRController>{LeftHand}/{SecondaryButton}` |

## State Flow Diagram

```
Playing
   ↓ (pause button or Pause())
Paused
   ↓ (pause button or Resume())
Playing
   ↓ (QuitToMenu())
MainMenu
```

## What Gets Disabled During Pause?

**Gated Systems (Disabled)**:
- TargetSpawner
- ThrusterController
- Weapon systems
- Additional gameplay systems
- Haptics systems

**Always Active Systems**:
- PauseInputController
- GameManager
- UI systems

**Pause Effects**:
- `Time.timeScale = 0` (if enabled in GameManager)
- HUD stays visible (but dimmed/overlaid by pause menu)
- XR menu ray enabled for UI interaction

## Troubleshooting Quick Fixes

| Issue | Fix |
|-------|-----|
| Pause button not responding | Enable "Log Pause Toggle" in PauseInputController, check console |
| Menu not visible | Verify Pause UI assigned in GameSceneBootstrapper |
| Can't click buttons | Check XR Menu Ray Root is assigned and Canvas has GraphicRaycaster |
| Physics still running | Ensure "Pause Uses Time Scale" is enabled in GameManager |
| Multiple pauses | Check only one PauseInputController exists in scene |
| Quit not loading menu | Verify Main Menu Scene Name in PauseMenuPresenter, check Build Settings |

## Debug Tips

```csharp
// Enable logging in PauseInputController Inspector
logPauseToggle = true

// Test pause in editor without VR
PauseInputController → Context Menu → "Test Pause Toggle"

// Check current state
Debug.Log($"Game State: {GameManager.Instance.State}");

// Monitor time scale
Debug.Log($"Time Scale: {Time.timeScale}");
```

## Integration Notes

- Follow project pattern: `DisallowMultipleComponent` on all custom MonoBehaviours
- Use proper namespaces: `Thrustslinger.UI`, `Thrustslinger.XR`
- Subscribe to `GameManager.OnStateChanged` for pause/resume events
- Use `Time.unscaledDeltaTime` for UI animations during pause
- PauseInputController is scene-bound, not persistent like GameManager

## Files Modified

- `GameManager.cs` - Added pauseInputController field to SceneBindings
- Created `PauseInputController.cs`
- Created `PauseMenuPresenter.cs`
- Created `PAUSE_SETUP.md` (detailed setup guide)
- Created `PAUSE_IMPLEMENTATION_SUMMARY.md` (technical documentation)
