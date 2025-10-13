# Rain World Wallpaper Mod - Design Document

## Overview
Transform Rain World into a dynamic animated wallpaper that smoothly transitions between random locations across the world.

## Technical Architecture

### Core Components

#### 1. Room Selection System
- Access `game.world.abstractRooms` to get list of available rooms
- Filter for valid, explorable rooms (not gates, etc.)
- Random selection with history to avoid repeating recent rooms
- Support for region cycling

#### 2. Camera Control System
- Hook into `RoomCamera.Update()` or `RainWorldGame.Update()`
- Manipulate camera position through RoomCamera properties
- Smooth interpolation between positions

#### 3. Transition System
- **Stay Phase**: Camera stays at location for configurable duration
- **Transition Phase**: Smooth easing between locations
- **Easing Function**: Cubic ease-in-out for natural movement

### Key Rain World APIs

#### Room Management
```csharp
// Access rooms in current world
game.world.abstractRooms  // List of AbstractRoom

// Realize a room for viewing
abstractRoom.RealizeRoom(game.world, game)

// Room properties
room.abstractRoom.name  // Room identifier
room.cameraPositions[]  // Array of camera positions in room
```

#### Camera Control
```csharp
// Primary camera access
game.cameras[0]  // RoomCamera - main game camera

// Camera properties
roomCamera.pos  // Current camera position (Vector2)
roomCamera.room  // Current room being viewed

// Methods we'll hook
On.RainWorldGame.Update += Hook
On.RoomCamera.Update += Hook
```

### Implementation Strategy

#### Phase 1: Basic Room Switching
1. Hook into game update loop
2. Access current world's abstract rooms
3. Implement timer for stay duration
4. Switch to random room after timer expires
5. Realize new room when switching

#### Phase 2: Smooth Transitions
1. Store start and end camera positions
2. Implement transition timer
3. Apply easing function during transition
4. Interpolate camera position smoothly

#### Phase 3: World Traversal
1. Track current region
2. Select adjacent regions for transitions
3. Implement region switching logic
4. Handle cross-region camera transitions

#### Phase 4: Polish
1. Add configuration options (timings, regions)
2. Handle edge cases (player interference, etc.)
3. Optimize room loading/unloading
4. Add visual effects during transitions

## Safari Mode Comparison

**Safari Mode Features:**
- Manual control via Overseer
- Teleport with Jump button
- Navigate between adjacent rooms
- Can control creatures

**Our Wallpaper Mode:**
- Fully automatic
- Smooth animated transitions
- Random exploration
- No player input needed
- Focus on visual spectacle

## Configuration Options

### User Configurable
- `stayDuration`: Time at each location (default: 30s)
- `transitionDuration`: Transition time (default: 5s)
- `allowedRegions`: List of regions to explore
- `avoidGates`: Skip karma gates
- `randomSeed`: For reproducible sequences

### Internal Settings
- Room history size (avoid repeats)
- Minimum distance between selections
- Loading/unloading thresholds

## Technical Challenges

### Challenge 1: Room Loading Performance
**Problem**: Realizing rooms is expensive
**Solution**: Pre-load next room during stay phase

### Challenge 2: Camera Position Consistency
**Problem**: Different rooms have different camera setups
**Solution**: Use room.cameraPositions array, select best view

### Challenge 3: Cross-Region Transitions
**Problem**: Changing regions requires world reload
**Solution**: Fade transition, reload world, then fade in

### Challenge 4: Memory Management
**Problem**: Too many realized rooms
**Solution**: Aggressively abstractize old rooms

## Hooks Required

```csharp
// Main update loop
On.RainWorldGame.Update += RainWorldGame_Update;

// Camera control
On.RoomCamera.Update += RoomCamera_Update;

// Room loading
On.Room.Loaded += Room_Loaded;

// World initialization
On.RainWorld.OnModsInit += RainWorld_OnModsInit;

// Optional: Override player input to prevent interference
On.Player.Update += Player_Update;
```

## Future Enhancements

1. **Music Sync**: Transition timing synced to ambient music
2. **Creature Following**: Occasionally follow interesting creatures
3. **Weather Effects**: Respect rain cycles and weather
4. **Custom Camera Paths**: Not just static positions
5. **Region Storytelling**: Follow natural flow through world
6. **Performance Mode**: Lower quality for weaker systems
7. **Multi-Monitor Support**: Different rooms on different monitors
8. **Time-of-Day**: Respect cycle progression for ambiance

## Reference Mods

- **Warp**: Room teleportation mechanics
- **SBCameraScroll**: Camera position manipulation
- **Safari Mode**: Base inspiration for spectator experience
- **DevTools**: Room and camera debugging

## Development Phases

### Phase 1 (Current): Foundation ✓
- [x] Research modding framework
- [x] Study Safari mode
- [x] Create mod template
- [x] Design architecture

### Phase 2: Core Implementation
- [ ] Implement room selection
- [ ] Add basic room switching
- [ ] Test with single region

### Phase 3: Smooth Transitions
- [ ] Implement camera interpolation
- [ ] Add easing functions
- [ ] Test transition smoothness

### Phase 4: World Traversal
- [ ] Add multi-region support
- [ ] Implement region switching
- [ ] Test across entire world

### Phase 5: Polish & Release
- [ ] Add configuration UI
- [ ] Optimize performance
- [ ] Test edge cases
- [ ] Documentation
- [ ] Release v1.0
