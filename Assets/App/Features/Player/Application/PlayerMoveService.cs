using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Player.Domain
{
    public sealed class PlayerMoveService
    {
        public bool TryMove(PlayerModel player, Direction8 direction, StageModel stage)
        {
            if (player.ForcedMove.IsForced) return false;

            player.CurrentFacing = direction;

            var target = player.CurrentPosition.Neighbor(direction);

            if (!stage.IsInBounds(target)) return false;
            if (!stage.IsPassable(target)) return false;

            player.CurrentPosition = target;
            return true;
        }
    }
}
