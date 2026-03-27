using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Shared.Application.Interfaces;
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
        private readonly IBalanceParameters _balance;
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
            IBalanceParameters balance,
            PlayerMoveService moveService,
            BombFlightTracker bombFlightTracker,
            BombLaunchUseCase bombLaunchUseCase,
            MatchClock clock,
            IReadOnlyList<PlayerModel> players,
            StageModel stage)
        {
            _balance = balance;
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
            adapter.OnDashTriggered += HandleDash;
        }

        /// <summary>
        /// 毎フレーム呼び出し。ホールド中の方向キーリピート移動を処理する。
        /// 全ての移動はここで実行される（イベントハンドラからは移動しない）。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_clock.CurrentPhaseValue != GamePhase.MatchRunning) return;

            // ダッシュクールダウン減算
            foreach (var key in new List<int>(_dashCooldowns.Keys))
            {
                float val = _dashCooldowns[key] - deltaTime;
                if (val <= 0f)
                    _dashCooldowns.Remove(key);
                else
                    _dashCooldowns[key] = val;
            }

            foreach (var kvp in _adapters)
            {
                int idx = kvp.Key;
                var adapter = kvp.Value;
                var state = _moveStates[idx];

                // グローバル移動クールダウンを常に減算（ホールド状態に依存しない）
                if (state.CooldownRemaining > 0f)
                    state.CooldownRemaining -= deltaTime;

                if (!state.IsHolding) continue;

                var player = GetPlayer(adapter.Owner);
                if (player == null) continue;
                if (player.ForcedMove.IsForced)
                {
                    state.Reset();
                    continue;
                }

                // アダプターの現在のホールド方向を常に反映
                var currentDir = adapter.HeldDirection;
                if (!currentDir.HasValue)
                {
                    state.Reset();
                    continue;
                }
                state.Direction = currentDir.Value;

                // AimLock 中: 向きだけ更新して移動しない
                if (adapter.IsAimLocked)
                {
                    player.CurrentFacing = state.Direction;
                    state.Timer = 0f;
                    state.FirstMoveDone = false;
                    state.RepeatCount = 0;
                    continue;
                }

                state.Timer += deltaTime;

                float moveInterval = GetMoveInterval(player);

                // 初回移動: バッファ時間経過後に実行（同時押しの中間状態を吸収）
                if (!state.FirstMoveDone)
                {
                    if (state.Timer >= _balance.InputBufferTime && state.CooldownRemaining <= 0f)
                    {
                        _moveService.TryMove(player, state.Direction, _stage);
                        state.CooldownRemaining = moveInterval;
                        state.FirstMoveDone = true;
                        state.Timer = 0f;
                        state.RepeatCount = 0;
                    }
                    continue;
                }

                // リピート移動
                float threshold = state.RepeatCount == 0
                    ? moveInterval + _balance.InputInitialRepeatDelay
                    : moveInterval;

                if (state.Timer >= threshold)
                {
                    state.Timer -= threshold;
                    state.RepeatCount++;
                    _moveService.TryMove(player, state.Direction, _stage);
                    state.CooldownRemaining = moveInterval;
                }
            }
        }

        private void HandleMovePressed(PlayerId playerId, Direction8 direction)
        {
            if (_clock.CurrentPhaseValue != GamePhase.MatchRunning) return;

            int idx = playerId.Index;
            var state = _moveStates[idx];

            if (!state.IsHolding)
            {
                // 新規押下: ホールド開始、Tick でバッファ後に移動
                state.IsHolding = true;
                state.Direction = direction;
                state.Timer = 0f;
                state.RepeatCount = 0;
                state.FirstMoveDone = false;
            }
            // 既にホールド中の方向変更はアダプターの HeldDirection を
            // Tick で読み取るため、ここでは何もしない
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
                var spec = cmd.Type == BombType.Break
                    ? _bombLaunchUseCase.CreateBreakBombSpec(player.Build)
                    : _bombLaunchUseCase.CreateFireBombSpec(player.Build);

                if (player.Build.HasDualShot)
                {
                    // 双射の書: 左右に同時発射
                    var leftDir = direction.RotateCCW90();
                    var rightDir = direction.RotateCW90();
                    _bombFlightTracker.StartFlight(cmd.Owner, player.CurrentPosition, leftDir, spec);
                    _bombFlightTracker.StartDualFlight(cmd.Owner, player.CurrentPosition, rightDir, spec);
                }
                else
                {
                    _bombFlightTracker.StartFlight(cmd.Owner, player.CurrentPosition, direction, spec);
                }
            }
            else
            {
                _bombFlightTracker.ReleaseBomb(cmd.Owner, _players);
            }
        }

        // ダッシュクールダウン (プレイヤーごと)
        private readonly Dictionary<int, float> _dashCooldowns = new();

        private void HandleDash(PlayerId playerId, Direction8 direction)
        {
            if (_clock.CurrentPhaseValue != GamePhase.MatchRunning) return;

            var player = GetPlayer(playerId);
            if (player == null) return;
            if (!player.Build.HasDash) return;

            // クールダウンチェック
            _dashCooldowns.TryGetValue(playerId.Index, out float remaining);
            if (remaining > 0f) return;

            if (_moveService.TryDash(player, direction, _stage))
            {
                // ダッシュ成功 → クールダウン開始（Tick で減算される）
                _dashCooldowns[playerId.Index] = _balance.DashCooldown;
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

        private float GetMoveInterval(PlayerModel player)
        {
            float speed = player.Stats.MoveSpeed;
            if (speed <= 0f) speed = 0.1f;
            return _balance.InputBaseMoveInterval / speed;
        }

        public void Dispose()
        {
            foreach (var adapter in _adapters.Values)
            {
                adapter.OnMoveInput -= HandleMovePressed;
                adapter.OnMoveReleased -= HandleMoveReleased;
                adapter.OnBombHoldInput -= HandleBombHold;
                adapter.OnDashTriggered -= HandleDash;
            }
        }

        private sealed class MoveRepeatState
        {
            public bool IsHolding;
            public Direction8 Direction;
            public float Timer;
            public int RepeatCount;
            public bool FirstMoveDone;

            /// <summary>前回移動からの残りクールダウン。ボタン離しでもリセットしない。</summary>
            public float CooldownRemaining;

            public void Reset()
            {
                IsHolding = false;
                Timer = 0f;
                RepeatCount = 0;
                FirstMoveDone = false;
                // CooldownRemaining はリセットしない（連打防止）
            }
        }
    }
}
