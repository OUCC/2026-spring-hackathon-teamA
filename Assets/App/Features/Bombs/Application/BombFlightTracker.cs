using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Bombs.Application
{
    public readonly struct BombFlightStartedEvent
    {
        public readonly PlayerId Owner;
        public readonly GridPos Origin;
        public readonly Direction8 Direction;
        public readonly BombSpec Spec;

        public BombFlightStartedEvent(PlayerId owner, GridPos origin, Direction8 direction, BombSpec spec)
        {
            Owner = owner;
            Origin = origin;
            Direction = direction;
            Spec = spec;
        }
    }

    public readonly struct BombLandedEvent
    {
        public readonly PlayerId Owner;
        public readonly GridPos LandingPos;
        public readonly BombType Type;
        public readonly int EffectRange;
        public readonly bool WallPenetration;

        public BombLandedEvent(PlayerId owner, GridPos landingPos, BombType type, int effectRange, bool wallPenetration)
        {
            Owner = owner;
            LandingPos = landingPos;
            Type = type;
            EffectRange = effectRange;
            WallPenetration = wallPenetration;
        }
    }

    internal struct BombFlightState
    {
        public bool IsFlying;
        public bool IsReleased;
        public BombFlightCommand Command;
        public float DistanceAccumulator;
        public int CurrentTileDistance;
    }

    public sealed class BombFlightTracker : IDisposable
    {
        private readonly BombLaunchUseCase _launchUseCase;
        private readonly IReadOnlyList<BombCooldownState> _cooldowns;
        private readonly StageModel _stage;
        private readonly SlimeRegistry _slimeRegistry;
        private readonly float _flightSpeed;

        private readonly Subject<BombFlightStartedEvent> _flightStarted = new();
        private readonly Subject<BombLandedEvent> _bombLanded = new();

        public Observable<BombFlightStartedEvent> FlightStarted => _flightStarted;
        public Observable<BombLandedEvent> BombLanded => _bombLanded;

        private readonly BombFlightState[] _flights;
        private readonly BombFlightState[] _dualFlights;

        public BombFlightTracker(
            BombLaunchUseCase launchUseCase,
            IReadOnlyList<BombCooldownState> cooldowns,
            StageModel stage,
            SlimeRegistry slimeRegistry,
            IBalanceParameters balance)
        {
            _launchUseCase = launchUseCase;
            _cooldowns = cooldowns;
            _stage = stage;
            _slimeRegistry = slimeRegistry;
            _flightSpeed = balance.BombFlightSpeed;

            int count = cooldowns.Count;
            _flights = new BombFlightState[count];
            _dualFlights = new BombFlightState[count];
        }

        public bool IsFlying(PlayerId player)
        {
            return _flights[player.Index].IsFlying;
        }

        /// <summary>DualShot の2発目を開始する。クールダウン / IsFlying チェックなし。</summary>
        public bool StartDualFlight(PlayerId owner, GridPos origin, Direction8 direction, BombSpec spec)
        {
            var cmd = new BombFlightCommand(origin, direction, spec, owner);
            _dualFlights[owner.Index] = new BombFlightState
            {
                IsFlying = true,
                Command = cmd,
                DistanceAccumulator = 0f,
                CurrentTileDistance = 0,
            };
            _flightStarted.OnNext(new BombFlightStartedEvent(owner, origin, direction, spec));
            return true;
        }

        public bool StartFlight(PlayerId owner, GridPos origin, Direction8 direction, BombSpec spec)
        {
            if (IsFlying(owner)) return false;
            if (!_cooldowns[owner.Index].CanFire(spec.Type)) return false;

            var cmd = new BombFlightCommand(origin, direction, spec, owner);
            _flights[owner.Index] = new BombFlightState
            {
                IsFlying = true,
                Command = cmd,
                DistanceAccumulator = 0f,
                CurrentTileDistance = 0,
            };
            _flightStarted.OnNext(new BombFlightStartedEvent(owner, origin, direction, spec));
            return true;
        }

        public void ReleaseBomb(PlayerId owner, IReadOnlyList<PlayerModel> players)
        {
            var state = _flights[owner.Index];
            if (state.IsFlying)
            {
                if (state.CurrentTileDistance >= state.Command.Spec.MinFlightDistance)
                {
                    Land(owner, state, players);
                }
                else
                {
                    state.IsReleased = true;
                    _flights[owner.Index] = state;
                }
            }

            // DualShot 2発目もリリース
            var dual = _dualFlights[owner.Index];
            if (dual.IsFlying)
            {
                if (dual.CurrentTileDistance >= dual.Command.Spec.MinFlightDistance)
                {
                    LandDual(owner, dual, players);
                }
                else
                {
                    dual.IsReleased = true;
                    _dualFlights[owner.Index] = dual;
                }
            }
        }

        public void Tick(float deltaTime, IReadOnlyList<PlayerModel> players)
        {
            for (int i = 0; i < _flights.Length; i++)
            {
                var id = PlayerId.FromIndex(i);
                TickPlayer(id, deltaTime, players);
                TickDualPlayer(id, deltaTime, players);
            }
        }

        private void TickPlayer(PlayerId owner, float deltaTime, IReadOnlyList<PlayerModel> players)
        {
            var state = _flights[owner.Index];
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
                    _flights[owner.Index] = state;
                    Land(owner, state, players);
                    return;
                }

                var tileData = _stage.GetTileData(nextPos);

                // 穴（Collapsed, PermanentlyDestroyed）はボムが飛び越える
                if (TileData.IsHoleCondition(tileData.Condition))
                {
                    state.CurrentTileDistance = nextTile;
                    continue;
                }

                if (TileData.IsImpassableType(tileData.Type))
                {
                    if (!state.Command.Spec.FlightPenetration)
                    {
                        state.CurrentTileDistance = nextTile;
                        _flights[owner.Index] = state;
                        Land(owner, state, players);
                        return;
                    }
                    // 貫通: 壁を無視して飛行続行
                }

                if (CheckEntityAt(nextPos, players))
                {
                    if (!state.Command.Spec.FlightPenetration)
                    {
                        state.CurrentTileDistance = nextTile;
                        _flights[owner.Index] = state;
                        Land(owner, state, players);
                        return;
                    }
                    // 貫通: エンティティを無視して飛行続行
                }

                state.CurrentTileDistance = nextTile;

                // リリース済みかつ最小飛行距離に到達 → ループ内で即着弾
                if (state.IsReleased
                    && state.CurrentTileDistance >= state.Command.Spec.MinFlightDistance)
                {
                    _flights[owner.Index] = state;
                    Land(owner, state, players);
                    return;
                }
            }

            // リリース済みかつ最小飛行距離に到達 → 着弾
            if (state.IsReleased
                && state.CurrentTileDistance >= state.Command.Spec.MinFlightDistance)
            {
                _flights[owner.Index] = state;
                Land(owner, state, players);
                return;
            }

            // 最大飛行距離に到達 → 着弾
            if (state.CurrentTileDistance >= state.Command.Spec.MaxFlightDistance)
            {
                _flights[owner.Index] = state;
                Land(owner, state, players);
                return;
            }

            _flights[owner.Index] = state;
        }

        private void Land(PlayerId owner, BombFlightState state, IReadOnlyList<PlayerModel> players)
        {
            _flights[owner.Index] = default; // IsFlying = false

            var offset = state.Command.Direction.ToOffset();
            var landingPos = state.CurrentTileDistance > 0
                ? state.Command.Origin + offset * state.CurrentTileDistance
                : state.Command.Origin;

            _cooldowns[owner.Index].StartCooldown(
                state.Command.Spec.Type, state.Command.Spec.Cooldown);

            _launchUseCase.ExecuteLanding(state.Command, landingPos, players, null);

            _bombLanded.OnNext(new BombLandedEvent(
                owner, landingPos, state.Command.Spec.Type,
                state.Command.Spec.EffectRange, state.Command.Spec.WallPenetration));
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

        /// <summary>DualShot 2発目用 Tick。メインの TickPlayer と同じロジック。</summary>
        private void TickDualPlayer(PlayerId owner, float deltaTime, IReadOnlyList<PlayerModel> players)
        {
            var state = _dualFlights[owner.Index];
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
                    _dualFlights[owner.Index] = state;
                    LandDual(owner, state, players);
                    return;
                }

                var tileData = _stage.GetTileData(nextPos);

                // 穴（Collapsed, PermanentlyDestroyed）はボムが飛び越える
                if (TileData.IsHoleCondition(tileData.Condition))
                {
                    state.CurrentTileDistance = nextTile;
                    continue;
                }

                if (TileData.IsImpassableType(tileData.Type) && !state.Command.Spec.FlightPenetration)
                {
                    state.CurrentTileDistance = nextTile;
                    _dualFlights[owner.Index] = state;
                    LandDual(owner, state, players);
                    return;
                }

                if (CheckEntityAt(nextPos, players) && !state.Command.Spec.FlightPenetration)
                {
                    state.CurrentTileDistance = nextTile;
                    _dualFlights[owner.Index] = state;
                    LandDual(owner, state, players);
                    return;
                }

                state.CurrentTileDistance = nextTile;

                if (state.IsReleased
                    && state.CurrentTileDistance >= state.Command.Spec.MinFlightDistance)
                {
                    _dualFlights[owner.Index] = state;
                    LandDual(owner, state, players);
                    return;
                }
            }

            if (state.IsReleased
                && state.CurrentTileDistance >= state.Command.Spec.MinFlightDistance)
            {
                _dualFlights[owner.Index] = state;
                LandDual(owner, state, players);
                return;
            }

            if (state.CurrentTileDistance >= state.Command.Spec.MaxFlightDistance)
            {
                _dualFlights[owner.Index] = state;
                LandDual(owner, state, players);
                return;
            }

            _dualFlights[owner.Index] = state;
        }

        private void LandDual(PlayerId owner, BombFlightState state, IReadOnlyList<PlayerModel> players)
        {
            _dualFlights[owner.Index] = default;

            var offset = state.Command.Direction.ToOffset();
            var landingPos = state.CurrentTileDistance > 0
                ? state.Command.Origin + offset * state.CurrentTileDistance
                : state.Command.Origin;

            // DualShot 2発目はクールダウンを開始しない（1発目で既に開始済み）
            _launchUseCase.ExecuteLanding(state.Command, landingPos, players, null);

            _bombLanded.OnNext(new BombLandedEvent(
                owner, landingPos, state.Command.Spec.Type,
                state.Command.Spec.EffectRange, state.Command.Spec.WallPenetration));
        }

        public void Dispose()
        {
            _flightStarted.Dispose();
            _bombLanded.Dispose();
        }
    }
}
