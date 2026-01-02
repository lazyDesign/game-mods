# Phase 1 Research Findings

## 1.1 Game Architecture

### Game Engine
**Confirmed: Unity Engine**
- Process name: `Escape From Duckov.exe`
- Data folder: `Duckov_Data/`
- Managed assemblies: `Duckov_Data/Managed/*.dll`

### Modding Framework
**Official modding support via C# Class Libraries**

| Aspect | Details |
|--------|---------|
| Language | C# |
| Target Framework | netstandard2.1 |
| Base Class | `Duckov.Modding.ModBehaviour` (inherits MonoBehaviour) |
| Lifecycle | Unity events (Start, Update, etc.) + game-specific events |
| Deployment | `Duckov_Data/Mods/<ModName>/` or Steam Workshop |

### Mod Structure Requirements
```
<ModName>/
├── <ModName>.dll      # Compiled mod
├── info.ini           # Required - contains "name=<Namespace>"
└── preview.png        # Optional - Workshop thumbnail
```

### Available Game APIs
- `ItemStatsSystem.ItemAssetsCollection` - Item management
- `SodaCraft.Localizations.LocalizationManager` - Localization
- Unity MonoBehaviour lifecycle events
- Game-specific event registration (to be discovered)

### Fire Event Detection Strategy
1. **Primary**: Hook into game weapon/shooting events if available
2. **Fallback**: Monitor Unity Input for fire key presses
3. **Last resort**: Poll game state for weapon firing status

---

## 1.2 Haptic Feedback Pathways

### Recommended: XInput Direct API (Windows)

| Pros | Cons |
|------|------|
| Direct hardware access | Windows only |
| No Unity Input System dependency | Xbox/XInput controllers only |
| Reliable, well-documented | Requires DllImport |
| Works regardless of game's input handling | - |

**Implementation:**
```csharp
[DllImport("xinput1_4.dll")]
static extern int XInputSetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

struct XINPUT_VIBRATION {
    public ushort wLeftMotorSpeed;   // Low frequency (0-65535)
    public ushort wRightMotorSpeed;  // High frequency (0-65535)
}
```

### Alternative: Unity Input System

| Pros | Cons |
|------|------|
| Cross-platform potential | May not be available in game's assemblies |
| Cleaner API | Requires Gamepad.current to be valid |
| PS4/Xbox support | Steam Input may intercept gamepad |

**Implementation:**
```csharp
Gamepad.current?.SetMotorSpeeds(0.5f, 0.5f);  // Low, High frequency
Gamepad.current?.ResetHaptics();               // Stop vibration
```

### Decision: Dual Approach
1. Try Unity Input System first (if available)
2. Fall back to XInput direct API for Windows
3. Fail gracefully if neither works

---

## 1.3 Feasibility Assessment

| Checkpoint | Status | Notes |
|------------|--------|-------|
| Mods can hook game events | ✅ PASS | ModBehaviour + Unity lifecycle |
| Game engine identified | ✅ PASS | Unity |
| Haptic method viable | ✅ PASS | XInput + Unity Input System |
| Can execute system calls | ✅ PASS | C# DllImport supported |

**Verdict: PROCEED TO PHASE 2**

---

## References

- [Official Duckov Modding API](https://github.com/xvrsl/duckov_modding/)
- [Community Mod Repository](https://github.com/Oeddish/Duckov)
- [Unity Input System - Gamepad Support](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.5/manual/Gamepad.html)
- [XInput API Documentation](https://github.com/MicrosoftDocs/sdk-api/blob/docs/sdk-api-src/content/xinput/nf-xinput-xinputsetstate.md)
- [Steam Workshop](https://steamcommunity.com/app/3167020/workshop/)
