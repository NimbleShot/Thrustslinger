# Pool Service Scene Transition Fix

## Problem

When quitting to the main menu from a paused game and then starting a new game, a `MissingReferenceException` was thrown:

```
MissingReferenceException: The object of type 'Thrustslinger.Core.PooledObject' has been destroyed 
but you are still trying to access it.
```

### Root Cause

1. **PoolService is a Singleton with DontDestroyOnLoad** - It persists across scene loads
2. **Pooled objects are in the gameplay scene** - They get destroyed when the scene unloads
3. **Pool references weren't cleared** - The PoolService still had references to destroyed objects in its queues
4. **Scene transition via LoadSceneMode.Single** - This unloads the current scene and all its objects

### Error Flow

1. Player pauses game → Clicks "Quit to Main Menu"
2. GameManager calls `SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single)`
3. Unity destroys all objects in the gameplay scene (including pooled targets)
4. PoolService singleton persists, but its queues contain destroyed object references
5. Player starts new game → TargetSpawner tries to spawn targets
6. PoolService.Get() dequeues a destroyed PooledObject
7. Accessing `pooled.gameObject` throws MissingReferenceException

### Additional Issues Found

**Issue 2: Pool Registration Cleared**
- Initial fix cleared pool registrations (`_pools.Clear()`)
- New scene couldn't find registered pools
- Error: `[PoolService] No pool registered for key 'targets.default'`

**Issue 3: Active Objects Visible in Main Menu**
- Active pooled objects (flying targets) weren't deactivated before scene transition
- Since PoolService uses `DontDestroyOnLoad`, these objects persisted
- Result: Visible moving targets in the main menu scene

## Solution

### 1. Clear Pools on Scene Load

Added scene load detection to automatically clear pools when a new scene loads in Single mode:

```csharp
private void OnEnable()
{
    SceneManager.sceneLoaded += OnSceneLoaded;
}

private void OnDisable()
{
    SceneManager.sceneLoaded -= OnSceneLoaded;
}

private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
{
    // When a new scene loads in Single mode, clear all pools since objects are destroyed
    if (mode == LoadSceneMode.Single)
    {
        ClearAllPools();
    }
}
```

### 2. Added ClearAllPools Method

```csharp
public void ClearAllPools()
{
    foreach (var entry in _pools.Values)
    {
        // First, deactivate and return all in-use objects to prevent them appearing in next scene
        var inUseList = new List<PooledObject>(entry.InUse);
        foreach (var pooled in inUseList)
        {
            if (pooled != null && pooled.gameObject != null)
            {
                pooled.gameObject.SetActive(false);
                pooled.InvokeDespawned();
            }
        }
        
        // Clear queues and sets - objects are already destroyed by scene unload
        entry.Available.Clear();
        entry.InUse.Clear();
        
        // Destroy the container if it still exists
        if (entry.Container != null)
        {
            Destroy(entry.Container.gameObject);
            entry.Container = null;
        }
    }
    
    _lookup.Clear();
    // NOTE: We keep _pools and _defaultKey intact so registrations persist
    // The containers will be recreated when objects are spawned again
}
```

This ensures:
- **All active objects are deactivated** before scene transition (prevents visible targets in main menu)
- All pooled objects properly invoke their despawn callbacks
- All pool queues are emptied (destroyed object references removed)
- All lookup dictionaries are cleared
- Pool containers are destroyed
- **Pool registrations persist** - No need to re-register in new scene
- Containers are automatically recreated when spawning resumes

### 3. Added Safety Checks in Get Method

Enhanced the `Get` method with defensive programming to handle any edge cases:

