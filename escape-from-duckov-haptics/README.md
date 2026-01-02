# Escape from Duckov - Haptic Feedback Mod

Controller vibration/haptic feedback for Escape from Duckov. Feel the recoil when you fire!

## Features

- **Fire Haptics**: Controller vibrates when you shoot
- **Configurable Intensity**: Adjust vibration strength to your preference
- **Configurable Duration**: Control how long each vibration lasts
- **Low Latency**: Direct XInput integration for minimal delay

## Requirements

- **Windows 8/10/11** (XInput required)
- **Xbox Controller** (or XInput-compatible controller)
- **Escape from Duckov** (Steam version)

## Installation

### Steam Workshop (Recommended)
1. Subscribe to the mod on Steam Workshop
2. Launch the game
3. Enable the mod in the Mods menu

### Manual Installation
1. Download the latest release
2. Extract to: `<Game Directory>/Duckov_Data/Mods/DuckovHaptics/`
3. Ensure the folder contains:
   - `DuckovHaptics.dll`
   - `info.ini`
4. Launch the game and enable in Mods menu

## Configuration

Configuration is auto-generated on first run at:
```
%APPDATA%/../LocalLow/DuckovHaptics/config.json
```

### Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `enabled` | Enable/disable the mod | `true` |
| `controllerIndex` | Controller slot (0-3) | `0` |
| `fireKey` | Key to monitor for shooting | `"Mouse0"` |

### Haptic Profiles

Each haptic event (fire, reload, damage) has these settings:

| Setting | Description | Range |
|---------|-------------|-------|
| `enabled` | Enable this feedback type | `true/false` |
| `lowFrequencyIntensity` | Left motor strength | `0.0 - 1.0` |
| `highFrequencyIntensity` | Right motor strength | `0.0 - 1.0` |
| `durationMs` | Vibration duration in milliseconds | `1 - 1000` |

### Example Config

```json
{
  "enabled": true,
  "controllerIndex": 0,
  "fireHaptics": {
    "enabled": true,
    "lowFrequencyIntensity": 0.6,
    "highFrequencyIntensity": 0.8,
    "durationMs": 80
  }
}
```

## Troubleshooting

### No vibration
1. Ensure controller is connected before launching game
2. Check controller works in other games
3. Verify `controllerIndex` matches your controller slot
4. Check Windows recognizes controller in "Set up USB game controllers"

### Wrong button triggers vibration
Edit `fireKey` in config. Valid values include:
- `Mouse0`, `Mouse1`, `Mouse2` (mouse buttons)
- `Space`, `LeftShift`, `E`, etc. (keyboard)
- See [Unity KeyCode](https://docs.unity3d.com/ScriptReference/KeyCode.html)

### Vibration feels delayed
- This is inherent to input monitoring approach
- Typical latency: 16-33ms (1-2 frames)
- Reduce in-game V-Sync for lower latency

## Building from Source

### Requirements
- .NET SDK 6.0+
- Escape from Duckov game files (for assembly references)

### Steps
1. Clone the repository
2. Update `.csproj` to reference your game's managed DLLs:
   ```xml
   <Reference Include="Duckov.Modding">
     <HintPath>C:\Path\To\Duckov_Data\Managed\Duckov.Modding.dll</HintPath>
   </Reference>
   ```
3. Build: `dotnet build -c Release`
4. Copy output to `Duckov_Data/Mods/DuckovHaptics/`

## License

MIT License - See LICENSE file

## Credits

- Team Soda for Escape from Duckov and modding support
- XInput documentation from Microsoft
