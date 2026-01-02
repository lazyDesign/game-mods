# Technical Implementation Notes

## Architecture Overview

```
ModBehaviour (Entry Point)
├── Start() - Initialize config and haptics controller
├── Update() - Poll input and manage vibration timing
└── OnDestroy() - Clean up vibration state

HapticsController (XInput Wrapper)
├── XInputSetState() - Send vibration commands
├── XInputGetState() - Check controller availability
└── Vibrate/StopVibration - High-level API

HapticsConfig (Settings)
├── Load() - Read from persistent storage
├── Save() - Write to persistent storage
└── Default values for all settings
```

## Fire Detection Strategy

### Current Implementation: Input Polling
```csharp
bool isFirePressed = Input.GetKey(_fireKeyCode);
if (isFirePressed && !_wasFirePressed) {
    TriggerFireHaptics();
}
```

**Pros:**
- Works regardless of game's internal event system
- Simple, reliable implementation
- No reverse engineering required

**Cons:**
- Slight latency (1-2 frames)
- Doesn't know about weapon state (ammo, cooldown)

### Future Enhancement: Game Event Hooks
If Duckov exposes weapon/combat events, hook directly:
```csharp
// Pseudo-code - actual API TBD
GameEvents.OnWeaponFire += (weapon) => TriggerFireHaptics();
GameEvents.OnPlayerDamaged += (damage) => TriggerDamageHaptics();
```

## XInput Integration

### Why XInput over Unity Input System?
1. **Reliability**: Direct Windows API, no Unity abstraction layer
2. **Compatibility**: Works even if game doesn't initialize Unity gamepad
3. **Steam Input**: Steam translates gamepad→keyboard, but XInput still works directly

### API Usage
```csharp
[DllImport("xinput1_4.dll")]
static extern int XInputSetState(int index, ref XINPUT_VIBRATION vibration);

struct XINPUT_VIBRATION {
    ushort wLeftMotorSpeed;   // 0-65535
    ushort wRightMotorSpeed;  // 0-65535
}
```

### Motor Characteristics
| Motor | Position | Frequency | Feel |
|-------|----------|-----------|------|
| Left | Usually left grip | Low (~20-150Hz) | Deep rumble |
| Right | Usually right grip | High (~150-500Hz) | Sharp buzz |

**Weapon fire recommendation:** Higher right motor for "snap" feel

## Configuration Storage

Location: `%APPDATA%/../LocalLow/DuckovHaptics/config.json`

Using Unity's `Application.persistentDataPath` ensures:
- Survives game updates
- Per-user settings
- Standard Unity mod convention

## Error Handling Philosophy

1. **Never crash the game** - All XInput calls wrapped in try/catch
2. **Fail silently** - Log warnings but continue operation
3. **Auto-recover** - Periodically recheck controller availability
4. **Clean shutdown** - Always stop vibration on mod unload

## Known Limitations

1. **Windows only** - XInput is Windows API
2. **Xbox controllers** - XInput doesn't support PlayStation natively
3. **Input polling** - Can't detect weapon state, only key press
4. **Single controller** - Currently supports one controller at a time

## Future Improvements

### Priority 1: Game Event Integration
- Research Duckov's event system
- Hook into actual weapon fire events
- Add weapon-specific vibration profiles

### Priority 2: Extended Controller Support
- Add DualShock 4 support via HID
- Steam Input API integration for universal support

### Priority 3: Advanced Haptics
- Continuous vibration for automatic weapons
- Intensity scaling with weapon damage
- Directional feedback for hits

## Testing Checklist

- [ ] Controller connected at game start
- [ ] Controller connected mid-game
- [ ] Controller disconnected mid-game
- [ ] Rapid fire (no stuck vibration)
- [ ] Config changes apply
- [ ] Game exit cleans up vibration
- [ ] Multiple controllers (index 0-3)
