using System.Collections.Generic;

namespace RainWorldWallpaperMod
{
    internal sealed class WallpaperSpectatorState
    {
        private const int PlayerCheckInterval = 40;

        private int playerCheckCounter;

        public bool IsPrepared { get; private set; }

        public bool EnsurePrepared(RainWorldGame game, WallpaperRoomTracker roomTracker)
        {
            if (game?.world == null || game.cameras == null || game.cameras.Length == 0)
            {
                return false;
            }

            foreach (var camera in game.cameras)
            {
                if (camera != null)
                {
                    camera.followAbstractCreature = null;
                }
            }

            if (!IsPrepared)
            {
                // Remove communication targets before any creature AI receives its first
                // wallpaper-mode update. OverseerCommunicationModule assumes a player exists.
                RemoveActivePlayers(game);
                RemoveActiveOverseers(game);
            }

            playerCheckCounter++;
            if (playerCheckCounter >= PlayerCheckInterval)
            {
                playerCheckCounter = 0;
                RemoveActivePlayers(game);
                RemoveActiveOverseers(game);
            }

            if (IsPrepared)
            {
                return false;
            }

            roomTracker?.Reset();
            IsPrepared = true;
            return true;
        }

        public void Reset()
        {
            IsPrepared = false;
            playerCheckCounter = 0;
        }

        private void RemoveActivePlayers(RainWorldGame game)
        {
            if (game == null)
            {
                return;
            }

            var playersToRemove = new List<AbstractCreature>();
            if (game.Players != null)
            {
                foreach (var abstractPlayer in game.Players)
                {
                    if (IsPlayerControlledSlugcat(abstractPlayer) && !playersToRemove.Contains(abstractPlayer))
                    {
                        playersToRemove.Add(abstractPlayer);
                    }
                }
            }

            // A campaign player can be removed from game.Players before its room
            // realizes. In that case Rain World still owns the AbstractCreature in
            // AbstractRoom.creatures and realizes it later as a floating slugcat.
            // Scan the rooms too so both ownership paths are cleared. SlugNPC pups
            // use a different creature template and are intentionally preserved.
            if (game.world?.abstractRooms != null)
            {
                foreach (var room in game.world.abstractRooms)
                {
                    if (room?.creatures == null)
                    {
                        continue;
                    }

                    foreach (var creature in room.creatures)
                    {
                        if (IsPlayerControlledSlugcat(creature) && !playersToRemove.Contains(creature))
                        {
                            playersToRemove.Add(creature);
                        }
                    }
                }
            }

            if (playersToRemove.Count == 0)
            {
                return;
            }

            foreach (var abstractPlayer in playersToRemove)
            {
                if (abstractPlayer?.realizedCreature is global::Player realizedPlayer)
                {
                    realizedPlayer.RemoveFromRoom();
                    realizedPlayer.Destroy();
                }

                if (game.Players != null)
                {
                    game.Players.Remove(abstractPlayer);
                }

                abstractPlayer?.Room?.RemoveEntity(abstractPlayer);
            }
        }

        private static bool IsPlayerControlledSlugcat(AbstractCreature creature)
        {
            return creature?.creatureTemplate?.type != null &&
                creature.creatureTemplate.type.ToString() == "Slugcat";
        }

        private void RemoveActiveOverseers(RainWorldGame game)
        {
            if (game?.world?.abstractRooms == null)
            {
                return;
            }

            foreach (var room in game.world.abstractRooms)
            {
                if (room?.creatures == null || room.creatures.Count == 0)
                {
                    continue;
                }

                var overseersToRemove = new List<AbstractCreature>();
                foreach (var creature in room.creatures)
                {
                    if (creature?.creatureTemplate?.type?.ToString() == "Overseer")
                    {
                        overseersToRemove.Add(creature);
                    }
                }

                foreach (var overseer in overseersToRemove)
                {
                    if (overseer.realizedCreature != null)
                    {
                        overseer.realizedCreature.RemoveFromRoom();
                        overseer.realizedCreature.Destroy();
                    }

                    room.creatures.Remove(overseer);
                }
            }
        }
    }
}
