using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Input.Infrastructure;

namespace FloorBreaker.Input.Application
{
    /// <summary>
    /// PlayerInputAdapter からの入力を Application 層のサービスにディスパッチする。
    /// フェーズや状態に応じて入力をブロックする。
    /// </summary>
    public sealed class GameplayInputBridge : IDisposable
    {
        private readonly PlayerMoveService _moveService;
        private readonly BombFlightTracker _bombFlightTracker;
        private readonly BombLaunchUseCase _bombLaunchUseCase;
        private readonly MatchClock _clock;
        private readonly IReadOnlyList<PlayerModel> _players;
        private readonly StageModel _stage;
        private readonly Dictionary<int, PlayerInputAdapter> _adapters = new();

        public GameplayInputBridge(
            PlayerMoveService moveService,
            BombFlightTracker bombFlightTracker,
            BombLaunchUseCase bombLaunchUseCase,
            MatchClock clock,
            IReadOnlyList<PlayerModel> players,
            StageModel stage)
        {
            _moveService = moveService;
            _bombFlightTracker = bombFlightTracker;
            _bombLaunchUseCase = bombLaunchUseCase;
            _clock = clock;
            _players = players;
            _stage = stage;
        }

        public void RegisterAdapter(PlayerInputAdapter adapter)
        {
            _adapters[adapter.Owner.Index] = adapter;
            adapter.OnMoveInput += HandleMove;
            adapter.OnBombHoldInput += HandleBombHold;
        }

        private void HandleMove(PlayerId playerId, Direction8 direction)
        {
            if (_clock.CurrentPhaseValue != GamePhase.MatchRunning) return;

            var player = GetPlayer(playerId);
            if (player == null) return;
            if (player.ForcedMove.IsForced) return;

            _moveService.TryMove(player, direction, _stage);
        }

        private void HandleBombHold(BombHoldCommand cmd)
        {
            if (_clock.CurrentPhaseValue != GamePhase.MatchRunning) return;

            var player = GetPlayer(cmd.Owner);
            if (player == null) return;

            if (cmd.IsPressed)
            {
                // ボム発射開始
                var adapter = _adapters.GetValueOrDefault(cmd.Owner.Index);
                var direction = adapter?.LastDirection ?? player.CurrentFacing;
                var spec = cmd.Type == BombType.Fall
                    ? _bombLaunchUseCase.CreateFallBombSpec(player.Build)
                    : _bombLaunchUseCase.CreateFireBombSpec(player.Build);

                _bombFlightTracker.StartFlight(cmd.Owner, player.CurrentPosition, direction, spec);
            }
            else
            {
                // ボムリリース → 着弾
                _bombFlightTracker.ReleaseBomb(cmd.Owner, _players);
            }
        }

        private PlayerModel GetPlayer(PlayerId id)
        {
            foreach (var p in _players)
            {
                if (p.Id == id) return p;
            }
            return null;
        }

        public void Dispose()
        {
            foreach (var adapter in _adapters.Values)
            {
                adapter.OnMoveInput -= HandleMove;
                adapter.OnBombHoldInput -= HandleBombHold;
            }
        }
    }
}
