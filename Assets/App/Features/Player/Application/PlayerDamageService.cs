using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Player.Domain
{
    public sealed class PlayerDamageService
    {
        private readonly float _invulnerabilityDuration;
        private readonly float _forcedMoveDuration;

        public PlayerDamageService(float invulnerabilityDuration, float forcedMoveDuration)
        {
            _invulnerabilityDuration = invulnerabilityDuration;
            _forcedMoveDuration = forcedMoveDuration;
        }

        /// <summary>
        /// ダメージを適用する。無敵中はスキップ。
        /// forceRelocate が true の場合（崩落タイル上）、安全マスへ強制移動。
        /// </summary>
        public bool ApplyDamage(
            PlayerModel player,
            int damage,
            bool forceRelocate,
            StageModel stage,
            SafeTileSearchService safeTileSearch,
            HashSet<GridPos> occupied)
        {
            if (player.Invulnerability.IsInvulnerable) return false;
            if (player.Stats.IsDead) return false;

            player.Stats.TakeDamage(damage);
            player.Invulnerability.Activate(_invulnerabilityDuration);

            if (forceRelocate)
            {
                var safeTile = safeTileSearch.FindSafeTile(stage, player.CurrentPosition, occupied);
                if (safeTile.HasValue)
                {
                    player.ForcedMove.Start(safeTile.Value, _forcedMoveDuration);
                    player.CurrentPosition = safeTile.Value;
                }
            }

            return true;
        }
    }
}