```csharp
// Clean out any destroyed objects from the queue
while (entry.Available.Count > 0)
{
    var pooled = entry.Available.Peek();
    if (pooled == null || pooled.gameObject == null)
    {
        entry.Available.Dequeue();
        continue;
    }
    break;
}

// ... dequeue logic ...

// Final safety check
if (validPooled == null || validPooled.gameObject == null)
{
    Debug.LogWarning($"[PoolService] Dequeued a destroyed pooled object from '{key}'. Creating a new instance.", this);
    validPooled = CreateInstance(entry);
}
```

This provides a fallback that:
- Cleans destroyed objects from the queue before use
- Creates new instances if destroyed objects are encountered
- Logs warnings for debugging

### 4. Container Recreation in CreateInstance

Enhanced the `CreateInstance` method to handle destroyed containers:

```csharp
private PooledObject CreateInstance(PoolEntry entry)
{
    // Recreate container if it was destroyed (e.g., after scene transition)
    if (entry.Container == null)
    {
        var container = new GameObject($"{entry.Key}_Pool").transform;
        container.SetParent(EnsureRootContainer(), false);
        container.gameObject.hideFlags = HideFlags.DontSave;
        entry.Container = container;
    }
    
    // ... rest of instantiation logic ...
}
```

This ensures that pool containers are automatically recreated after scene transitions.

## Benefits

1. **Automatic Cleanup** - No manual intervention needed when transitioning scenes
2. **Robust** - Multiple layers of protection against destroyed references
3. **Persistent Registrations** - Pool registrations survive scene transitions, no need to re-register
4. **Scene Reloading** - Pools automatically recreate containers and objects when spawning resumes
5. **No Performance Impact** - Cleanup only happens during scene transitions (not per-frame)
6. **Backward Compatible** - Existing code continues to work without changes

## Testing Checklist

- [x] Quit to main menu from pause menu
- [x] Start new game after quitting
- [x] Verify targets spawn without errors
- [x] Test multiple quit → restart cycles
- [x] Verify pool registration happens automatically in new scene
- [x] Check that no destroyed object errors occur
- [x] Verify no visible targets/objects in main menu after quitting
- [x] Confirm all active objects are properly deactivated during scene transition

## Technical Notes

### Why Clear on LoadSceneMode.Single?

- **Single mode** - Unloads current scene(s), destroying all GameObjects
- **Additive mode** - Keeps existing scenes loaded, objects persist
- Only need to clear pools when objects are actually destroyed (Single mode)

### Why Not Use OnDestroy on Pooled Objects?

- OnDestroy happens AFTER the scene unload, when references are already invalid
- PoolService needs to clear references BEFORE attempting to access them
- Scene load callback happens at the right time in the Unity lifecycle

### Alternative Approaches Considered

1. **Don't use DontDestroyOnLoad for PoolService**
   - ❌ Would need to recreate service in every scene
   - ❌ Loses benefits of singleton pattern
   - ❌ More complex scene setup

2. **Make pooled objects DontDestroyOnLoad**
   - ❌ Objects would leak across scenes
   - ❌ Would need manual cleanup anyway
   - ❌ Breaks scene-based organization

3. **Clear pools in GameManager.QuitToMenu()**
   - ❌ Requires GameManager to know about PoolService internals
   - ❌ Doesn't handle other scene transition methods
   - ❌ Less automatic/more error-prone

The chosen approach (scene load callback) is the most robust and automatic solution.

## Files Modified

- `Assets/Scripts/Core/Pooling/PoolService.cs`
  - Added `using UnityEngine.SceneManagement;`
  - Added `OnEnable()` and `OnDisable()` lifecycle methods
  - Added `OnSceneLoaded()` callback
  - Modified `ClearAllPools()` to preserve pool registrations while clearing object references
  - Enhanced `Get()` method with destroyed object detection and cleanup
  - Enhanced `CreateInstance()` to recreate containers if destroyed

## Related Systems

This fix complements the pause menu functionality:
- Pause menu → Quit button → GameManager.QuitToMenu() → Scene load → Pool cleanup
- Scene load → Bootstrapper → Pool re-registration → Game starts clean
