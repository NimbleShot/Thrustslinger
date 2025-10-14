# Projectile Weapon Audio System

## Overview
The `ProjectileWeapon` class now includes 3D spatial audio effects for firing and reloading actions. Audio originates from the weapon hand (configurable, defaults to muzzle/right hand) with automatic pitch variation for shot variety and full positional audio support.

## Features

### 1. **Fire Sound Effects**
- Plays one-shot audio clip when projectile is fired
- Supports rapid fire with sound layering (no cutoff)
- Optional pitch randomization adds variety to repeated shots
- 3D spatial positioning for immersive directional feedback

### 2. **Reload Sound Effects**
- Optional audio clip plays when reload begins
- Consistent pitch (no variation) for recognition
- Helps players identify reload state without looking at UI

### 3. **Flexible Audio Source Location**
- Priority order: `audioSourceTransform` → `muzzle` → weapon transform
- Typically originates from right hand controller (via muzzle)
- Single AudioSource shared for all weapon sounds

### 4. **3D Positional Audio**
- Full 3D spatialization (`spatialBlend = 1.0`)
- Linear rolloff from 0.5m to 25m
- Audio follows weapon/hand movement in real-time

## Configuration

### Inspector Parameters
All audio settings are directly in the `ProjectileWeapon` component:

```csharp
[Header("Audio")]
public AudioClip fireSound;                // Required for shot audio
public AudioClip reloadSound;              // Optional reload audio
[Range(0f, 1f)] public float audioVolume = 0.7f;
[Range(0f, 0.3f)] public float pitchVariation = 0.05f;
public Transform audioSourceTransform;     // Optional override
```

### Parameter Details

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| **Fire Sound** | `AudioClip` | `null` | Audio played on each shot. Should be short (0.1-0.5s) |
| **Reload Sound** | `AudioClip` | `null` | Audio played when reload starts. Can be longer (0.5-2s) |
| **Audio Volume** | `float` | `0.7` | Master volume for all weapon sounds (0-1) |
| **Pitch Variation** | `float` | `0.05` | Random pitch range for fire sound (±value) |
| **Audio Source Transform** | `Transform` | `null` | Override audio origin. Falls back to muzzle, then weapon |

## Setup Instructions

### 1. Assign Audio Clips
1. Select the GameObject with the `ProjectileWeapon` component
2. In Inspector, find the **Audio** section
3. Drag a short gunshot/laser sound into **Fire Sound**
4. (Optional) Drag a reload/magazine sound into **Reload Sound**

### 2. Configure Volume & Pitch
- **Audio Volume**: Adjust based on your game's audio mix (0.5-0.8 recommended)
- **Pitch Variation**: 
  - `0.0` = No variation (monotonous)
  - `0.05` = Subtle variety (default, recommended)
  - `0.1-0.2` = Noticeable variation (more "game-y" feel)

### 3. Optional: Custom Audio Position
If you want audio to originate from a specific location:
1. Create/assign **Audio Source Transform** 
2. Typically left as `null` to use muzzle position automatically

## Recommended Audio Clips

### Fire Sound
- **Duration:** 0.1-0.5 seconds
- **Type:** Gunshot, laser blast, energy weapon discharge
- **Format:** WAV or OGG
- **Import Settings:**
  - Load Type: **Decompress On Load** (for rapid playback)
  - Compression Format: **PCM** or **ADPCM**
  - Quality: 70-100%
  - Force Mono: Optional (3D audio works best with mono)

### Reload Sound
- **Duration:** 0.5-2.0 seconds
- **Type:** Magazine click, energy pack insertion, mechanical reload
- **Format:** WAV or OGG
- **Import Settings:**
  - Load Type: **Compressed In Memory** (played less frequently)
  - Compression Format: **Vorbis**
  - Quality: 70-100%

## Technical Details

### Audio Source Setup
- **Created automatically** in `Awake()` via `SetupAudioSource()`
- Single `AudioSource` component added to chosen transform
- Configured for 3D spatial audio with linear rolloff
- Reused for both fire and reload sounds

### Audio Playback Flow

#### Fire Sound
1. **TryFire()** successfully spawns projectile
2. Calls `PlayFireSound()`
3. Applies random pitch variation: `basePitch ± pitchVariation`
4. Uses `PlayOneShot()` for non-blocking playback (allows layering)

#### Reload Sound
1. **BeginReload()** starts reload coroutine
2. Calls `PlayReloadSound()`
3. Resets pitch to 1.0 (consistent sound)
4. Uses `PlayOneShot()` for non-blocking playback

### Integration with Existing Systems

#### Magazine System
- Fire sound only plays when projectile successfully spawns
- No "dry fire" click when magazine empty (can be added if desired)
- Reload sound plays regardless of `reloadDuration` setting

#### Fire Rate Limiting
- Audio respects fire rate restrictions
- Rapid trigger pulls in `allowManualRapidFire` mode each play audio
- `PlayOneShot()` prevents audio cutoff during rapid fire

#### Pooling System
- Fire sound plays **after** projectile spawn succeeds
- Ensures audio matches visual feedback
- No sound if pool exhausted

## Performance Notes

- **CPU:** Negligible (~0.01ms per shot for `PlayOneShot` call)
- **Memory:** ~100KB per audio clip (compressed)
- **Audio Channels:** Uses Unity's audio channel system (typically 32 max)
- **3D Processing:** Handled by Unity's audio engine, minimal overhead

