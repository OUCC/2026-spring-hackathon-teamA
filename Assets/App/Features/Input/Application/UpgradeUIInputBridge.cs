using System;
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
    public sealed class UpgradeUIInputBridge
    {
        /// <summary>カード行のインデックス上限。0,1,2=カード、3=リロール。</summary>
        private const int RerollIndex = 3;

        private readonly UpgradeDraftService _draftP1;
        private readonly UpgradeDraftService _draftP2;
        private readonly PlayerModel _player1;
        private readonly PlayerModel _player2;
        private readonly MatchClock _clock;
        private readonly IRandomProvider _random;
        private readonly UpgradeSelectionState _selectionState;


        public UpgradeUIInputBridge(
            UpgradeDraftService draftP1,
            UpgradeDraftService draftP2,
            PlayerModel player1,
            PlayerModel player2,
            MatchClock clock,
            IRandomProvider random,
            UpgradeSelectionState selectionState)
        {
            _draftP1 = draftP1;
            _draftP2 = draftP2;
            _player1 = player1;
            _player2 = player2;
            _clock = clock;
            _random = random;
            _selectionState = selectionState;
        }

        // --- P1 ---

        public void OnNavigateP1(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase()) return;
            Navigate(PlayerId.Player1, ctx.ReadValue<UnityEngine.Vector2>());
        }

        public void OnSubmitP1(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;
            Submit(PlayerId.Player1, _draftP1, _player1);
        }

        public void OnSkipP1(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;
            _draftP1.Skip();
        }

        public void OnRerollP1(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;
            _draftP1.Reroll(_player1, _random);
        }

        // --- P2 ---

        public void OnNavigateP2(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase()) return;
            Navigate(PlayerId.Player2, ctx.ReadValue<UnityEngine.Vector2>());
        }

        public void OnSubmitP2(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;
            Submit(PlayerId.Player2, _draftP2, _player2);
        }

        public void OnSkipP2(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;
            _draftP2.Skip();
        }

        public void OnRerollP2(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;
            _draftP2.Reroll(_player2, _random);
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
                    // リロール — 購入済みインデックスを先にクリア
                    // (Reroll が新カードをセット → R3 が PopulateCards を発火するため、
                    //  先にクリアしないと古い購入済み状態で描画されてしまう)
                    _selectionState.ClearPurchased(player);
                    draft.Reroll(playerModel, _random);
                }
                else
                {
                    // カード購入
                    if (_selectionState.IsPurchased(player, index)) return;
                    if (draft.SelectChoice(index, playerModel))
                    {
                        _selectionState.MarkPurchased(player, index);
                    }
                }
            }
            else
            {
                // 完了行
                draft.Skip();
            }
        }

        private bool IsUpgradePhase()
            => _clock.CurrentPhaseValue == GamePhase.UpgradePhase;

        public void ResetSelection()
        {
            _selectionState.Reset();
        }
    }
}
