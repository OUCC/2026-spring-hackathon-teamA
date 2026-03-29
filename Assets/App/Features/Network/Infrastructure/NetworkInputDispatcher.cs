using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Upgrades.Application;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// ホスト側の入力ディスパッチャー。
    /// FloorBreakerInput を受け取り、対応する Domain サービスを呼び出す。
    /// GameplayInputBridge + UpgradeUIInputBridge のネットワーク版。
    /// </summary>
    public sealed class NetworkInputDispatcher
    {
        private readonly PlayerMoveService _moveService;
        private readonly BombFlightTracker _bombFlightTracker;
        private readonly BombLaunchUseCase _bombLaunchUseCase;
        private readonly StageModel _stage;
        private readonly IReadOnlyList<PlayerModel> _players;
        private readonly MatchClock _clock;
        private readonly IReadOnlyList<UpgradeDraftService> _drafts;
        private readonly UpgradeSelectionState _selectionState;
        private readonly IRandomProvider _random;
        private readonly IBalanceParameters _balance;

        // ダッシュクールダウン（プレイヤーごと）
        private readonly float[] _dashCooldowns;

        public NetworkInputDispatcher(
            PlayerMoveService moveService,
            BombFlightTracker bombFlightTracker,
            BombLaunchUseCase bombLaunchUseCase,
            StageModel stage,
            IReadOnlyList<PlayerModel> players,
            MatchClock clock,
            IReadOnlyList<UpgradeDraftService> drafts,
            UpgradeSelectionState selectionState,
            IRandomProvider random,
            IBalanceParameters balance)
        {
            _moveService = moveService;
            _bombFlightTracker = bombFlightTracker;
            _bombLaunchUseCase = bombLaunchUseCase;
            _stage = stage;
            _players = players;
            _clock = clock;
            _drafts = drafts;
            _selectionState = selectionState;
            _random = random;
            _balance = balance;
            _dashCooldowns = new float[players.Count];
        }

        /// <summary>
        /// FixedUpdateNetwork の先頭で呼ばれる。ダッシュクールダウンの減算。
        /// </summary>
        public void TickCooldowns(float deltaTime)
        {
            for (int i = 0; i < _dashCooldowns.Length; i++)
            {
                if (_dashCooldowns[i] > 0f)
                    _dashCooldowns[i] -= deltaTime;
            }
        }

        /// <summary>
        /// 指定プレイヤーの入力を Domain サービスにディスパッチする。
        /// ホストの FixedUpdateNetwork() から呼ばれる。
        /// </summary>
        public void Dispatch(int playerIndex, FloorBreakerInput input)
        {
            if (playerIndex < 0 || playerIndex >= _players.Count) return;

            var player = _players[playerIndex];
            if (player.Stats.IsDead) return;

            var phase = _clock.CurrentPhaseValue;

            if (phase == GamePhase.MatchRunning)
            {
                DispatchGameplay(playerIndex, player, input);
            }
            else if (phase == GamePhase.UpgradePhase)
            {
                DispatchUpgrade(playerIndex, player, input);
            }
        }

        private void DispatchGameplay(int playerIndex, PlayerModel player, FloorBreakerInput input)
        {
            // --- 移動 ---
            if (input.MoveHeld && !player.ForcedMove.IsForced)
            {
                _moveService.TryMove(player, input.MoveDirection, _stage);
            }

            // --- ダッシュ ---
            if (input.DashTriggered && player.Build.HasDash && !player.ForcedMove.IsForced)
            {
                if (_dashCooldowns[playerIndex] <= 0f)
                {
                    if (_moveService.TryDash(player, input.DashDirection, _stage))
                    {
                        _dashCooldowns[playerIndex] = _balance.DashCooldown;
                    }
                }
            }

            // --- ボム ---
            DispatchBomb(playerIndex, player, BombType.Break, input.BreakBombPressed, input.BreakBombReleased);
            DispatchBomb(playerIndex, player, BombType.Fire, input.FireBombPressed, input.FireBombReleased);
        }

        private void DispatchBomb(int playerIndex, PlayerModel player, BombType type,
            bool pressed, bool released)
        {
            var playerId = player.Id;

            if (pressed)
            {
                var direction = player.CurrentFacing;
                var spec = type == BombType.Break
                    ? _bombLaunchUseCase.CreateBreakBombSpec(player.Build)
                    : _bombLaunchUseCase.CreateFireBombSpec(player.Build);

                if (player.Build.HasDualShot)
                {
                    var leftDir = direction.RotateCCW90();
                    var rightDir = direction.RotateCW90();
                    _bombFlightTracker.StartFlight(playerId, player.CurrentPosition, leftDir, spec);
                    _bombFlightTracker.StartDualFlight(playerId, player.CurrentPosition, rightDir, spec);
                }
                else
                {
                    _bombFlightTracker.StartFlight(playerId, player.CurrentPosition, direction, spec);
                }
            }

            if (released)
            {
                _bombFlightTracker.ReleaseBomb(playerId, _players);
            }
        }

        // =============================================
        // 強化フェーズ入力
        // =============================================

        /// <summary>リロールボタンのインデックス（UpgradeUIInputBridge と同一）。</summary>
        private const int RerollIndex = 3;

        private void DispatchUpgrade(int playerIndex, PlayerModel player, FloorBreakerInput input)
        {
            if (input.UpgradeAction == UpgradeInputAction.None) return;
            if (playerIndex >= _drafts.Count) return;

            var draft = _drafts[playerIndex];
            var playerId = player.Id;

            switch (input.UpgradeAction)
            {
                case UpgradeInputAction.SelectCard0:
                    TryPurchaseCard(playerId, 0, draft, player);
                    break;
                case UpgradeInputAction.SelectCard1:
                    TryPurchaseCard(playerId, 1, draft, player);
                    break;
                case UpgradeInputAction.SelectCard2:
                    TryPurchaseCard(playerId, 2, draft, player);
                    break;
                case UpgradeInputAction.Reroll:
                    _selectionState.ClearPurchased(playerId);
                    draft.Reroll(player, _random);
                    break;
                case UpgradeInputAction.Skip:
                    draft.Skip();
                    break;
                case UpgradeInputAction.NavigateLeft:
                    NavigateHorizontal(playerId, -1);
                    break;
                case UpgradeInputAction.NavigateRight:
                    NavigateHorizontal(playerId, 1);
                    break;
                case UpgradeInputAction.NavigateUp:
                    NavigateVertical(playerId, -1);
                    break;
                case UpgradeInputAction.NavigateDown:
                    NavigateVertical(playerId, 1);
                    break;
            }
        }

        private void TryPurchaseCard(PlayerId playerId, int cardIndex, UpgradeDraftService draft, PlayerModel player)
        {
            if (_selectionState.IsPurchased(playerId, cardIndex)) return;
            if (draft.SelectChoice(cardIndex, player))
            {
                _selectionState.MarkPurchased(playerId, cardIndex);
            }
        }

        private void NavigateHorizontal(PlayerId playerId, int delta)
        {
            int row = _selectionState.GetRow(playerId);
            if (row != 0) return;

            int current = _selectionState.GetIndex(playerId);
            int next = current + delta;
            if (next < 0) next = 0;
            if (next > RerollIndex) next = RerollIndex;
            _selectionState.SetIndex(playerId, next);
        }

        private void NavigateVertical(PlayerId playerId, int delta)
        {
            int row = _selectionState.GetRow(playerId);
            int newRow = row + delta;
            if (newRow < 0) newRow = 0;
            if (newRow > 1) newRow = 1;
            _selectionState.SetRow(playerId, newRow);
        }
    }
}