### Optimization Tips
- Use **mono** audio clips for 3D sounds (stereo unnecessary)
- Keep fire sound clips **short** (< 0.5s)
- Use **Compressed In Memory** or **Streaming** for longer reload sounds
- Avoid extremely high quality settings (70-80% sufficient for gameplay SFX)

## Troubleshooting

### No Audio Playing

**Check:**
1. Audio clips assigned in Inspector (`fireSound` for shots)
2. `audioVolume` > 0 in ProjectileWeapon
3. Unity Audio Listener present in scene (typically on camera/player)
4. Audio Source not muted/paused globally
5. System audio output device working

**Debug:**
```csharp
// Enable debug logging in ProjectileWeapon
logShots = true;
// Check console for "Fired projectile" messages
```

### Audio Pops or Clicks

**Causes:**
- Audio clip not looping properly (N/A for one-shots, but check clip settings)
- Sample rate mismatch with project settings
- Overly aggressive compression

**Solutions:**
- Ensure clips are normalized (prevent clipping)
- Match project audio sample rate (44.1kHz or 48kHz)
- Use PCM or ADPCM for fire sounds (less compression artifacts)

### Audio Too Loud/Quiet

**Adjust:**
1. `audioVolume` parameter in ProjectileWeapon (per-weapon)
2. Unity Audio Mixer settings (global)
3. AudioSource `minDistance`/`maxDistance` (affects falloff)

**Recommended Ranges:**
- Close range weapon: `audioVolume = 0.5-0.7`
- Heavy weapon: `audioVolume = 0.8-1.0`
- Silenced weapon: `audioVolume = 0.2-0.4`

### Audio Doesn't Follow Weapon

**Verify:**
1. `muzzle` transform is assigned and parented to hand controller
2. `audioSourceTransform` (if used) is moving with weapon
3. AudioSource not accidentally created on static parent

**Fix:**
- Ensure weapon GameObject hierarchy includes XR controller as ancestor
- AudioSource automatically attaches to muzzle if available

### Pitch Variation Sounds Unnatural

**Adjust:**
- Reduce `pitchVariation` (try 0.02-0.03 for subtlety)
- Increase for more variety (0.1-0.15)
- Set to 0.0 for no variation (pure consistency)

**Note:** Human perception of pitch is logarithmic; small values (0.05) are often sufficient.

## Code Examples

### Basic Setup (Inspector)
```
ProjectileWeapon Component:
  Fire Sound: LaserShot.wav
  Reload Sound: (none)
  Audio Volume: 0.7
  Pitch Variation: 0.05
  Audio Source Transform: (auto - uses muzzle)
```

### Adding "Dry Fire" Click
```csharp
// In TryFire(), where ammo is empty:
if (_currentAmmo <= 0)
{
    PlayDryFireSound(); // Custom method to add
    if (autoReloadOnEmpty)
    {
        BeginReload();
    }
    return;
}
```

### Multiple Fire Sounds (Random Selection)
```csharp
// Replace single fireSound with array:
[SerializeField] private AudioClip[] fireSounds;

// In PlayFireSound():
if (fireSounds != null && fireSounds.Length > 0)
{
    var clip = fireSounds[Random.Range(0, fireSounds.Length)];
    _audioSource.PlayOneShot(clip, audioVolume);
}
```

### Distance-Based Volume Scaling
```csharp
// In PlayFireSound(), adjust volume by distance to Audio Listener:
var listener = FindObjectOfType<AudioListener>();
if (listener != null)
{
    var distance = Vector3.Distance(_audioSource.transform.position, listener.transform.position);
    var volumeScale = Mathf.Clamp01(1f - (distance / _audioSource.maxDistance));
    _audioSource.PlayOneShot(fireSound, audioVolume * volumeScale);
}
```

## GameManager Integration (Future)

Currently audio plays whenever weapon fires (regardless of game state). To gate audio with GameManager:

```csharp
// In PlayFireSound():
if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
{
    return; // Don't play audio during pause/menu
}
```

Add using statement: `using Thrustslinger.Core;`

## Comparison: Weapon vs Thruster Audio

| Feature | Weapon Audio | Thruster Audio |
|---------|--------------|----------------|
| **Type** | One-shot clips | Looping clips |
| **Trigger** | Discrete events (fire/reload) | Continuous (grip held) |
| **Volume Control** | Fixed + pitch variation | Grip-intensity based |
| **Audio Sources** | Single shared | Per-hand separate |
| **Configuration** | Inspector parameters | ScriptableObject (ThrusterConfig) |
| **Pitch** | Randomized per shot | Smooth interpolation |

## Future Enhancements

Potential additions for v2:
- **Dry fire** click when magazine empty
- **Charge-up** sound for energy weapons
- **Ejected casing** clink sounds (pooled)
- **Impact feedback** audio (on projectile hit)
- **Ambient hum** for energy weapons (looping layer)
- **Reload variants** (tactical reload vs empty reload)
- **Distance attenuation curve** customization

---

**Implementation Complete!** 🔫🔊  
The ProjectileWeapon now features immersive 3D spatial audio for combat feedback, with simple inspector-based configuration and zero-allocation runtime performance.
