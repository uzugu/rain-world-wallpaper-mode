# Rain World Wallpaper Mode - V2.0 Progress

## What We've Built So Far

### ✅ Core Architecture (Complete)

**1. WallpaperProcess.cs**
- Custom ProcessManager.Process for standalone mode
- Handles fade in/out transitions (0.45s standard)
- ESC key exits to main menu
- Manages game initialization and cleanup
- Integrates with RegionManager and WallpaperHUD

**2. RegionManager.cs**
- Tracks all 20 Rain World regions (vanilla + Downpour)
- Shuffles regions for random exploration
- Switches regions after 20 rooms
- Maintains exploration progress
- Queues next regions automatically

**3. WallpaperHUD.cs**
- Auto-hiding HUD with 3-second fade delay
- Mouse movement detection to show/hide
- Displays:
  - Current location (region + room)
  - Next location (configurable)
  - Previous location (configurable)
  - Region progress (X/20 regions, Y/20 rooms)
- Smooth alpha fade animations (0.5s)
- Configurable options ready for Remix menu

### 📋 What's Ready

#### Features Implemented:
✅ Process system foundation
✅ All regions defined and ready
✅ Auto-hiding HUD with mouse detection
✅ Fade in/out animations
✅ ESC to exit
✅ Region shuffling and queueing
✅ Progress tracking
✅ Configurable HUD options (structure ready)

#### Code Quality:
✅ Well-commented
✅ Modular design
✅ Error logging
✅ Clean architecture
✅ Easy to extend

## What's Still TODO

### 🔧 Integration Tasks

**1. Connect to Existing Room System**
- Merge with current WallpaperMod.cs room transition code
- Hook WallpaperProcess into the smooth camera transitions
- Connect room selection to RegionManager
- Link room explored events

**2. Menu Integration**
- Research MainMenu SimpleButton API
- Add "Wallpaper Mode" button to main menu
- Hook button click to launch WallpaperProcess
- Alternative: Add to Collections menu

**3. Game Initialization**
- Initialize RainWorldGame without player
- Set up overseer-style camera
- Load region properly
- Handle world creation

**4. Configuration System**
- Create WallpaperConfig.cs (OptionInterface)
- Add to Remix menu
- Implement options:
  - Stay duration slider
  - Transition duration slider
  - Rooms per region slider
  - Show next/previous toggles
  - Always show HUD toggle
  - HUD fade delay slider
  - HUD color picker

### 📐 Technical Challenges

**Challenge 1: ProcessManager.ProcessID**
- Current code uses `ProcessID.Game`
- May need custom ProcessID enum extension
- Or reuse existing IDs creatively

**Challenge 2: Menu Button API**
- Need to examine Menu.SimpleButton class
- Understand MenuScene button positioning
- Hook click events properly

**Challenge 3: Game Without Player**
- Need to init RainWorldGame without slugcat
- Similar to Safari mode but automated
- May need to hook Player initialization

**Challenge 4: Cross-Region Transitions**
- World unload/reload between regions
- Handle fade during reload
- Maintain camera continuity

## Current File Structure

```
RainWorldWallpaperMod/
├── WallpaperMod.cs           # Original plugin (V1.0)
├── WallpaperProcess.cs       # NEW: Custom process
├── RegionManager.cs          # NEW: Region handling
├── WallpaperHUD.cs           # NEW: HUD system
├── RainWorldWallpaperMod.csproj
├── assets/
│   └── modinfo.json
└── lib/
    ├── UnityEngine.dll
    ├── UnityEngine.CoreModule.dll
    ├── UnityEngine.InputLegacyModule.dll
    ├── Assembly-CSharp.dll
    └── HOOKS-Assembly-CSharp.dll
```

## Next Steps (Priority Order)

### Step 1: Merge with Existing Code
**Goal**: Connect new architecture to working room transitions

**Tasks**:
1. Update WallpaperMod.cs to use WallpaperProcess
2. Move room transition logic into WallpaperProcess
3. Connect RegionManager.OnRoomExplored() calls
4. Test basic functionality

**Why First**: This gets us a functional system quickly

### Step 2: Test Without Menu
**Goal**: Make it work via F9 toggle with new architecture

**Tasks**:
1. Temporary F9 to launch WallpaperProcess
2. Verify all new features work
3. Test HUD fade and mouse detection
4. Fix any bugs

**Why Second**: Validate architecture before menu work

### Step 3: Add Menu Integration
**Goal**: Professional menu button

**Tasks**:
1. Research Menu.SimpleButton
2. Hook MainMenu constructor
3. Add "Wallpaper Mode" button
4. Wire up to WallpaperProcess

**Why Third**: Polish after core works

### Step 4: Configuration
**Goal**: User customization

**Tasks**:
1. Create WallpaperConfig.cs
2. Add OptionInterface
3. Wire up to Remix
4. Test all options

**Why Last**: Core features first, then customization

## Testing Strategy

### Phase 1: Unit Testing
- Test RegionManager shuffling
- Test HUD fade logic
- Test mouse detection

### Phase 2: Integration Testing
- Test process switching
- Test region transitions
- Test HUD with real game

### Phase 3: User Testing
- Long-duration runs
- All regions coverage
- Performance monitoring
- Bug hunting

## Timeline Estimate

**Week 1**: Integration + Testing (Steps 1-2)
- Merge code
- Get it working end-to-end
- Fix bugs

**Week 2**: Menu Integration (Step 3)
- Research menu API
- Add button
- Test menu flow

**Week 3**: Configuration (Step 4)
- Build Remix interface
- Test options
- Polish

**Week 4**: Polish + Release
- Bug fixes
- Documentation
- Release V2.0

## Success Metrics

✅ Can launch from main menu with one click
✅ Explores all 20 regions automatically
✅ HUD shows/hides on mouse movement
✅ Smooth transitions between rooms and regions
✅ Configurable via Remix
✅ No crashes during extended runs
✅ 60 FPS maintained
✅ Professional user experience

## Current Status

**Version**: 2.0-alpha (Foundation)
**Phase**: Architecture Complete, Integration Pending
**Working**: Core classes built and ready
**Not Working**: Not yet connected to game
**Next Milestone**: Functional prototype via F9 toggle

---

**Files Created**:
- WallpaperProcess.cs (180 lines)
- RegionManager.cs (150 lines)
- WallpaperHUD.cs (230 lines)

**Total New Code**: ~560 lines

**Ready for**: Integration testing
