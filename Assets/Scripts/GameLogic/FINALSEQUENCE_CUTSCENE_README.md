# Final Sequence Cutscene System - PlayableDirector Implementation

## Overview
The Final Sequence cutscene system uses Unity's Timeline (PlayableDirector) on the Painter prefab to play a cinematic cutscene when the player enters the chase trigger. After the cutscene completes, the chase sequence begins.

## How It Works

### Flow Sequence:
1. **Player walks through chase trigger**
2. **Cutscene starts** - Player is frozen
3. **Timeline plays** - PlayableDirector on Painter plays the cutscene
4. **Cutscene ends** - Detected automatically by monitoring PlayableDirector state
5. **Player unfrozen** - Movement and camera restored
6. **"Spawn" animation triggered** - On Painter (if not handled by Timeline)
7. **Chase begins** - "Walk" trigger fired, PainterAI activated

## Setup Requirements

### Painter Prefab Setup:
```
Painter Prefab
?? PlayableDirector Component (REQUIRED)
?  ?? Timeline Asset: [Your cutscene timeline]
?? Animator Component
?  ?? "Spawn" trigger parameter
?  ?? "Walk" trigger parameter
?? PainterAI Component
```

### FinalSequenceManager Inspector:
```
FinalSequenceManager
?? Painter AI
?  ?? Painter Prefab: [Assign Painter with PlayableDirector]
?  ?? Painter Spawn Point: [Where painter spawns]
?? Chase Trigger Wall: [Trigger that starts cutscene]
?? Debug Logs: [true/false]
```

## Code Implementation

### Cutscene Coroutine:
```csharp
private IEnumerator CutsceneCoroutine()
{
    cutscenePlaying = true;
    
    // 1. Freeze player (no movement, camera, or interaction)
    FreezePlayer(true);
    
    // 2. Get PlayableDirector from Painter
    var playableDirector = painterInstance.GetComponent<PlayableDirector>();
    
    // 3. Play the Timeline cutscene
    playableDirector.Play();
    
    // 4. Wait for Timeline to finish
    while (playableDirector.state == PlayState.Playing)
    {
        yield return null;
    }
    
    // 5. Unfreeze player
    FreezePlayer(false);
    
    // 6. Trigger Spawn animation (if needed)
    animator.SetTrigger("Spawn");
    
    // 7. Start chase
    StartChase();
}
```

### Player Freeze System:
```csharp
private void FreezePlayer(bool freeze)
{
    if (freeze)
    {
        cachedPlayerManager.enabled = false;  // Stop movement
        cachedPlayerManager.PauseCamera();    // Stop camera rotation
    }
    else
    {
        cachedPlayerManager.enabled = true;   // Restore movement
        cachedPlayerManager.ResumeCamera();   // Restore camera
    }
}
```

### Chase Start:
```csharp
public void StartChase()
{
    chaseActive = true;
    
    // Trigger Walk animation
    animator.SetTrigger("Walk");
    
    // Activate AI chase behavior
    painterAI.SetChaseActive(true);
    
    // Spawn blocking objects
    ActivateObjects(chaseBlockingObjects);
    
    // Activate end sequence trigger
    endSequenceWall.SetActive(true);
}
```

## Timeline Setup Guide

### Your Timeline Should Include:
1. **Cinemachine Virtual Camera tracks** (for cutscene camera work)
2. **Painter Animation tracks** (for spawn/reveal animations)
3. **Audio tracks** (optional - for dramatic music/sounds)
4. **Signal tracks** (optional - for triggering additional effects)

### Timeline Duration:
- The system automatically detects when the Timeline finishes
- No need to set `cutsceneDuration` manually
- Duration is determined by your Timeline asset

### Camera Handling:
- Your Timeline can control the camera through Cinemachine
- Player camera is NOT disabled (Timeline overrides it)
- After Timeline ends, control returns to player camera automatically

## Animation Triggers

### "Spawn" Trigger:
- **When:** Triggered after cutscene ends (before chase starts)
- **Purpose:** Transition from idle/spawn pose to ready-to-chase pose
- **Note:** Can also be triggered within Timeline if preferred

