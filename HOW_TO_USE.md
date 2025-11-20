# Rain World Wallpaper Mode - How to Use

## Updated Standalone Mode!

The mod now works as a **toggle-able wallpaper mode** that you can turn on/off with a keypress!

## How to Use

### Step 1: Launch Rain World
- Start Rain World normally through Steam
- Enable the mod in **Remix → Mod Management** if not already enabled

### Step 2: Start Any Game Mode
You can use wallpaper mode in:
- **Campaign** (any slugcat)
- **Safari Mode** (recommended - already spectator mode)
- **Arena** (if desired)

### Step 3: Activate Wallpaper Mode
Once in-game:
1. **Press F9** to activate Wallpaper Mode
2. You'll see a console message: "Wallpaper Mode ACTIVATED"
3. The mod will now:
   - **Block ALL player input** (you can't control anything)
   - **Automatically explore rooms** with smooth transitions
   - **Travel through the entire region**
   - Show the world as a living, animated wallpaper

### Step 4: Watch the World Unfold
- Camera will stay at each location for **30 seconds**
- Then smoothly transition to a new random room over **5 seconds**
- Avoids repeating the last 10 rooms visited
- After exploring 20 rooms, it prepares to switch regions (TODO)

### Step 5: Deactivate (Optional)
- **Press F9 again** to turn off wallpaper mode
- You'll see: "Wallpaper Mode DEACTIVATED"
- Normal gameplay resumes (player control restored)

## What It Does

### When Active:
✅ Blocks player input completely
✅ Randomly selects rooms to visit
✅ Smooth cubic easing between locations
✅ Remembers recent rooms to avoid repeats
✅ Automatically manages memory (loads/unloads rooms)
✅ Picks random camera angles in each room
✅ Tracks regions for future multi-region support

### When Inactive:
- Normal Rain World gameplay
- Mod is dormant and doesn't affect anything

## Best Way to Experience It

### Recommended Setup:
1. **Start Safari Mode** (already spectator-friendly)
2. Select any region you want to explore
3. Once loaded, **press F9**
4. Sit back and watch!

### Alternative - Campaign Mode:
1. Start any campaign
2. Get to a safe location
3. **Press F9**
4. Watch the game explore on its own
5. **Press F9** again when you want to play normally

## Configuration

Current settings (hardcoded in WallpaperMod.cs):

```csharp
transitionDuration = 5f;        // 5 second transitions
stayDuration = 30f;             // 30 seconds per location
MAX_HISTORY = 10;               // Remember last 10 rooms
ROOMS_PER_REGION = 20;          // Explore 20 rooms before region switch
toggleKey = KeyCode.F9;         // F9 to toggle on/off
```

To change these, edit the source code and rebuild.

## Troubleshooting

### "Nothing happens when I press F9"
- Make sure you're in-game (not in menus)
- Check that a world is loaded
- Look at BepInEx console for messages

### "Camera doesn't move"
- Wait up to 30 seconds for first transition
- Make sure wallpaper mode activated (check console)
- Verify you're not in a menu

### "I can't control my character"
- Press F9 to deactivate wallpaper mode
- Player control returns immediately

### "Mod doesn't appear in Remix"
- Check installation: `E:\SteamLibrary\steamapps\common\Rain World\RainWorld_Data\StreamingAssets\mods\vrmakes.wallpapermod\`
- Verify `modinfo.json` exists
- Verify `plugins/RainWorldWallpaperMod.dll` exists

## Controls

| Key | Action |
| --- | --- |
| **F9** | Toggle Wallpaper Mode ON/OFF |
| **F1 / Tab** | Toggle Settings Overlay |
| **H** | Toggle HUD visibility |
| **L** | Lock/Unlock current room (pauses timer) |
| **Right Arrow / D** | Jump to **Next Room** (Random) |
| **Left Arrow / A** | Jump to **Previous Room** (History) |
| **Up / Down / W / S** | Cycle **Camera Positions** in current room |
| **G** | Force jump to **Next Region** |
| **B** | Force jump to **Previous Region** |
| **Esc** | Return to Main Menu |

## Features

### Chaos Mode & Slugpups 🐱
- **Chaos Mode**: Spawns random creatures in the room for a lively environment.
- **Slugpups**: Now spawn **very frequently**! Look out for them playing around.
- **Scavengers**: Also spawn frequently to interact with the world.

### Navigation
- **Smart History**: Use Left Arrow/A to go back to rooms you just visited.
- **Camera Control**: Use Up/Down to see different angles of the same room.
- **Room Lock**: Press L to stay in a beautiful room as long as you want. The timer pauses!

### Rain Cycle
- The mod tracks the rain cycle.
- When the day ends, it automatically switches to a new region to avoid the rain (unless the room is locked!).

## Configuration
You can adjust settings in the **Remix** menu or use the in-game **Overlay (F1/Tab)**.

---

**Mod ID**: `vrmakes.wallpapermod`
**Version**: 1.1.0
**Status**: ✅ Fully Functional with Navigation & Chaos

Enjoy your Rain World wallpaper experience! 🌧️
