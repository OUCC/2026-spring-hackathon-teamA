using System;
using R3;
using UnityEngine.InputSystem;
using FloorBreaker.Shared.Domain.Timing;

namespace FloorBreaker.Input.Infrastructure
{
    /// <summary>
    /// GamePhase に応じてアクションマップを有効/無効化する。
    /// </summary>
    public sealed class InputMapSwitcher : IDisposable
    {
        private readonly InputActionAsset _actions;
        private readonly IDisposable _subscription;

        public InputMapSwitcher(InputActionAsset actions, MatchClock clock)
        {
            _actions = actions;

            _subscription = clock.CurrentPhase.Subscribe(phase => SwitchMaps(phase));
            SwitchMaps(clock.CurrentPhaseValue);
        }

        private void SwitchMaps(GamePhase phase)
        {
            var gameplayP1 = _actions.FindActionMap("Gameplay_P1");
            var gameplayP2 = _actions.FindActionMap("Gameplay_P2");
            var upgradeP1 = _actions.FindActionMap("UpgradeUI_P1");
            var upgradeP2 = _actions.FindActionMap("UpgradeUI_P2");
            var system = _actions.FindActionMap("System");

            switch (phase)
            {
                case GamePhase.MatchRunning:
                    gameplayP1?.Enable();
                    gameplayP2?.Enable();
                    upgradeP1?.Disable();
                    upgradeP2?.Disable();
                    system?.Enable();
                    break;
                case GamePhase.UpgradePhase:
                    gameplayP1?.Disable();
                    gameplayP2?.Disable();
                    upgradeP1?.Enable();
                    upgradeP2?.Enable();
                    system?.Enable();
                    break;
                case GamePhase.Result:
                    gameplayP1?.Disable();
                    gameplayP2?.Disable();
                    upgradeP1?.Disable();
                    upgradeP2?.Disable();
                    system?.Enable();
                    break;
                default:
                    gameplayP1?.Disable();
                    gameplayP2?.Disable();
                    upgradeP1?.Disable();
                    upgradeP2?.Disable();
                    system?.Disable();
                    break;
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