### "Walk" Trigger:
- **When:** Triggered when `StartChase()` is called
- **Purpose:** Transition from idle/spawn to walking/chasing animation
- **Required:** Yes - tells animator to start movement animations

## Debug Logging

When `debugLogs = true`, you'll see:
```
[FinalSequence] CUTSCENE STARTED - Freezing player
[FinalSequence] Playing cutscene Timeline. Duration: 5.5s
[FinalSequence] Cutscene Timeline finished
[FinalSequence] CUTSCENE ENDED - Starting chase
[FinalSequence] Triggered 'Spawn' animation on Painter
[FinalSequence] CHASE STARTED!
[FinalSequence] Triggered 'Walk' animation on Painter
```

## Error Handling

### No PlayableDirector Found:
```
[FinalSequence] Painter has no PlayableDirector component! Cutscene will not play.
```
**Solution:** Add PlayableDirector to your Painter prefab and assign a Timeline asset

### Painter Instance Null:
```
[FinalSequence] Painter instance is null, cannot play cutscene!
```
**Solution:** Ensure `painterPrefab` is assigned in FinalSequenceManager inspector

### Cannot Freeze Player:
```
[FinalSequence] Cannot freeze player - PlayerManager not found
```
**Solution:** Chase trigger didn't detect player properly - check collider tags

## Timeline Best Practices

### Camera Setup:
1. Create Cinemachine Virtual Cameras for your shots
2. Add Camera Animation track to Timeline
3. Animate between vcams for dramatic camera moves
4. Timeline will automatically blend back to player camera on end

### Painter Animation:
1. Use Animation track to play custom spawn/reveal animations
2. Keep it short (3-5 seconds recommended)
3. End with painter in a "ready to chase" pose

### Audio:
1. Add dramatic sting when painter appears
2. Fade in chase music at the end
3. Use Audio Source track on painter or separate audio object

## Advanced Features

### Cutscene Skipping (Optional):
You can add skip functionality by detecting input during the cutscene:
```csharp
// In CutsceneCoroutine, add:
while (playableDirector.state == PlayState.Playing)
{
    if (Input.GetKeyDown(KeyCode.Space))  // Or any skip key
    {
        playableDirector.Stop();
        break;
    }
    yield return null;
}
```

### Subtitles/UI (Optional):
Add Signal tracks in your Timeline to trigger:
- Subtitle text
- On-screen prompts
- UI fades
- Screen effects

### Multiple Cutscene Variations:
Assign different Timeline assets based on conditions:
```csharp
if (painterInstance != null)
{
    var director = painterInstance.GetComponent<PlayableDirector>();
    // Switch timeline based on game state
    director.playableAsset = someCondition ? cutsceneA : cutsceneB;
    director.Play();
}
```

## Testing Checklist

- [ ] Painter prefab has PlayableDirector component
- [ ] Timeline asset is assigned to PlayableDirector
- [ ] Painter has Animator with "Spawn" and "Walk" triggers
- [ ] Chase trigger is setup in scene
- [ ] Player freezes when cutscene starts
- [ ] Timeline plays correctly
- [ ] Player unfreezes when cutscene ends
- [ ] "Spawn" trigger fires after cutscene
- [ ] Chase begins correctly with "Walk" trigger
- [ ] PainterAI chase behavior activates

## Common Issues

**Issue:** Cutscene plays but player can still move
- **Fix:** Ensure `FreezePlayer(true)` is called before Timeline plays

**Issue:** Timeline doesn't play
- **Fix:** Check PlayableDirector has a Timeline asset assigned

**Issue:** Camera doesn't return to player after cutscene
- **Fix:** Ensure Timeline has proper Cinemachine bindings

**Issue:** Chase doesn't start after cutscene
- **Fix:** Check that `StartChase()` is being called at end of coroutine

**Issue:** Cutscene loops infinitely
- **Fix:** Set Timeline's "Wrap Mode" to "None" or "Hold"

## Performance Notes

- Timeline playback is efficient (Unity native system)
- Player freeze has minimal overhead (just disables components)
- No manual camera switching needed (Timeline handles it)
- Cutscene state is properly reset on sequence reset

---

**Last Updated:** Implementation complete with PlayableDirector support
**Status:** ? Ready for use
