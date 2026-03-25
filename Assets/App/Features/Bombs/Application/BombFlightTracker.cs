using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Bombs.Application
{
    internal struct BombFlightState
    {
        public bool IsFlying;
        public BombFlightCommand Command;
        public float DistanceAccumulator;
        public int CurrentTileDistance;
    }

    public sealed class BombFlightTracker
    {
        private readonly BombLaunchUseCase _launchUseCase;
        private readonly BombCooldownState _p1Cooldown;
        private readonly BombCooldownState _p2Cooldown;
        private readonly StageModel _stage;
        private readonly SlimeRegistry _slimeRegistry;
        private readonly float _flightSpeed;

        private BombFlightState _p1Flight;
        private BombFlightState _p2Flight;

        public BombFlightTracker(
            BombLaunchUseCase launchUseCase,
            BombCooldownState p1Cooldown,
            BombCooldownState p2Cooldown,
            StageModel stage,
            SlimeRegistry slimeRegistry,
            IBalanceParameters balance)
        {
            _launchUseCase = launchUseCase;
            _p1Cooldown = p1Cooldown;
            _p2Cooldown = p2Cooldown;
            _stage = stage;
            _slimeRegistry = slimeRegistry;
            _flightSpeed = balance.BombFlightSpeed;
        }

        public bool IsFlying(PlayerId player)
        {
            return GetFlight(player).IsFlying;
        }

        public bool StartFlight(PlayerId owner, GridPos origin, Direction8 direction, BombSpec spec)
        {
            if (IsFlying(owner)) return false;
            if (!GetCooldown(owner).CanFire(spec.Type)) return false;

            var cmd = new BombFlightCommand(origin, direction, spec, owner);
            SetFlight(owner, new BombFlightState
            {
                IsFlying = true,
                Command = cmd,
                DistanceAccumulator = 0f,
                CurrentTileDistance = 0,
            });
            return true;
        }

        public void ReleaseBomb(PlayerId owner, IReadOnlyList<PlayerModel> players)
        {
            var state = GetFlight(owner);
            if (!state.IsFlying) return;
            Land(owner, state, players);
        }

        public void Tick(float deltaTime, IReadOnlyList<PlayerModel> players)
        {
            TickPlayer(PlayerId.Player1, deltaTime, players);
            TickPlayer(PlayerId.Player2, deltaTime, players);
        }

        private void TickPlayer(PlayerId owner, float deltaTime, IReadOnlyList<PlayerModel> players)
        {
            var state = GetFlight(owner);
            if (!state.IsFlying) return;

            state.DistanceAccumulator += deltaTime * _flightSpeed;

            while (state.CurrentTileDistance < (int)state.DistanceAccumulator
                && state.CurrentTileDistance < state.Command.Spec.MaxFlightDistance)
            {
                int nextTile = state.CurrentTileDistance + 1;
                var offset = state.Command.Direction.ToOffset();
                var nextPos = state.Command.Origin + offset * nextTile;

                if (!_stage.IsInBounds(nextPos))
                {
                    SetFlight(owner, state);
                    Land(owner, state, players);
                    return;
                }

                var tileState = _stage.GetTileState(nextPos);

                if (tileState == TileState.Collapsed || tileState == TileState.PermanentlyDestroyed)
                {
                    SetFlight(owner, state);
                    Land(owner, state, players);
                    return;
                }

                if (tileState == TileState.Wall)
                {
                    state.CurrentTileDistance = nextTile;
                    SetFlight(owner, state);
                    Land(owner, state, players);
                    return;
                }

                if (CheckEntityAt(nextPos, players))
                {
                    state.CurrentTileDistance = nextTile;
                    SetFlight(owner, state);
                    Land(owner, state, players);
                    return;
                }

                state.CurrentTileDistance = nextTile;
            }

            if (state.CurrentTileDistance >= state.Command.Spec.MaxFlightDistance)
            {
                SetFlight(owner, state);
                Land(owner, state, players);
                return;
            }

            SetFlight(owner, state);
        }

        private void Land(PlayerId owner, BombFlightState state, IReadOnlyList<PlayerModel> players)
        {
            SetFlight(owner, default); // IsFlying = false

            var offset = state.Command.Direction.ToOffset();
            var landingPos = state.CurrentTileDistance > 0
                ? state.Command.Origin + offset * state.CurrentTileDistance
                : state.Command.Origin;

            GetCooldown(owner).StartCooldown(
                state.Command.Spec.Type, state.Command.Spec.Cooldown);

            _launchUseCase.ExecuteLanding(state.Command, landingPos, players, null);
        }

        private bool CheckEntityAt(GridPos pos, IReadOnlyList<PlayerModel> players)
        {
            foreach (var player in players)
            {
                if (player.CurrentPosition.Equals(pos))
                    return true;
            }
            return _slimeRegistry != null && _slimeRegistry.IsOccupied(pos);
        }

        private ref BombFlightState GetFlight(PlayerId player)
        {
            if (player == PlayerId.Player1) return ref _p1Flight;
            return ref _p2Flight;
        }

        private void SetFlight(PlayerId player, BombFlightState state)
        {
            if (player == PlayerId.Player1) _p1Flight = state;
            else _p2Flight = state;
        }

        private BombCooldownState GetCooldown(PlayerId player)
        {
            return player == PlayerId.Player1 ? _p1Cooldown : _p2Cooldown;
        }
    }
}
