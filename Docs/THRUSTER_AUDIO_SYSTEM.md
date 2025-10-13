# Thruster Audio System

## Overview
The ThrusterController now includes intensity-based 3D spatial audio that responds dynamically to grip input, with separate audio sources attached to each controller transform for immersive positional feedback.

## Features

### 1. **Intensity-Based Volume Control**
- Volume scales from `minVolume` to `maxVolume` based on grip pressure
- Optional `volumeCurve` allows custom volume response shaping
- Smooth interpolation prevents audio popping

### 2. **Dynamic Pitch Shifting**
- Pitch ranges from `minPitch` to `maxPitch` based on thrust intensity
- Creates realistic "engine revving" effect as thrust increases
- Configurable via ThrusterConfig asset

### 3. **3D Spatial Audio**
- Each controller (left/right hand) has its own AudioSource
- AudioSources are parented to controller transforms
- Full 3D spatialization with configurable rolloff (0.1m - 10m range)
- Audio follows controller movement in real-time

### 4. **Smooth Transitions**
- Configurable `audioSmoothingSpeed` for fade in/out
- Prevents abrupt audio changes when grip is released
- Audio stops only when volume reaches near-zero

## Configuration

### ThrusterConfig ScriptableObject
New audio parameters added to `Assets/Scripts/XR/Locomotion/ThrusterConfig.cs`:

```csharp
[Header("Audio")]
public AudioClip thrustLoopClip;           // Seamlessly looping thrust sound
public float minVolume = 0.1f;             // Volume at minimum thrust
public float maxVolume = 0.8f;             // Volume at full thrust
public AnimationCurve volumeCurve;         // Optional volume response curve
public float minPitch = 0.8f;              // Pitch at minimum thrust
public float maxPitch = 1.2f;              // Pitch at full thrust
public float audioSmoothingSpeed = 10f;    // Fade speed (higher = snappier)
```

## Setup Instructions

### 1. Assign Audio Clip
1. Open your `ThrusterConfig` asset (typically in `Assets/Configs/`)
2. Drag a looping audio clip into the **Thrust Loop Clip** field
3. Recommended: Use a seamless engine loop or jet thruster sound

### 2. Tune Audio Parameters
- **Min/Max Volume**: Adjust based on your game's overall audio mix
- **Min/Max Pitch**: Range of 0.8-1.2 provides subtle variation; wider ranges are more dramatic
- **Audio Smoothing Speed**: 
  - `5-8`: Slow, smooth transitions
  - `10-15`: Default, balanced
  - `20+`: Snappy, responsive

### 3. Optional: Customize Volume Curve
- Click the **Volume Curve** field to open the curve editor
- Default is linear (0,0) → (1,1)
- Example curves:
  - **EaseIn**: Gentle start, aggressive at high grip
  - **EaseOut**: Strong immediate feedback, plateaus at high grip
  - **Custom**: Shape response to match your game feel

## Technical Details

### Audio Source Setup
- **Created automatically** in `Awake()` if `thrustLoopClip` is assigned
- Child GameObjects named `LeftThrusterAudio` / `RightThrusterAudio`
- Positioned at controller origin with zero local offset
- Configured for 3D spatial audio with linear rolloff

### Update Flow
1. **Update()**: Reads grip input, calls `UpdateThrusterAudio()`
2. **UpdateThrusterAudio()**: Calculates target volume/pitch for each hand
3. **UpdateHandAudio()**: Smoothly interpolates AudioSource parameters
   - Starts playback when grip exceeds dead zone
   - Fades out and stops when grip drops below dead zone

### Integration with Existing Systems
- **Respects grip dead zone** from existing thrust logic
- **No performance impact** when audio clip is not assigned
- **Compatible with haptics** (both use normalized grip intensity)

## Performance Notes
- AudioSources are created once in `Awake()`, not pooled
- Smooth interpolation uses `Time.deltaTime` scaling
- Audio only plays when grip > dead zone (no wasted processing)

## Troubleshooting

### No Audio Playing
1. Check `ThrusterConfig` has `thrustLoopClip` assigned
2. Verify grip input is working (enable Debug HUD)
3. Check Unity Audio Listener is present in scene
4. Ensure audio clip is set to **streaming** for looping sounds

### Audio Pops/Clicks
- Increase `audioSmoothingSpeed` for smoother transitions
- Ensure audio clip loops seamlessly (no silence at start/end)
- Check grip dead zone isn't too small (causing rapid on/off)

### Audio Too Loud/Quiet
- Adjust `minVolume` and `maxVolume` in ThrusterConfig
- Check Unity's Audio Mixer settings
- Verify AudioSource rolloff settings (maxDistance)

### Audio Doesn't Follow Controllers
- Ensure `leftHand` and `rightHand` transforms are assigned in ThrusterController
- Verify transforms are actually the XR controller GameObjects
- Check AudioSource child objects were created in Awake()

## Future Enhancements
Potential additions for v2:
- Separate start/stop one-shot clips
- Stereo panning based on thrust direction
- Turbulence/wind sound layer at high speeds
- Integration with GameManager state gating
