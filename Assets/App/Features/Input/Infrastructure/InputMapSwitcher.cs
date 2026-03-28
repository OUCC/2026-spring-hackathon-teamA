using System;
using System.Collections.Generic;
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
        private readonly int _playerCount;
        private readonly IDisposable _subscription;

        public InputMapSwitcher(InputActionAsset actions, MatchClock clock, int playerCount)
        {
            _actions = actions;
            _playerCount = playerCount;

            _subscription = clock.CurrentPhase.Subscribe(phase => SwitchMaps(phase));
            SwitchMaps(clock.CurrentPhaseValue);
        }

        private void SwitchMaps(GamePhase phase)
        {
            var system = _actions.FindActionMap("System");

            switch (phase)
            {
                case GamePhase.MatchRunning:
                    SetGameplayMaps(enabled: true);
                    SetUpgradeMaps(enabled: false);
                    system?.Enable();
                    break;
                case GamePhase.UpgradePhase:
                    SetGameplayMaps(enabled: false);
                    SetUpgradeMaps(enabled: true);
                    system?.Enable();
                    break;
                case GamePhase.Result:
                    SetGameplayMaps(enabled: false);
                    SetUpgradeMaps(enabled: false);
                    system?.Enable();
                    break;
                default:
                    SetGameplayMaps(enabled: false);
                    SetUpgradeMaps(enabled: false);
                    system?.Disable();
                    break;
            }
        }

        private void SetGameplayMaps(bool enabled)
        {
            for (int i = 1; i <= _playerCount; i++)
            {
                var map = _actions.FindActionMap($"Gameplay_P{i}");
                if (enabled) map?.Enable(); else map?.Disable();
            }
        }

        private void SetUpgradeMaps(bool enabled)
        {
            for (int i = 1; i <= _playerCount; i++)
            {
                var map = _actions.FindActionMap($"UpgradeUI_P{i}");
                if (enabled) map?.Enable(); else map?.Disable();
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
