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

        private int _p1SelectIndex;
        private int _p2SelectIndex;

        public UpgradeUIInputBridge(
            UpgradeDraftService draftP1,
            UpgradeDraftService draftP2,
            PlayerModel player1,
            PlayerModel player2,
            MatchClock clock,
            IRandomProvider random)
        {
            _draftP1 = draftP1;
            _draftP2 = draftP2;
            _player1 = player1;
            _player2 = player2;
            _clock = clock;
            _random = random;
        }

        public void OnNavigateP1(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase()) return;
            var v = ctx.ReadValue<UnityEngine.Vector2>();
            if (v.y > 0.5f) _p1SelectIndex = Math.Max(0, _p1SelectIndex - 1);
            else if (v.y < -0.5f) _p1SelectIndex = Math.Min(2, _p1SelectIndex + 1);
        }

        public void OnSubmitP1(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;
            _draftP1.SelectChoice(_p1SelectIndex, _player1);
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
            if (v.y > 0.5f) _p2SelectIndex = Math.Max(0, _p2SelectIndex - 1);
            else if (v.y < -0.5f) _p2SelectIndex = Math.Min(2, _p2SelectIndex + 1);
        }

        public void OnSubmitP2(InputAction.CallbackContext ctx)
        {
            if (!IsUpgradePhase() || !ctx.performed) return;
            _draftP2.SelectChoice(_p2SelectIndex, _player2);
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
            _p1SelectIndex = 0;
            _p2SelectIndex = 0;
        }
    }
}
