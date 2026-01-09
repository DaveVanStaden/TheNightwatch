# Sanity Threshold Manager

## Overview
The `SanityThresholdManager` script enables GameObjects when the player's sanity drops below specific thresholds. This is perfect for spawning enemies, hallucinations, or environmental effects based on the player's mental state.

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

### 3. Example Configuration
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
```

This will:
- Enable ShadowEnemy_1 when sanity drops below 80
- Enable Hallucination_Whispers when sanity drops below 60
- Enable EnvironmentalDistortion when sanity drops below 40

### 4. Important Notes
- Each threshold triggers **only once** per game session
- Objects are automatically set to **inactive** at the start
- Thresholds are checked when sanity **drops below** the value (not at or below)
- The script uses reflection to access PlayerStats for compatibility

### 5. Debug Mode
Enable "Debug Log" in the Inspector to see console messages when:
- The manager initializes
- Each threshold is triggered
- Showing current sanity values

### 6. Context Menu Functions
Right-click the component in the Inspector to access:
- **Reset All Thresholds**: Re-arms all thresholds and disables all objects (useful for testing)

### 7. Manual Triggering (For Testing)
Call `TriggerThresholdByIndex(int index)` from code to manually trigger a specific threshold:
```csharp
GetComponent<SanityThresholdManager>().TriggerThresholdByIndex(0); // Triggers first entry
```

## Technical Details
- The script monitors `PlayerStats.Sanity` every frame
- Uses reflection for PlayerStats access to avoid compilation issues
- Caches property info for optimal performance
- Thread-safe and compatible with Unity 2021+

## Tips
- Make sure the referenced GameObjects exist before the SanityThresholdManager starts
- Set thresholds in descending order for better organization (80, 60, 40, etc.)
- Objects can have their own logic/AI - they just need to be inactive initially
- Consider using this with the existing HallucinationSpawner and ShadowLogic systems
