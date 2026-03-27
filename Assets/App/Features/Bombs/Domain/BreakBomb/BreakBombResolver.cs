using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Bombs.Domain
{
    public readonly struct BreakBombResult
    {
        public readonly IReadOnlyList<GridPos> AffectedTiles;
        public readonly IReadOnlyList<GridPos> WallsDestroyed;
        public readonly int Damage;
        public readonly float CollapseTime;
        public readonly float RecoveryTime;

        public BreakBombResult(
            IReadOnlyList<GridPos> affectedTiles,
            IReadOnlyList<GridPos> wallsDestroyed,
            int damage,
            float collapseTime,
            float recoveryTime)
        {
            AffectedTiles = affectedTiles;
            WallsDestroyed = wallsDestroyed;
            Damage = damage;
            CollapseTime = collapseTime;
            RecoveryTime = recoveryTime;
        }
    }

    public sealed class BreakBombResolver
    {
        private readonly BombAreaResolver _areaResolver;

        public BreakBombResolver(BombAreaResolver areaResolver)
        {
            _areaResolver = areaResolver;
        }

        public BreakBombResult Resolve(GridPos landingPos, BombSpec spec, StageModel stage)
        {
            var allTiles = _areaResolver.Resolve(landingPos, spec.EffectRange, spec.WallPenetration);

            var affectedTiles = new List<GridPos>();
            var wallsDestroyed = new List<GridPos>();

            foreach (var pos in allTiles)
            {
                var state = stage.GetTileState(pos);
                switch (state)
                {
                    case TileState.Wall:
                        wallsDestroyed.Add(pos);
                        affectedTiles.Add(pos);
                        break;
                    case TileState.Normal:
                    case TileState.OnFire:
                    case TileState.Collapsing:
                    case TileState.Collapsed:
                        affectedTiles.Add(pos);
                        break;
                    // PermanentlyDestroyed はスキップ
                }
            }

            return new BreakBombResult(affectedTiles, wallsDestroyed,
                spec.Damage, spec.CollapseTime, spec.RecoveryTime);
        }
    }
}
