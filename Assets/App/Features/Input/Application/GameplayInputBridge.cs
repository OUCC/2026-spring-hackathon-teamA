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
    /// Tick ベースのホールドリピート移動を実装。
    /// </summary>
    public sealed class GameplayInputBridge : IDisposable
    {
        /// <summary>基本移動間隔 (秒)。実際の間隔 = BaseMoveInterval / MoveSpeed。</summary>
        private const float BaseMoveInterval = 0.2f;

        /// <summary>初回入力後の追加遅延。ホールド開始時に少し溜めてからリピート開始。</summary>
        private const float InitialRepeatDelay = 0.12f;

        private readonly PlayerMoveService _moveService;
        private readonly BombFlightTracker _bombFlightTracker;
        private readonly BombLaunchUseCase _bombLaunchUseCase;
        private readonly MatchClock _clock;
        private readonly IReadOnlyList<PlayerModel> _players;
        private readonly StageModel _stage;
        private readonly Dictionary<int, PlayerInputAdapter> _adapters = new();

        // ホールドリピート用の状態 (プレイヤーごと)
        private readonly Dictionary<int, MoveRepeatState> _moveStates = new();

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
            int idx = adapter.Owner.Index;
            _adapters[idx] = adapter;
            _moveStates[idx] = new MoveRepeatState();
            adapter.OnMoveInput += HandleMovePressed;
            adapter.OnMoveReleased += HandleMoveReleased;
            adapter.OnBombHoldInput += HandleBombHold;
        }

        /// <summary>
        /// 毎フレーム呼び出し。ホールド中の方向キーリピート移動を処理する。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_clock.CurrentPhaseValue != GamePhase.MatchRunning) return;

            foreach (var kvp in _adapters)
            {
                int idx = kvp.Key;
                var adapter = kvp.Value;
                var state = _moveStates[idx];

                if (!state.IsHolding) continue;

                var player = GetPlayer(adapter.Owner);
                if (player == null) continue;
                if (player.ForcedMove.IsForced)
                {
                    state.Reset();
                    continue;
                }

                // ホールド中: 方向が変わったら即座に移動 + タイマーリセット
                var currentDir = adapter.HeldDirection;
                if (!currentDir.HasValue)
                {
                    state.Reset();
                    continue;
                }

                if (currentDir.Value != state.Direction)
                {
                    state.Direction = currentDir.Value;
                    state.Timer = 0f;
                    state.InitialMoveDone = false;
                }

                // 初回移動は HandleMovePressed で即座に実行済み
                if (!state.InitialMoveDone) continue;

                float moveInterval = GetMoveInterval(player);
                float threshold = state.RepeatCount == 0
                    ? moveInterval + InitialRepeatDelay
                    : moveInterval;

                state.Timer += deltaTime;
                if (state.Timer >= threshold)
                {
                    state.Timer -= threshold;
                    state.RepeatCount++;
                    _moveService.TryMove(player, state.Direction, _stage);
                }
            }
        }

        private void HandleMovePressed(PlayerId playerId, Direction8 direction)
        {
            if (_clock.CurrentPhaseValue != GamePhase.MatchRunning) return;

            var player = GetPlayer(playerId);
            if (player == null) return;
            if (player.ForcedMove.IsForced) return;

            int idx = playerId.Index;
            var state = _moveStates[idx];

            // 初回押下: 即座に1マス移動
            _moveService.TryMove(player, direction, _stage);

            state.IsHolding = true;
            state.Direction = direction;
            state.Timer = 0f;
            state.RepeatCount = 0;
            state.InitialMoveDone = true;
        }

        private void HandleMoveReleased(PlayerId playerId)
        {
            int idx = playerId.Index;
            if (_moveStates.TryGetValue(idx, out var state))
            {
                state.Reset();
            }
        }

        private void HandleBombHold(BombHoldCommand cmd)
        {
            if (_clock.CurrentPhaseValue != GamePhase.MatchRunning) return;

            var player = GetPlayer(cmd.Owner);
            if (player == null) return;

            if (cmd.IsPressed)
            {
                var adapter = _adapters.GetValueOrDefault(cmd.Owner.Index);
                var direction = adapter?.LastDirection ?? player.CurrentFacing;
                var spec = cmd.Type == BombType.Fall
                    ? _bombLaunchUseCase.CreateFallBombSpec(player.Build)
                    : _bombLaunchUseCase.CreateFireBombSpec(player.Build);

                _bombFlightTracker.StartFlight(cmd.Owner, player.CurrentPosition, direction, spec);
            }
            else
            {
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

        private static float GetMoveInterval(PlayerModel player)
        {
            float speed = player.Stats.MoveSpeed;
            if (speed <= 0f) speed = 0.1f;
            return BaseMoveInterval / speed;
        }

        public void Dispose()
        {
            foreach (var adapter in _adapters.Values)
            {
                adapter.OnMoveInput -= HandleMovePressed;
                adapter.OnMoveReleased -= HandleMoveReleased;
                adapter.OnBombHoldInput -= HandleBombHold;
            }
        }

        private sealed class MoveRepeatState
        {
            public bool IsHolding;
            public Direction8 Direction;
            public float Timer;
            public int RepeatCount;
            public bool InitialMoveDone;

            public void Reset()
            {
                IsHolding = false;
                Timer = 0f;
                RepeatCount = 0;
                InitialMoveDone = false;
            }
        }
    }
}
