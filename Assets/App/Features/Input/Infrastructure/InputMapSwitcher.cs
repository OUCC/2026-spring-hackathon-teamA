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
        private readonly MatchClock _clock;
        private readonly int _playerCount;
        private readonly IDisposable _phaseSub;
        private readonly IDisposable _pauseSub;

        public InputMapSwitcher(InputActionAsset actions, MatchClock clock, int playerCount)
        {
            _actions = actions;
            _clock = clock;
            _playerCount = playerCount;

            _phaseSub = clock.CurrentPhase.Subscribe(phase => SwitchMaps(phase));
            _pauseSub = clock.IsPaused.Subscribe(paused =>
            {
                if (_clock.CurrentPhaseValue == GamePhase.MatchRunning)
                    SetGameplayMaps(enabled: !paused);
            });
            SwitchMaps(clock.CurrentPhaseValue);
        }

        private void SwitchMaps(GamePhase phase)
        {
            var system = _actions.FindActionMap("System");

            switch (phase)
            {
                case GamePhase.Countdown:
                    SetGameplayMaps(enabled: false);
                    SetUpgradeMaps(enabled: false);
                    system?.Enable();
                    break;
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
                    SetGameplayMaps(enabled: true);
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
            _phaseSub?.Dispose();
            _pauseSub?.Dispose();
        }
    }
}
