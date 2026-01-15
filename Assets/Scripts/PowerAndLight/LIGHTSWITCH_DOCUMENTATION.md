# LightSwitch Shadow Integration & Power Dependency

## Overview
The `LightSwitch` script integrates with:
1. **Power System** - Requires connected breaker/fuse to be ON to function
2. **Shadow System** - Lights protect player from shadows when ON

## Power Dependency System

### How It Works
The light switch is **completely dependent** on its connected `BreakerButton` (fuse):

#### ? **Can Turn Lights ON When:**
- Connected fuse is ON
- Lights are not broken (OR fuse is ON which resets broken state)

#### ? **Cannot Turn Lights ON When:**
- Connected fuse is OFF (plays feedback sound, no state change)

#### ?? **Auto-Off Scenarios:**
1. **Fuse turned OFF while lights ON** ? Lights immediately turn off
2. **Lights break** ? Fuse automatically turns OFF, lights turn off
3. **Player toggles switch while power OFF** ? Nothing happens (feedback sound plays)

### Code Implementation

**Power Check Before Toggle:**
```csharp
// Cannot turn lights ON if fuse is OFF
if (!lightsOn && connectedFuse != null && !connectedFuse.GetState())
{
    Debug.Log("Cannot turn lights on - fuse is OFF");
    PlaySound(switchOffClip); // Feedback
    return; // Prevent toggle
}
```

**Monitor Power State (Update Loop):**
```csharp
// If fuse is turned off while lights are on, force lights off
if (lightsOn && connectedFuse != null && !connectedFuse.GetState())
{
    ForceOffDueToPowerLoss();
}
```

**Force Off Method:**
```csharp
private void ForceOffDueToPowerLoss()
{
    lightsOn = false;
    // Turn off all lights
    // Stop sanity gain
    // Stop break checking
    // Re-enable shadow spawning
    // Play off sound
}
```

## Shadow Protection System

### When Lights Turn ON (and have power):
- ? All spawned shadows **immediately destroyed**
- ? Shadow spawning **disabled**
- ? Player is **safe** from shadows
- ? Sanity gain begins

### When Lights Turn OFF (or power lost):
- ? Shadow spawning **re-enabled**
- ? Player **vulnerable** to shadows
- ? Sanity gain stops

### Final Quest Integration:
- When finale activates ? Shadows **permanently disabled**
- No shadow interference during final sequence

## Gameplay Flow

### 1. Normal Operation
```
Player approaches switch ? Checks fuse state
?? Fuse ON ? Can toggle lights
?  ?? Lights ON ? Shadows disabled, sanity gain
?  ?? Lights OFF ? Shadows enabled, no sanity
?? Fuse OFF ? Cannot turn on (feedback sound only)
```

### 2. Remote Power Control
```
Lights ON ? Someone turns fuse OFF remotely
?? Lights immediately turn OFF
   ?? Shadows re-enabled
      ?? Player must turn fuse back ON to use lights
```

### 3. Light Breaking
```
Lights ON ? Break randomly
?? Lights turn OFF
?? Fuse automatically turns OFF (with animation)
?? Player must:
   ?? Turn fuse back ON
   ?? Toggle switch (resets broken state)
```

## Technical Details

### New Fields
```csharp
private HallucinationSpawner shadowSpawner;  // Reference to spawner
private bool shadowsDisabledByThisSwitch;    // Track if this switch disabled spawning
```

### New Methods
```csharp
private void Update()                        // Monitor fuse state
private void ForceOffDueToPowerLoss()        // Handle power loss while on
```

### Modified Methods
```csharp
private void Toggle()                        // Added power check
private void BreakLights()                   // Toggles fuse off (with animation)
private void OnDestroy()                     // Re-enable shadows if needed
```

## Debug Logging

All actions are logged:
```
[LightSwitch] Cannot turn lights on - connected fuse 'FuseA' is OFF
[LightSwitch] Connected fuse turned off - forcing lights OFF on LightSwitch_Hallway
[LightSwitch] Fuse is on - resetting broken state on LightSwitch_Hallway
[LightSwitch] disabled shadow spawning (lights ON)
[LightSwitch] re-enabled shadow spawning (lights OFF)
```

## Testing Checklist

### Power Dependency Tests
- [ ] Fuse ON ? Can toggle lights ?
- [ ] Fuse OFF ? Cannot turn lights on (feedback sound) ?
- [ ] Fuse OFF while lights ON ? Lights turn off immediately ?
- [ ] Fuse ON after being OFF ? Can turn lights on again ?
- [ ] Lights break ? Fuse turns off (with animation) ?
- [ ] Turn fuse ON after break ? Can use lights (broken state reset) ?

### Shadow Integration Tests
- [ ] Lights ON ? Shadow destroyed ?
- [ ] Lights ON ? Shadow spawning disabled ?
- [ ] Lights OFF ? Shadow spawning re-enabled ?
- [ ] Fuse OFF while lights ON ? Shadows re-enabled ?
- [ ] Final quest ? Shadows permanently disabled ?

### Edge Cases
- [ ] Multiple switches on same fuse ? All respect fuse state ?
- [ ] Switch destroyed while lights ON ? Shadows re-enabled ?
- [ ] Rapid toggle attempts when fuse OFF ? No state corruption ?

## Configuration

### Inspector Fields
- **Lights** - Array of Light components to control
- **Connected Fuse** - BreakerButton that powers this switch
- **Initial Sanity Boost** - Instant sanity when lights turn on
- **Sanity Per Second** - Continuous sanity gain while lit
- **Break Chance Settings** - Probability of lights breaking over time
- **Audio Clips** - On/Off/Break sound effects

### Recommended Settings
- **Break Check Interval:** 1 second
- **Initial Break Chance:** 0.0001 (0.01%)
- **Break Chance Increase:** 0.0001 per second
- **Max Break Chance:** 0.01 (1%)
- **Max Time Before Break:** 300 seconds (5 minutes)

## Important Notes

?? **Always assign a Connected Fuse** for proper power dependency
?? **Fuse must be BreakerButton type** for Toggle animation to work
?? **Multiple switches** can share the same fuse
?? **Power loss** during gameplay creates tension and strategic choices
?? **Shadow protection** requires continuous power - risk vs reward
