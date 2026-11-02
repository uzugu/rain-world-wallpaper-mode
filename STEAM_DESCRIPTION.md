# Steam Workshop Description Draft

[h1]Rain World Wallpaper Mode[/h1]

hey! this mod is basically a way to let Rain World play itself while you watch.

It turns the game into a hands-free animated wallpaper, moving through rooms and regions automatically with smooth camera transitions. I made it mostly for having Rain World running in the background, taking screenshots, relaxing with the ambience, or using the game as an actual desktop wallpaper.

[h2]How to start it[/h2]

1. Subscribe and enable the mod in Remix.
2. Go back to the main menu.
3. Press the new [b]WALLPAPER MODE[/b] button.

The mod will load the campaign and starting region selected in its Remix settings, remove the player from the camera, and begin exploring on its own.

[h2]What it does[/h2]

[list]
[*]Automatically explores random rooms with smooth camera movement.
[*]Changes regions using the active rain cycle, so sessions do not all move at exactly the same pace.
[*]Supports the base game, Downpour and The Watcher campaigns and regions.
[*]Uses the proper region list for the selected slugcat, including Rubicon for Saint.
[*]Can include compatible modded regions exposed through Custom Regions Support.
[*]Has four camera modes: Random Exploration, Single Random, All Positions and First Only.
[*]Includes quick travel for campaigns, regions and individual rooms.
[*]Supports echoes naturally and keeps their music from becoming distorted.
[*]Includes optional Chaos Mode if you want rooms to be more alive and full of creatures.
[*]Includes Watcher environment modes: Natural Mix, Clean, Rotted, Karma Flowers and Rot + Karma.
[*]Works with keyboard and controller controls.
[/list]

[h2]HUD and live settings[/h2]

The blue text is the Wallpaper Mode HUD. By default it fades after a few seconds without input and comes back whenever you use a control.

[b]H[/b] hides it immediately or switches it back to always-visible mode. You can also change its fade delay in Remix.

Press [b]F1[/b] or [b]Tab[/b] at any time to open the live settings and quick-travel menu. Most options can be changed without restarting the session.

[h2]Keyboard controls[/h2]

[list]
[*][b]Right Arrow / D[/b] - next room
[*][b]Left Arrow / A[/b] - previous room
[*][b]Up Arrow / W[/b] - next camera position
[*][b]Down Arrow / S[/b] - previous camera position
[*][b]N[/b] - immediate random room
[*][b]G / B[/b] - next or previous region
[*][b]L[/b] - lock the current room
[*][b]H[/b] - hide or show the HUD
[*][b]F1 / Tab[/b] - live settings and quick travel
[*][b]Escape[/b] - return to the main menu
[/list]

Controller prompts automatically replace the keyboard hints when a controller is being used.

[h2]Compatibility[/h2]

The normal base-game DLCs are supported. Modded regions can appear when their authors correctly mark them as Story or Optional regions through Custom Regions Support.

Chaos Mode's [b]Spawn All[/b] option is experimental and can still make some creature combinations unstable, so use that one for fun rather than for a perfectly reliable long session.

I am still looking into conflicts with certain gameplay mods, especially Expedition or Jolly Co-op setups. If Wallpaper Mode gets stuck loading, please tell me which mods you have enabled and their order.

[h2]Bug reports and feedback[/h2]

There is a pinned bug discussion on this Workshop page. If something breaks, please include:

[list]
[*]the selected campaign and region
[*]your enabled mods and their order
[*]what happened immediately before the problem
[*]the relevant part of [b]BepInEx/LogOutput.log[/b], if possible
[/list]

thank you to everyone who has been reporting strange rooms, wrong names, crashes and compatibility problems. A lot of the fixes and new options came directly from those comments :D

Inspired by Rain World's Safari mode by Videocult.

---

# Update Announcement Draft

## Title

Update 1.1.6 - region support, safer rooms and HUD fixes

## Body

hey! this update is mostly built from all the feedback people have been leaving in the comments and bug discussion, thank you again for helping me find all of this :D

[list]
[*]Region lists are now campaign-aware instead of forcing every region into every slugcat.
[*]Rubicon is now available for Saint.
[*]Watcher region names are read correctly instead of showing duplicated or placeholder names.
[*]Compatible modded regions can now be discovered through Custom Regions Support.
[*]Starting rooms are validated before loading, which should prevent several invalid-region loading problems.
[*]Unsafe story rooms such as RM_AI, other Oracle chambers and the off-screen den are no longer selected by random exploration or quick travel.
[*]The blue HUD now auto-hides by default on new configurations, and pressing H hides it immediately.
[*]Controller support and controller-specific control hints have been improved.
[*]Added the Rot + Karma Flowers Watcher state as a proper option.
[*]Added better logs for campaign, region, start-room and mod compatibility problems.
[/list]

After the update, please disable and re-enable the mod in Remix or restart Rain World. Remix can sometimes keep the previous plugin state active even while showing the new version number.

I am still investigating the infinite-loading reports involving some Expedition/Jolly Co-op setups, so please keep sending your enabled mod list and log if that still happens. I do not want to pretend that one is fixed until I can reproduce it properly.

thanks again, and please keep using the bug discussion if you find anything weird!
