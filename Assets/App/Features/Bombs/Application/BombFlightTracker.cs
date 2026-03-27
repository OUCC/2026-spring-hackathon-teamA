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
        private readonly BombCooldownState _p1Cooldown;
        private readonly BombCooldownState _p2Cooldown;
        private readonly StageModel _stage;
        private readonly SlimeRegistry _slimeRegistry;
        private readonly float _flightSpeed;

        private readonly Subject<BombFlightStartedEvent> _flightStarted = new();
        private readonly Subject<BombLandedEvent> _bombLanded = new();

        public Observable<BombFlightStartedEvent> FlightStarted => _flightStarted;
        public Observable<BombLandedEvent> BombLanded => _bombLanded;

        private BombFlightState _p1Flight;
        private BombFlightState _p2Flight;

        // DualShot 用の2発目スロット
        private BombFlightState _p1DualFlight;
        private BombFlightState _p2DualFlight;

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

        /// <summary>DualShot の2発目を開始する。クールダウン / IsFlying チェックなし。</summary>
        public bool StartDualFlight(PlayerId owner, GridPos origin, Direction8 direction, BombSpec spec)
        {
            var cmd = new BombFlightCommand(origin, direction, spec, owner);
            SetDualFlight(owner, new BombFlightState
            {
                IsFlying = true,
                Command = cmd,
                DistanceAccumulator = 0f,
                CurrentTileDistance = 0,
            });
            _flightStarted.OnNext(new BombFlightStartedEvent(owner, origin, direction, spec));
            return true;
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
            _flightStarted.OnNext(new BombFlightStartedEvent(owner, origin, direction, spec));
            return true;
        }

        public void ReleaseBomb(PlayerId owner, IReadOnlyList<PlayerModel> players)
        {
            var state = GetFlight(owner);
            if (state.IsFlying)
            {
                if (state.CurrentTileDistance >= state.Command.Spec.MinFlightDistance)
                {
                    Land(owner, state, players);
                }
                else
                {
                    state.IsReleased = true;
                    SetFlight(owner, state);
                }
            }

            // DualShot 2発目もリリース
            var dual = GetDualFlight(owner);
            if (dual.IsFlying)
            {
                if (dual.CurrentTileDistance >= dual.Command.Spec.MinFlightDistance)
                {
                    LandDual(owner, dual, players);
                }
                else
                {
                    dual.IsReleased = true;
                    SetDualFlight(owner, dual);
                }
            }
        }

        public void Tick(float deltaTime, IReadOnlyList<PlayerModel> players)
        {
            TickPlayer(PlayerId.Player1, deltaTime, players);
            TickPlayer(PlayerId.Player2, deltaTime, players);
            // DualShot 2発目
            TickDualPlayer(PlayerId.Player1, deltaTime, players);
            TickDualPlayer(PlayerId.Player2, deltaTime, players);
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

                // 穴（Collapsed, PermanentlyDestroyed）はボムが飛び越える
                if (tileState == TileState.Collapsed || tileState == TileState.PermanentlyDestroyed)
                {
                    state.CurrentTileDistance = nextTile;
                    continue;
                }

                if (tileState == TileState.Wall)
                {
                    if (!state.Command.Spec.FlightPenetration)
                    {
                        state.CurrentTileDistance = nextTile;
                        SetFlight(owner, state);
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
                        SetFlight(owner, state);
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
                    SetFlight(owner, state);
                    Land(owner, state, players);
                    return;
                }
            }

            // リリース済みかつ最小飛行距離に到達 → 着弾
            if (state.IsReleased
                && state.CurrentTileDistance >= state.Command.Spec.MinFlightDistance)
            {
                SetFlight(owner, state);
                Land(owner, state, players);
                return;
            }

            // 最大飛行距離に到達 → 着弾
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

        private ref BombFlightState GetDualFlight(PlayerId player)
        {
            if (player == PlayerId.Player1) return ref _p1DualFlight;
            return ref _p2DualFlight;
        }

        private void SetDualFlight(PlayerId player, BombFlightState state)
        {
            if (player == PlayerId.Player1) _p1DualFlight = state;
            else _p2DualFlight = state;
        }

        /// <summary>DualShot 2発目用 Tick。メインの TickPlayer と同じロジック。</summary>
        private void TickDualPlayer(PlayerId owner, float deltaTime, IReadOnlyList<PlayerModel> players)
        {
            var state = GetDualFlight(owner);
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
                    SetDualFlight(owner, state);
                    LandDual(owner, state, players);
                    return;
                }

                var tileState = _stage.GetTileState(nextPos);

                // 穴（Collapsed, PermanentlyDestroyed）はボムが飛び越える
                if (tileState == TileState.Collapsed || tileState == TileState.PermanentlyDestroyed)
                {
                    state.CurrentTileDistance = nextTile;
                    continue;
                }

                if (tileState == TileState.Wall && !state.Command.Spec.FlightPenetration)
                {
                    state.CurrentTileDistance = nextTile;
                    SetDualFlight(owner, state);
                    LandDual(owner, state, players);
                    return;
                }

                if (CheckEntityAt(nextPos, players) && !state.Command.Spec.FlightPenetration)
                {
                    state.CurrentTileDistance = nextTile;
                    SetDualFlight(owner, state);
                    LandDual(owner, state, players);
                    return;
                }

                state.CurrentTileDistance = nextTile;

                if (state.IsReleased
                    && state.CurrentTileDistance >= state.Command.Spec.MinFlightDistance)
                {
                    SetDualFlight(owner, state);
                    LandDual(owner, state, players);
                    return;
                }
            }

            if (state.IsReleased
                && state.CurrentTileDistance >= state.Command.Spec.MinFlightDistance)
            {
                SetDualFlight(owner, state);
                LandDual(owner, state, players);
                return;
            }

            if (state.CurrentTileDistance >= state.Command.Spec.MaxFlightDistance)
            {
                SetDualFlight(owner, state);
                LandDual(owner, state, players);
                return;
            }

            SetDualFlight(owner, state);
        }

        private void LandDual(PlayerId owner, BombFlightState state, IReadOnlyList<PlayerModel> players)
        {
            SetDualFlight(owner, default);

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

        private BombCooldownState GetCooldown(PlayerId player)
        {
            return player == PlayerId.Player1 ? _p1Cooldown : _p2Cooldown;
        }

        public void Dispose()
        {
            _flightStarted.Dispose();
            _bombLanded.Dispose();
        }
    }
}
