using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Player.Application
{
    public sealed class PlayerMoveService
    {
        private readonly WarpService _warpService;

        public PlayerMoveService(WarpService warpService = null)
        {
            _warpService = warpService;
        }

        public bool TryMove(PlayerModel player, Direction8 direction, StageModel stage)
        {
            if (player.ForcedMove.IsForced) return false;

            player.CurrentFacing = direction;

            var target = player.CurrentPosition.Neighbor(direction);

            if (!stage.IsInBounds(target)) return false;
            if (!IsMovable(player, target, stage)) return false;

            player.CurrentPosition = target;

            // ワープチェック
            CheckWarp(player);

            return true;
        }

        /// <summary>
        /// ダッシュ: 2マス瞬間移動。経路上の炎・崩落タイルのダメージを無視する。
        /// 壁・永久消滅・範囲外では停止。
        /// </summary>
        public bool TryDash(PlayerModel player, Direction8 direction, StageModel stage)
        {
            if (!player.Build.HasDash) return false;
            if (player.ForcedMove.IsForced) return false;

            player.CurrentFacing = direction;

            var first = player.CurrentPosition.Neighbor(direction);

            // 1マス目が通過不可なら失敗
            if (!stage.IsInBounds(first)) return false;
            if (IsSolidBlock(first, stage)) return false;

            var second = first.Neighbor(direction);

            // 2マス目が通過不可なら1マス目に着地
            if (!stage.IsInBounds(second) || IsSolidBlock(second, stage))
            {
                player.CurrentPosition = first;
                CheckWarp(player);
                return true;
            }

            player.CurrentPosition = second;
            CheckWarp(player);
            return true;
        }

        private void CheckWarp(PlayerModel player)
        {
            if (_warpService == null) return;
            var dest = _warpService.TryGetWarpDestination(player.CurrentPosition);
            if (dest.HasValue)
                player.CurrentPosition = dest.Value;
        }

        /// <summary>
        /// 通常移動の通行判定。浮遊中は Collapsing/Collapsed も歩ける。
        /// </summary>
        private static bool IsMovable(PlayerModel player, GridPos target, StageModel stage)
        {
            if (stage.IsPassable(target)) return true;

            // 風の羽衣: 崩落タイルを歩ける
            if (player.Stats.LevitationActive.CurrentValue)
            {
                var cond = stage.GetTileCondition(target);
                return cond == TileCondition.Collapsing || cond == TileCondition.Collapsed;
            }

            return false;
        }

        /// <summary>
        /// ダッシュ時の壁・永久消滅判定（通過不可の固いブロック）。
        /// </summary>
        private static bool IsSolidBlock(GridPos pos, StageModel stage)
        {
            var data = stage.GetTileData(pos);
            return TileData.IsImpassableType(data.Type)
                || data.Condition == TileCondition.PermanentlyDestroyed;
        }
    }
}
