using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Bombs.Domain
{
    public readonly struct FireBombResult
    {
        public readonly IReadOnlyList<GridPos> AffectedTiles;
        public readonly IReadOnlyList<GridPos> WallsDestroyed;
        public readonly int ContactDamage;
        public readonly float FireDuration;

        public FireBombResult(
            IReadOnlyList<GridPos> affectedTiles,
            IReadOnlyList<GridPos> wallsDestroyed,
            int contactDamage,
            float fireDuration)
        {
            AffectedTiles = affectedTiles;
            WallsDestroyed = wallsDestroyed;
            ContactDamage = contactDamage;
            FireDuration = fireDuration;
        }
    }

    public sealed class FireBombResolver
    {
        private readonly BombAreaResolver _areaResolver;

        public FireBombResolver(BombAreaResolver areaResolver)
        {
            _areaResolver = areaResolver;
        }

        public FireBombResult Resolve(GridPos landingPos, BombSpec spec, StageModel stage)
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
                        affectedTiles.Add(pos);
                        break;
                    // Collapsing, Collapsed, PermanentlyDestroyed はスキップ
                }
            }

            return new FireBombResult(affectedTiles, wallsDestroyed,
                spec.Damage, spec.Duration);
        }
    }
}
