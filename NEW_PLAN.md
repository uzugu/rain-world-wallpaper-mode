# Rain World Wallpaper Mode - Redesign Plan

## Vision
A true standalone game mode that automatically explores the entire Rain World universe as a live wallpaper/screensaver.

## Core Requirements

### 1. Standalone Game Mode
**Goal**: Add "Wallpaper Mode" to main menu (like Safari, Arena, Campaign)

**Implementation**:
- Add menu button to MainMenu
- Create custom ProcessManager.ProcessID
- Initialize game without player control
- Load overseer-style camera system

**Challenges**:
- Menu API research needed
- ProcessManager integration
- Custom game initialization

**Alternative** (simpler): Add to Collections menu or modify Safari mode

### 2. All Regions Available
**Goal**: Explore every region in the game automatically

**Implementation**:
- Pre-load list of all available regions
- Create region queue/random selector
- Implement seamless region transitions
- Handle region-specific slugcat variations

**Regions to support**:
- SU (Outskirts)
- HI (Industrial Complex)
- CC (Chimney Canopy)
- GW (Garbage Wastes)
- SH (Shaded Citadel)
- DS (Drainage System)
- SL (Shoreline)
- SI (Sky Islands)
- LF (Farm Arrays)
- UW (The Exterior)
- SS (Five Pebbles)
- SB (Subterranean)
- And all Downpour regions!

**Transition Flow**:
```
Stay at room (30s) → Transition to new room (5s) →
After 20 rooms → Fade out (2s) →
Load new region → Fade in (2s) →
Continue exploring
```

### 3. Auto-Hiding HUD
**Goal**: Minimal, elegant HUD that doesn't interfere with the view

**HUD Elements**:
```
┌─────────────────────────────────────────┐
│ [Fade in on mouse move]                 │
│                                         │
│ Current: Outskirts - SU_A23            │
│ Next: Outskirts - SU_B12               │
│ Previous: Outskirts - SU_A14           │
│                                         │
│ Region: 3/15 | Rooms: 8/20             │
│                                         │
│ [Auto-fade after 3 seconds]            │
└─────────────────────────────────────────┘
```

**Behavior**:
- **Default**: Fully transparent after 3 seconds idle
- **On mouse move**: Fade in to full opacity (0.5s animation)
- **Reset timer**: Every mouse movement resets the 3s countdown
- **Smooth transitions**: Use alpha blending

**Implementation**:
- Track mouse position delta
- Alpha animation system
- Configurable fade delay
- Optional: Always show minimal info

### 4. Configuration System
**Goal**: User customization through Remix menu

**Options Structure**:
```csharp
public class WallpaperConfig : OptionInterface
{
    // Timing
    public Configurable<float> StayDuration;      // Default: 30s
    public Configurable<float> TransitionDuration; // Default: 5s
    public Configurable<int> RoomsPerRegion;      // Default: 20

    // HUD Settings
    public Configurable<bool> ShowNextLocation;    // Default: true
    public Configurable<bool> ShowPreviousLocation; // Default: false
    public Configurable<bool> AlwaysShowHUD;       // Default: false
    public Configurable<float> HUDFadeDelay;       // Default: 3s

    // Region Selection
    public Configurable<bool> ExploreAllRegions;   // Default: true
    public Configurable<string> SpecificRegions;   // Comma-separated list

    // Visual Options
    public Configurable<bool> ShowCreatures;       // Default: true
    public Configurable<bool> ShowEffects;         // Default: true
    public Configurable<Color> HUDColor;           // Default: cyan
}
```

**Remix Menu Layout**:
```
┌──────────────────────────────────────┐
│ Rain World Wallpaper Mode Settings  │
├──────────────────────────────────────┤
│ [Timing]                             │
│   Stay Duration: [30s] slider        │
│   Transition: [5s] slider            │
│   Rooms per Region: [20] slider      │
│                                      │
│ [HUD Display]                        │
│   ☑ Show Next Location              │
│   ☐ Show Previous Location          │
│   ☐ Always Show HUD                 │
│   HUD Fade Delay: [3s] slider       │
│                                      │
│ [Regions]                            │
│   ☑ Explore All Regions             │
│   Custom Regions: [_____________]    │
│                                      │
│ [Visual]                             │
│   ☑ Show Creatures                  │
│   ☑ Show Effects                    │
│   HUD Color: [████] picker          │
└──────────────────────────────────────┘
```

## Implementation Phases

### Phase 1: Menu Integration (Week 1)
**Tasks**:
- [ ] Research MainMenu/ProcessManager API
- [ ] Add "Wallpaper Mode" button to main menu
- [ ] Create WallpaperProcess class
- [ ] Handle menu → wallpaper mode transition
- [ ] Handle exit back to menu

