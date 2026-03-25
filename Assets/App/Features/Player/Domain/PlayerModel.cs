using System;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Player.Domain
{
    public sealed class PlayerModel : IDisposable
    {
        private readonly ReactiveProperty<GridPos> _position;
        private readonly ReactiveProperty<Direction8> _facingDirection;

        public PlayerId Id { get; }
        public PlayerStats Stats { get; }
        public PlayerBuild Build { get; }
        public InvulnerabilityState Invulnerability { get; }
        public ForcedMoveState ForcedMove { get; }

        public ReadOnlyReactiveProperty<GridPos> Position => _position;
        public ReadOnlyReactiveProperty<Direction8> FacingDirection => _facingDirection;

        public GridPos CurrentPosition
        {
            get => _position.Value;
            set => _position.Value = value;
        }

        public Direction8 CurrentFacing
        {
            get => _facingDirection.Value;
            set => _facingDirection.Value = value;
        }

        public PlayerModel(PlayerId id, GridPos spawnPosition, PlayerStats stats, PlayerBuild build)
        {
            Id = id;
            Stats = stats;
            Build = build;
            Invulnerability = new InvulnerabilityState();
            ForcedMove = new ForcedMoveState();
            _position = new ReactiveProperty<GridPos>(spawnPosition);
            _facingDirection = new ReactiveProperty<Direction8>(Direction8.S);
        }

        public void Dispose()
        {
            _position.Dispose();
            _facingDirection.Dispose();
            Stats.Dispose();
            Build.Dispose();
        }
    }
}
