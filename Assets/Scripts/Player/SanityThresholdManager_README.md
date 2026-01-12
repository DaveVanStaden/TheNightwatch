# Sanity Threshold Manager

## Overview
The `SanityThresholdManager` script enables GameObjects when the player's sanity drops below specific thresholds. This is perfect for spawning enemies, hallucinations, or environmental effects based on the player's mental state.

**NEW**: Objects won't spawn if they're too close to the player, preventing visible pop-in and maintaining immersion!

## How to Use

### 1. Setup
1. Create an empty GameObject in your scene (e.g., "SanityThresholdManager")
2. Add the `SanityThresholdManager` component to it
3. The script will automatically find the PlayerStats component in the scene

### 2. Configure Threshold Entries
In the Inspector, expand "Threshold Entries" and add entries:
- **Object To Enable**: The GameObject to activate (enemy, effect, etc.)
- **Sanity Threshold**: The sanity value below which the object should spawn
  - Example: Set to 75 to enable when sanity drops below 75
  - Supports values from 0-100

### 3. Configure Spawn Prevention Settings
- **Min Spawn Distance** (default: 10m): Objects won't spawn if within this distance from the player
  - Prevents the player from seeing objects pop in
  - Higher values = more careful spawning, but might delay spawn longer
  - Recommended: 10-15m for most scenarios
  
- **Min/Max Spawn Delay** (default: 0.5-2 seconds): Random delay before spawning
  - Gives player time to naturally look away
  - Creates more organic spawn timing
  - Can be set to 0 if you only want distance checks
  
- **Spawn Check Interval** (default: 0.5 seconds): How often to check if object can spawn
  - Lower values = more responsive but slightly higher performance cost
  - 0.5s is a good balance for most games

### 4. Example Configuration
```
Entry 0:
  - Object To Enable: ShadowEnemy_1
  - Sanity Threshold: 80
  
Entry 1:
  - Object To Enable: Hallucination_Whispers
  - Sanity Threshold: 60
  
Entry 2:
  - Object To Enable: EnvironmentalDistortion
  - Sanity Threshold: 40

Spawn Prevention Settings:
  - Min Spawn Distance: 12
  - Min Spawn Delay: 0.5
  - Max Spawn Delay: 2.0
  - Spawn Check Interval: 0.5
```

This will:
- Enable ShadowEnemy_1 when sanity drops below 80 (if it's >12m from player)
- Enable Hallucination_Whispers when sanity drops below 60 (if it's >12m from player)
- Enable EnvironmentalDistortion when sanity drops below 40 (if it's >12m from player)

### 5. How Spawning Works
When a threshold is triggered:
1. **Initial delay**: Waits a random time (between min/max spawn delay)
2. **Distance check**: Checks if object is far enough from player
3. **Wait loop**: If too close, waits and checks again every `spawnCheckInterval` seconds
4. **Spawn**: Once far enough, enables the object

This ensures objects never visibly pop in front of the player!

### 6. Important Notes
- Each threshold triggers **only once** per game session
- Objects are automatically set to **inactive** at the start
- Thresholds are checked when sanity **drops below** the value (not at or below)
- Objects in the scene should be placed at their desired spawn location
- The script waits until they're far enough from the player before enabling

### 7. Debug Mode
Enable "Debug Log" in the Inspector to see console messages when:
- The manager initializes
- Thresholds are met and waiting to spawn
- Objects are too close (with distance info)
- Each threshold finally spawns

Enable "Show Spawn Radius Gizmo" to visualize:
- Orange wireframe sphere showing the minimum spawn distance around the player
- Yellow lines and spheres showing objects that are waiting to spawn

### 8. Context Menu Functions
Right-click the component in the Inspector to access:
- **Reset All Thresholds**: Re-arms all thresholds and disables all objects (useful for testing)
- **Force Spawn All Waiting Objects**: Immediately spawns any waiting objects, bypassing distance checks (testing)

### 9. Manual Triggering (For Testing)
Call `TriggerThresholdByIndex(int index)` from code to manually trigger a specific threshold:
```csharp
GetComponent<SanityThresholdManager>().TriggerThresholdByIndex(0); // Triggers first entry (bypasses distance check)
```

Or force all waiting objects to spawn:
```csharp
GetComponent<SanityThresholdManager>().ForceSpawnAllWaitingObjects();
```

## Best Practices

### Object Placement
- Place spawn objects at their desired spawn locations in the scene
- Keep them far enough from player spawn (>15m recommended)
- For enemies, consider placing them in rooms/areas the player hasn't visited yet

### Distance Settings
- **Close quarters/small rooms**: Use 8-10m spawn distance
- **Medium spaces**: Use 10-15m spawn distance
- **Large open areas**: Use 15-20m spawn distance

### Delay Settings
- **Quick spawns**: 0-0.5 seconds (more aggressive)
- **Natural spawns**: 0.5-2 seconds (recommended)
- **Slow burns**: 2-5 seconds (very cautious)

### Performance Tips
- Don't set spawn check interval too low (<0.3s) unless needed
- Limit the number of simultaneous waiting-to-spawn objects
- Consider disabling debug logging in production builds

## Technical Details
- Uses coroutines for spawn timing and distance checking
- Monitors `PlayerStats.Sanity` every frame for threshold detection
- Uses reflection for PlayerStats access to avoid compilation issues
- Caches property info for optimal performance
- Automatically finds player transform via PlayerManager, tag, or PlayerStats

## Integration with Other Systems
- Works seamlessly with HallucinationSpawner and ShadowLogic
- Compatible with the existing sanity drain system
- Can trigger objects with their own AI/behavior scripts
- Objects should have their own logic for despawning/disabling when appropriate
