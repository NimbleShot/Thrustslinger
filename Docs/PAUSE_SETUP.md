# Pause Menu Setup Guide

This guide explains how to set up the pause menu functionality in your VR game scene.

## Overview

The pause system consists of three main components:
1. **PauseInputController** - Listens for XR button input to pause/unpause the game
2. **PauseMenuPresenter** - Controls the pause menu UI with Resume and Quit buttons
3. **GameManager** - Orchestrates pause state and system toggling

## Scene Setup Instructions

### 1. Create the Pause Input Controller

1. In your gameplay scene, create a new empty GameObject (e.g., "PauseInputController")
2. Add the `PauseInputController` component
3. Configure the pause button:
   - **Option A (Recommended)**: In the Inspector, assign an `InputActionReference` to the `Pause Action` field
     - You can create an Input Action asset or reference an existing one
     - Map it to a common VR button like the Menu button: `<XRController>{LeftHand}/{Menu}`
   - **Option B (Fallback)**: Leave the field empty and the component will automatically use the left controller's Menu button
4. Optionally enable `Log Pause Toggle` for debugging

### 2. Create the Pause Menu UI

1. In your Canvas (make sure it's a World Space canvas for VR), create a Panel for the pause menu
2. Name it something like "PauseMenu"
3. Add UI elements:
   - A "Resume" button
   - A "Quit to Main Menu" button
   - Optional: Title text, background panel, etc.
4. Add the `PauseMenuPresenter` component to the PauseMenu GameObject
5. In the Inspector, assign the buttons:
   - Drag the Resume button to the `Resume Button` field
   - Drag the Quit button to the `Quit To Main Menu Button` field
6. (No configuration needed here; scene loading is handled by `GameManager`.)
7. Initially **disable** the PauseMenu GameObject (GameManager will enable it when paused)

### 3. Wire to GameSceneBootstrapper

1. Find or create the `GameSceneBootstrapper` GameObject in your scene
2. In the Inspector, expand the `Scene Bindings` section
3. Assign the following:
   - **Pause Input Controller**: Drag the PauseInputController GameObject
   - **Pause UI**: Drag the PauseMenu GameObject (the root with PauseMenuPresenter)
4. Make sure other bindings are set up (HUD UI, XR Menu Ray Root, etc.)

### 4. Configure GameManager (if needed)

The GameManager singleton should already be configured, but verify:
- `Pause Uses Time Scale` is enabled (pauses physics/animations)
- `Auto Pause On Focus Loss` is enabled (optional, for desktop testing)

## Input Action Setup (Recommended)

For better control and consistency, create an Input Action asset:

1. In Unity, create an Input Actions asset: `Assets > Create > Input Actions`
2. Name it something like "VRInputActions"
3. Add an Action Map (e.g., "Gameplay")
4. Add a new action:
   - **Name**: "Pause"
   - **Action Type**: Button
   - **Binding Path**: `<XRController>{LeftHand}/{Menu}` (or another button like Start)
5. Save the asset
6. In the PauseInputController Inspector, assign this action to the `Pause Action` field

## Button Binding Options

Common VR controller buttons you can use for pause:
- **Menu Button**: `<XRController>{LeftHand}/{Menu}` (most common)
- **Start Button**: `<XRController>{LeftHand}/{Start}`
- **Primary Button**: `<XRController>{LeftHand}/{PrimaryButton}` (X on Quest, Square on PlayStation)
- **Secondary Button**: `<XRController>{LeftHand}/{SecondaryButton}` (Y on Quest, Triangle on PlayStation)

## How It Works

### Pause Flow
1. Player presses the configured pause button (e.g., Menu button)
2. `PauseInputController` detects the button press
3. It calls `GameManager.Pause()`
4. GameManager:
   - Disables all gated gameplay systems (weapons, spawners, thruster)
   - Sets `Time.timeScale = 0` (if configured)
   - Shows the pause UI
   - Enables the XR menu ray for UI interaction
   - Changes state to `Paused`

### Resume Flow
1. Player either:
   - Presses the pause button again, OR
   - Clicks the "Resume" button in the pause menu
2. `GameManager.Resume()` is called
3. GameManager:
   - Restores `Time.timeScale`
   - Re-enables gated gameplay systems
   - Hides the pause UI
   - Changes state back to `Playing`

### Quit to Main Menu Flow
1. Player clicks "Quit to Main Menu" button
2. `PauseMenuPresenter.HandleQuitToMainMenuClicked()` is called
3. It caches the menu context and summary via `MenuRunContextStore`
4. It calls `GameManager.QuitToMenu()` to clean up the run state
5. GameManager:
   - Stops all gameplay coroutines
   - Resets time scale
   - Disables gameplay systems
   - Changes state to `MainMenu`
6. The PauseMenuPresenter then loads the main menu scene via `SceneManager.LoadScene()`

## Troubleshooting

**Pause button not working:**
- Check that PauseInputController is enabled in the scene hierarchy
- Verify the Input Action is properly configured and enabled
- Enable "Log Pause Toggle" to see debug messages
- Make sure you're in Playing state (pause only works during gameplay)

**Pause menu UI not appearing:**
- Verify the Pause UI GameObject is assigned in GameSceneBootstrapper
- Check that ToggleUIForState is being called (add a debug log if needed)
- Make sure the pause menu Canvas is set to World Space for VR

**Can't interact with pause menu:**
- Verify the XR Menu Ray Root is assigned and enabled during pause
- Check that the pause menu has proper colliders for UI interaction
- Make sure the Canvas has a Graphic Raycaster component

**Resume button not working:**
- Check that the button is properly assigned in PauseMenuPresenter Inspector
- Verify the button has the onClick event wired up (should happen automatically)
- Enable debugging in PauseInputController to see if state changes are happening

**Quit to Main Menu not loading scene:**
- Verify the `Main Menu Scene Name` field in PauseMenuPresenter matches your actual main menu scene name
- Check that the scene is added to the Build Settings (File → Build Settings)
- Look for errors in the Console when clicking the button

## Architecture Notes

- **PauseInputController** is NOT part of the gated systems - it stays enabled during both Playing and Paused states so it can handle pause/unpause
- **Gated systems** (weapons, spawner, thruster) are disabled during pause to freeze gameplay
- The **pause UI** is only visible during Paused state
- The **XR menu ray** is enabled during pause to allow UI interaction
- `Time.timeScale = 0` freezes physics and most game logic (use `Time.unscaledDeltaTime` for UI animations if needed)
