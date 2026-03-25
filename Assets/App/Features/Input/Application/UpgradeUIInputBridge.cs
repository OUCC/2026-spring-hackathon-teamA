using System;
using UnityEngine.InputSystem;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;

namespace FloorBreaker.Input.Application
{
    /// <summary>
    /// UpgradeUI アクションマップからの入力を UpgradeDraftService にディスパッチする。
    /// 強化フェーズ中のみアクティブ。
    /// </summary>
    public sealed class UpgradeUIInputBridge
    {
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

        public void OnNavigateP1(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase()) return;
            var v = ctx.ReadValue<UnityEngine.Vector2>();
            int current = _selectionState.GetIndex(PlayerId.Player1);
            if (v.x > 0.5f) _selectionState.SetIndex(PlayerId.Player1, Math.Min(2, current + 1));
            else if (v.x < -0.5f) _selectionState.SetIndex(PlayerId.Player1, Math.Max(0, current - 1));
        }

        public void OnSubmitP1(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;
            _draftP1.SelectChoice(_selectionState.GetIndex(PlayerId.Player1), _player1);
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

        public void OnNavigateP2(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase()) return;
            var v = ctx.ReadValue<UnityEngine.Vector2>();
            int current = _selectionState.GetIndex(PlayerId.Player2);
            if (v.x > 0.5f) _selectionState.SetIndex(PlayerId.Player2, Math.Min(2, current + 1));
            else if (v.x < -0.5f) _selectionState.SetIndex(PlayerId.Player2, Math.Max(0, current - 1));
        }

        public void OnSubmitP2(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;
            _draftP2.SelectChoice(_selectionState.GetIndex(PlayerId.Player2), _player2);
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

        private bool IsUpgradePhase()
        {
            return _clock.CurrentPhaseValue == GamePhase.UpgradePhase;
        }

        public void ResetSelection()
        {
            _selectionState.Reset();
        }
    }
}