**Deliverable**: Can launch into wallpaper mode from menu

### Phase 2: Multi-Region System (Week 2)
**Tasks**:
- [ ] Create region list/database
- [ ] Implement region queue system
- [ ] Add region loading logic
- [ ] Implement fade transitions
- [ ] Test region switching stability

**Deliverable**: Smoothly transitions between all regions

### Phase 3: HUD System (Week 3)
**Tasks**:
- [ ] Create HUD container class
- [ ] Implement mouse tracking
- [ ] Add fade in/out animations
- [ ] Display current/next/previous rooms
- [ ] Add region progress indicators

**Deliverable**: Functional auto-hiding HUD

### Phase 4: Configuration (Week 4)
**Tasks**:
- [ ] Create OptionInterface implementation
- [ ] Add all configuration options
- [ ] Implement option loading/saving
- [ ] Test all combinations
- [ ] Add tooltips/help text

**Deliverable**: Full Remix configuration menu

### Phase 5: Polish & Release (Week 5)
**Tasks**:
- [ ] Performance optimization
- [ ] Bug fixes
- [ ] User testing
- [ ] Documentation
- [ ] Release v2.0

## Technical Architecture

### Class Structure
```
WallpaperMod (BaseUnityPlugin)
├── WallpaperProcess (ProcessManager.Process)
│   ├── WallpaperGame (manages game state)
│   ├── WallpaperCamera (camera controller)
│   └── RegionManager (handles region switching)
├── WallpaperHUD (HUD system)
│   ├── LocationDisplay
│   ├── ProgressDisplay
│   └── MouseTracker
└── WallpaperConfig (OptionInterface)
    └── Configuration options
```

### Key Components

**WallpaperProcess**:
- Custom ProcessManager process
- Initializes world without player
- Manages overseer-style camera
- Handles region transitions

**RegionManager**:
- Maintains list of all regions
- Tracks exploration progress
- Handles region loading/unloading
- Manages transition timing

**WallpaperHUD**:
- Creates FLabel elements
- Tracks mouse movement
- Animates alpha values
- Updates location info

**WallpaperConfig**:
- Extends OptionInterface
- Saves to config file
- Provides UI in Remix menu

## Data Flow

```
Main Menu
    ↓ (User selects Wallpaper Mode)
WallpaperProcess Initialize
    ↓
Load First Region
    ↓
Create Camera & HUD
    ↓
┌─────────────────────────┐
│   Main Loop:            │
│   1. Stay at room (30s) │
│   2. Select next room   │
│   3. Smooth transition  │
│   4. After 20 rooms:    │
│      - Fade out         │
│      - Load new region  │
│      - Fade in          │
│   5. Update HUD         │
│   6. Repeat             │
└─────────────────────────┘
    ↓ (User presses ESC)
Return to Main Menu
```

## File Structure

```
RainWorldWallpaperMod/
├── WallpaperMod.cs           # Main plugin
├── WallpaperProcess.cs       # Game mode process
├── WallpaperGame.cs          # Game state manager
├── RegionManager.cs          # Region handling
├── WallpaperHUD.cs           # HUD system
├── WallpaperConfig.cs        # Configuration
├── MenuIntegration.cs        # Menu hooks
└── Utilities/
    ├── FadeTransition.cs     # Fade effects
    ├── MouseTracker.cs       # Mouse detection
    └── RegionDatabase.cs     # Region list
```

## Success Criteria

✅ **Accessibility**: One click from main menu
✅ **Coverage**: All regions accessible
✅ **Performance**: Smooth 60 FPS
✅ **UX**: Intuitive, minimal HUD
✅ **Customization**: Full user control via Remix
✅ **Stability**: No crashes, proper memory management
✅ **Polish**: Beautiful transitions, professional feel

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|---------|-----------|
| Menu API complexity | High | Use simpler Collection menu alternative |
| Region loading performance | Medium | Async loading, memory management |
| Mouse detection issues | Low | Fallback to always-show HUD |
| Configuration complexity | Medium | Start with essential options only |

## Timeline

**Week 1**: Menu integration research & implementation
**Week 2**: Multi-region system
**Week 3**: HUD system
**Week 4**: Configuration
**Week 5**: Polish & release

**Total**: ~5 weeks for full implementation

## Next Steps

1. Research MainMenu/ProcessManager API
2. Create simplified prototype of menu button
3. Test region loading system
4. Build HUD prototype
5. Implement configuration basics

---

**Status**: Planning Phase
**Target Version**: 2.0.0
**Original Version**: 1.0.0 (F9 toggle prototype)
