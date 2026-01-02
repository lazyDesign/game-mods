# Escape from Duckov - Gamepad Haptic Feedback Mod

## Project Overview

**Goal**: Develop a mod for "Escape from Duckov" that provides controller vibration/haptic feedback when firing weapons, despite the game lacking native gamepad support.

**Current Setup**:
- Game: Escape from Duckov (Steam, supports Steam Workshop)
- Controller Input: Steam Input translating gamepad → keyboard
- Missing Feature: No haptic/vibration feedback on fire actions

---

## Technical Context

The player uses Steam Input to map gamepad controls to keyboard inputs. Since the game has no native gamepad awareness, it cannot trigger controller vibration. This mod must bridge that gap by detecting fire events and triggering haptic feedback through available APIs.

---

## Phase 1: Research & Feasibility Analysis

### 1.1 Game Architecture Investigation

Before writing code, research and document:

1. **Modding Framework**: What modding system does Escape from Duckov use?
   - Check Steam Workshop for existing mods to understand mod structure
   - Look for official modding documentation or community wikis
   - Identify the scripting language (Lua, C#, Python, proprietary?)
   - Determine if mods can execute external code or access system APIs

2. **Game Engine**: Identify the engine (Unity, Unreal, Godot, custom?)
   - This determines available hooking methods and API access
   - Unity games often allow more extensive modding via BepInEx/MelonLoader

3. **Fire Event Detection**: How can we detect when the player fires?
   - In-game event hooks (preferred if available)
   - Input monitoring (keyboard key press detection)
   - Memory/state reading (complex, last resort)

### 1.2 Haptic Feedback Pathways

Research viable methods to trigger controller vibration:

| Method | Pros | Cons | Feasibility |
|--------|------|------|-------------|
| **Steam Input API** | Native Steam integration, works with Steam Input config | Requires Steamworks SDK access | Research if mod can call |
| **XInput API** | Direct Xbox controller support | Only Xbox/XInput controllers | Widely documented |
| **SDL2 Haptics** | Cross-platform | Requires SDL2 library | If game uses SDL |
| **DS4Windows/Similar** | External tool handles vibration | User must install additional software | Backup option |

### 1.3 Deliverables for Phase 1

Document findings in `/docs/research.md`:
- [ ] Confirmed modding framework and capabilities
- [ ] Game engine identified
- [ ] List of accessible game events (especially fire/shoot events)
- [ ] Viable haptic feedback method selected
- [ ] Kill criteria: If mods cannot execute system calls or access controller APIs, pivot to external tool approach

---

## Phase 2: Proof of Concept

### 2.1 Minimal Viable Implementation

Build the simplest possible version that demonstrates core functionality:
```
[Fire Event Detected] → [Trigger Vibration] → [Vibration Stops After Duration]
```

**POC Requirements**:
- Detect ANY fire event (even just one weapon type)
- Trigger ANY vibration (even fixed intensity/duration)
- Confirm round-trip works without crashing game

### 2.2 Architecture Decision

Based on Phase 1 research, implement one of these architectures:

**Option A: Native Mod (Preferred)**
```
Game Mod
├── Hook into fire event
├── Call Steam Input / XInput API
└── Trigger haptic feedback directly
```

**Option B: Hybrid Approach**
```
Game Mod                     External Process
├── Detect fire event        ├── Listen for signal
├── Write to shared file/    ├── Read signal
│   named pipe/localhost     ├── Call vibration API
└── Signal fire occurred     └── Trigger controller
```

**Option C: External Tool Only (Fallback)**
```
External Process
├── Monitor keyboard input (fire key)
├── When fire key pressed
└── Trigger controller vibration via XInput
```

### 2.3 Deliverables for Phase 2

- [ ] Working POC that vibrates controller on fire
- [ ] Performance baseline (no noticeable input lag)
- [ ] Document any game stability issues

---

## Phase 3: Full Implementation

### 3.1 Feature Requirements

| Feature | Priority | Description |
|---------|----------|-------------|
| Basic fire vibration | P0 | Vibrate on any weapon fire |
| Weapon-specific haptics | P1 | Different vibration patterns per weapon type |
| Configurable intensity | P1 | User-adjustable vibration strength |
| Configurable duration | P2 | User-adjustable vibration length |
| Reload feedback | P2 | Subtle vibration on reload |
| Damage received feedback | P2 | Vibration when player takes damage |
| Settings persistence | P1 | Save user preferences |

### 3.2 Configuration System

Create user-configurable settings:
```
config/
├── haptics_config.json (or appropriate format for mod framework)
    ├── enabled: true/false
    ├── fire_intensity: 0.0-1.0
    ├── fire_duration_ms: 50-500
    ├── weapon_profiles: {...}
    └── controller_type: "auto" | "xbox" | "playstation" | "steam"
```

### 3.3 Code Quality Standards

- Comment all non-obvious logic
- Handle controller disconnection gracefully
- Fail silently if haptics unavailable (don't crash game)
- Log errors to file for debugging
- Test with multiple controller types if possible

---

## Phase 4: Steam Workshop Packaging

### 4.1 Workshop Requirements

- [ ] Follow Escape from Duckov's Workshop submission guidelines
- [ ] Create preview image (Workshop thumbnail)
- [ ] Write clear description with:
  - What the mod does
  - Known compatible controllers
  - Configuration instructions
  - Troubleshooting steps
- [ ] Tag appropriately (gameplay, controller, accessibility)

### 4.2 Documentation

Create user-facing documentation:
- `README.md`: Installation and usage
- `CONFIGURATION.md`: All settings explained
- `TROUBLESHOOTING.md`: Common issues and solutions
- `CHANGELOG.md`: Version history

---

## Development Guidelines

### Coding Approach

1. **Start minimal**: Get basic vibration working before adding features
2. **Test incrementally**: Verify each feature before moving to next
3. **Preserve game stability**: Mod should never crash or degrade game performance
4. **Fail gracefully**: If controller disconnects or API fails, handle silently

### File Structure (Adapt to actual mod framework)
```
escape-from-duckov-haptics/
├── src/
│   ├── main.[ext]           # Entry point, hooks into game
│   ├── haptics_controller   # Vibration API wrapper
│   ├── event_detector       # Fire/damage event detection
│   └── config_manager       # Settings load/save
├── config/
│   └── default_config       # Default settings
├── docs/
│   ├── research.md          # Phase 1 findings
│   └── technical_notes.md   # Implementation details
├── README.md
└── workshop/
    ├── preview.png
    └── description.txt
```

### Testing Checklist

- [ ] Vibration triggers on fire
- [ ] No vibration when not firing
- [ ] Vibration stops appropriately (no stuck vibration)
- [ ] Works after controller reconnection
- [ ] Settings load correctly
- [ ] Settings changes apply without restart (if possible)
- [ ] No performance degradation
- [ ] Works in all game modes/maps
- [ ] Compatible with other popular mods (if applicable)

---

## Reference Resources

Research these during development:
- Steam Input API documentation (Steamworks)
- XInput API (Windows)
- Game's modding documentation/wiki
- Existing Workshop mods (examine structure)
- Community modding forums/Discord for the game

---

## Decision Points & Kill Criteria

| Checkpoint | Continue If | Pivot/Stop If |
|------------|------------|---------------|
| After 1.1 | Mods can hook game events | No modding API access |
| After 1.2 | At least one haptic method viable | No way to trigger vibration from mod |
| After 2.1 | POC works with acceptable latency | >50ms latency makes feedback feel disconnected |
| After 3.1 | Core features stable | Frequent crashes or conflicts |

**Fallback Plan**: If native mod approach fails, develop standalone external tool that monitors fire key input and triggers vibration independently of game modding system.

---

## Session Start Checklist

When starting a development session:
1. Confirm current phase and next task
2. Review any blockers from previous session
3. State specific goal for this session
4. After completing work, document progress and next steps
