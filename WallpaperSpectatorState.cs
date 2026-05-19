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
            if (game?.Players == null || game.Players.Count == 0)
            {
                return;
            }

            var playersToRemove = new List<AbstractCreature>();
            foreach (var abstractPlayer in game.Players)
            {
                if (abstractPlayer?.state is PlayerState playerState)
                {
                    if (playerState.isPup || abstractPlayer.creatureTemplate.type.ToString() == "SlugNPC")
                    {
                        continue;
                    }
                }

                playersToRemove.Add(abstractPlayer);
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

                game.Players.Remove(abstractPlayer);
            }
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
