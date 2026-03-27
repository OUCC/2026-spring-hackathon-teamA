using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Player.Application
{
    public sealed class PlayerDamageService
    {
        private readonly float _invulnerabilityDuration;
        private readonly float _forcedMoveDuration;
        private readonly StageModel _stage;
        private readonly SafeTileSearchService _safeTileSearch;

        public PlayerDamageService(
            float invulnerabilityDuration,
            float forcedMoveDuration,
            StageModel stage,
            SafeTileSearchService safeTileSearch)
        {
            _invulnerabilityDuration = invulnerabilityDuration;
            _forcedMoveDuration = forcedMoveDuration;
            _stage = stage;
            _safeTileSearch = safeTileSearch;
        }

        /// <summary>
        /// ダメージを適用する。無敵中はスキップ。
        /// forceRelocate が true の場合（崩落タイル上）、安全マスへ強制移動。
        /// ignoreInvulnerability が true の場合、無敵を無視してダメージを適用し無敵も発動しない（ステージ縮小用）。
        /// </summary>
        public bool ApplyDamage(
            PlayerModel player,
            int damage,
            bool forceRelocate,
            HashSet<GridPos> occupied,
            bool ignoreInvulnerability = false)
        {
            if (!ignoreInvulnerability && player.Invulnerability.IsInvulnerable) return false;
            if (player.Stats.IsDead) return false;

            player.Stats.TakeDamage(damage);
            if (!ignoreInvulnerability)
                player.Invulnerability.Activate(_invulnerabilityDuration);

            if (forceRelocate)
            {
                var safeTile = _safeTileSearch.FindSafeTile(_stage, player.CurrentPosition, occupied);
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
