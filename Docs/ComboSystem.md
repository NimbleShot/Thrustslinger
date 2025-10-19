# Combo System Implementation

## Overview
A combo tracking system has been added to Thrustslinger that increments on successful kills and resets on target breaches. The combo is displayed on the HUD when it reaches a threshold (default: 2).

## New Files Created

### 1. `IRuntimeComboProvider.cs`
- **Location**: `Assets/Scripts/Core/GameManagement/`
- **Purpose**: Interface for exposing runtime combo data to UI components
- **Key Members**:
  - `event Action<int> ComboChanged` - Raised whenever combo count changes
  - `int CurrentCombo` - Current combo count
  - `int HighestCombo` - Highest combo achieved across all runs
  - `void IncrementCombo()` - Increments the combo counter

### 2. `ComboTracker.cs`
- **Location**: `Assets/Scripts/Core/GameManagement/`
- **Purpose**: MonoBehaviour component that implements combo tracking logic
- **Implements**: `IComboTracker`, `IRuntimeComboProvider`
- **Features**:
  - Tracks current combo count
  - Persists highest combo to PlayerPrefs
  - Raises events when combo changes
  - Integrates with GameManager lifecycle (reset on run start, break on breach)
  - Debug logging for combo events

## Modified Files

### 1. `GameManager.cs`
**Change**: Added combo increment on kill registration

```csharp
public void RegisterKill(in RunKillData killData)
{
    _scoreService?.RegisterKill(killData);
    
    // Increment combo on successful kill
    if (_comboTracker is IRuntimeComboProvider comboProvider)
    {
        comboProvider.IncrementCombo();
    }
}
```

**Existing Integration Points** (already in place):
- `_comboTracker?.ResetCombo()` - Called at start of run
- `_comboTracker?.BreakCombo()` - Called on target breach

### 2. `HudPresenter.cs`
**Changes**: Added combo display functionality

**New Inspector Fields**:
- `TMP_Text comboText` - Text component for displaying combo
- `MonoBehaviour comboProviderBehaviour` - Optional combo provider reference
- `string comboFormat = "Combo: {0}"` - Format string for combo display
- `int comboThreshold = 2` - Minimum combo count before showing on HUD

**New Private Members**:
- `IRuntimeComboProvider _comboProvider` - Resolved combo provider
- `int _currentCombo` - Cached combo value

**New Methods**:
- `ResolveComboProvider()` - Finds ComboTracker in scene or uses assigned provider
- `HandleComboChanged(int)` - Event handler for combo updates
- `UpdateComboText()` - Updates the combo text and visibility based on threshold

**Behavior**:
- Combo text is hidden when below threshold (default: < 2)
- Combo text becomes visible and updates when >= threshold
- Auto-resolves ComboTracker component if not explicitly assigned

## Setup Instructions

### In Unity Editor:

1. **Add ComboTracker Component**:
   - Add `ComboTracker` component to the GameManager GameObject
   - Assign it to GameManager's `comboTrackerBehaviour` field (inspector)

2. **Update HUD**:
   - Add a new TextMeshProUGUI element to your HUD canvas for combo display
   - Assign it to HudPresenter's `comboText` field
   - (Optional) Adjust `comboFormat` and `comboThreshold` in HudPresenter inspector

3. **(Optional) Assign Combo Provider**:
   - If you want explicit wiring, assign the ComboTracker to HudPresenter's `comboProviderBehaviour` field
   - Otherwise, it will auto-find the ComboTracker in the scene

### Configuration Options:

**ComboTracker Inspector**:
- `Track Highest Combo` - Enable/disable high score persistence
- `Highest Combo Prefs Key` - PlayerPrefs key for storing record
- `Log Combo Events` - Debug logging toggle

**HudPresenter Inspector**:
- `Combo Text` - TextMeshPro component reference
- `Combo Provider Behaviour` - Optional explicit provider
- `Combo Format` - Display format (default: "Combo: {0}")
- `Combo Threshold` - Minimum combo before showing (default: 2)

## How It Works

### Combo Flow:
1. **Kill** → `Target.cs` calls `GameManager.RegisterKill()`
2. **RegisterKill** → Increments combo via `IRuntimeComboProvider.IncrementCombo()`
3. **ComboChanged event** → `HudPresenter.HandleComboChanged()` updates UI
4. **Breach** → `GameManager.NotifyPlaneBreach()` calls `_comboTracker?.BreakCombo()`
5. **Break** → Combo resets to 0, event raised, UI updates

### High Score Tracking:
- When combo breaks, if current combo > HighestCombo, it's saved to PlayerPrefs
- HighestCombo is loaded on Awake
- Use context menu "Clear Highest Combo" to reset (editor only)

## Testing

1. Start a run in the editor
2. Kill targets - combo should increment and display after 2+ kills
3. Let a target breach - combo should reset to 0 and hide from HUD
4. Check console if `Log Combo Events` is enabled

## Architecture Notes

- Follows the same pattern as `RuntimeScoreService` / `IRuntimeScoreProvider`
- Uses interface-based service resolution (inspector MonoBehaviour → interface cast)
- GameManager already had `IComboTracker` support, now extended with runtime provider
- HUD auto-resolves services using `FindFirstObjectByType` as fallback
- Combo display visibility controlled by threshold to avoid clutter at low values
