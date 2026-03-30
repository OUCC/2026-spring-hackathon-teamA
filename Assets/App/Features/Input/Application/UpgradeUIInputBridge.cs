using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Upgrades.Application;

namespace FloorBreaker.Input.Application
{
    /// <summary>
    /// UpgradeUI アクションマップからの入力を UpgradeDraftService にディスパッチする。
    /// Row 0 = カード行 (左右でカード選択、Submit で購入)
    /// Row 1 = アクション行 (左右でリロール/スキップ切替、Submit で実行)
    /// </summary>
    public sealed class UpgradeUIInputBridge : IDisposable
    {
        /// <summary>カード行のインデックス上限。0,1,2=カード、3=リロール。</summary>
        private const int RerollIndex = 3;

        private readonly IReadOnlyList<UpgradeDraftService> _drafts;
        private readonly IReadOnlyList<PlayerModel> _players;
        private readonly MatchClock _clock;
        private readonly IRandomProvider _random;
        private readonly UpgradeSelectionState _selectionState;
        private bool _disposed;

        public UpgradeUIInputBridge(
            IReadOnlyList<UpgradeDraftService> drafts,
            IReadOnlyList<PlayerModel> players,
            MatchClock clock,
            IRandomProvider random,
            UpgradeSelectionState selectionState)
        {
            _drafts = drafts;
            _players = players;
            _clock = clock;
            _random = random;
            _selectionState = selectionState;
        }

        // --- 汎用コールバック（InputInitializer から PlayerId をキャプチャして呼び出す） ---

        public void OnNavigate(PlayerId player, InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase()) return;
            Navigate(player, ctx.ReadValue<UnityEngine.Vector2>());
        }

        public void OnSubmit(PlayerId player, InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;
            Submit(player, _drafts[player.Index], _players[player.Index]);
        }

        public void OnCancel(PlayerId player, InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;

            // Cancel は最後の購入を取り消す（Undo）のみ。
            // 購入履歴がなければ何もしない。Skip は Done ボタンで行う。
            var draft = _drafts[player.Index];
            if (draft.CanUndo)
            {
                var record = draft.UndoLastPurchase(_players[player.Index]);
                if (record.HasValue)
                    _selectionState.UnmarkPurchased(player, record.Value.CardIndex);
            }
        }

        // --- 共通 ---

        private void Navigate(PlayerId player, UnityEngine.Vector2 v)
        {
            int row = _selectionState.GetRow(player);

            // 上下: カード行 (row=0) ↔ 完了行 (row=1)
            if (v.y < -0.5f && row == 0)
            {
                _selectionState.SetRow(player, 1);
                return;
            }
            if (v.y > 0.5f && row == 1)
            {
                _selectionState.SetRow(player, 0);
                return;
            }

            // 左右 (カード行のみ: 0,1,2=カード、3=リロール)
            if (row == 0)
            {
                int current = _selectionState.GetIndex(player);
                if (v.x > 0.5f) _selectionState.SetIndex(player, Math.Min(RerollIndex, current + 1));
                else if (v.x < -0.5f) _selectionState.SetIndex(player, Math.Max(0, current - 1));
            }
        }

        private void Submit(PlayerId player, UpgradeDraftService draft, PlayerModel playerModel)
        {
            int row = _selectionState.GetRow(player);

            if (row == 0)
            {
                int index = _selectionState.GetIndex(player);

                if (index == RerollIndex)
                {
                    _selectionState.ClearPurchased(player);
                    draft.Reroll(playerModel, _random);
                    _selectionState.SetIndex(player, 0); // リロール後はカード0に戻す
                }
                else
                {
                    if (_selectionState.IsPurchased(player, index)) return;
                    if (draft.SelectChoice(index, playerModel))
                    {
                        _selectionState.MarkPurchased(player, index);
                    }
                }
            }
            else
            {
                draft.Skip();
            }
        }

        private bool IsUpgradePhase()
            => !_disposed && _clock.CurrentPhaseValue == GamePhase.UpgradePhase;

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
