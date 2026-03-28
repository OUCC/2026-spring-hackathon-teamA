using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Input.Application;
using FloorBreaker.Input.Infrastructure;
using FloorBreaker.MatchFlow.Application;
using DeviceType = FloorBreaker.Shared.Application.Interfaces.DeviceType;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Input 関連の初期化を担当する。
    /// PlayerInputAdapter の検出、アクションマップ接続、InputMapSwitcher 生成を行う。
    /// </summary>
    public sealed class InputInitializer : IDisposable
    {
        private readonly GameplayInputBridge _gameplayInputBridge;
        private readonly UpgradeUIInputBridge _upgradeUIInputBridge;
        private readonly MatchClock _clock;
        private readonly MatchModeConfig _modeConfig;
        private readonly MatchPlayers _players;
        private readonly MatchPhaseScheduler _scheduler;
        private readonly ITimeProvider _timeProvider;

        private InputMapSwitcher _inputMapSwitcher;
        private readonly List<(InputActionMap map, PlayerId id)> _upgradeMaps = new();
        private readonly List<GameObject> _dynamicAdapters = new();
        private InputAction _pauseAction;

        public InputInitializer(
            GameplayInputBridge gameplayInputBridge,
            UpgradeUIInputBridge upgradeUIInputBridge,
            MatchClock clock,
            MatchModeConfig modeConfig,
            MatchPlayers players,
            MatchPhaseScheduler scheduler,
            ITimeProvider timeProvider)
        {
            _gameplayInputBridge = gameplayInputBridge;
            _upgradeUIInputBridge = upgradeUIInputBridge;
            _clock = clock;
            _modeConfig = modeConfig;
            _players = players;
            _scheduler = scheduler;
            _timeProvider = timeProvider;
        }

        public void Initialize()
        {
            // 1. シーン上の PlayerInputAdapter 検出（P1/P2 キーボード用）
            var sceneAdapters = UnityEngine.Object.FindObjectsByType<PlayerInputAdapter>(
                FindObjectsSortMode.None);

            InputActionAsset inputActions = null;
            foreach (var adapter in sceneAdapters)
            {
                if (adapter.InputActions != null)
                {
                    inputActions = adapter.InputActions;
                    break;
                }
            }

            // 2. Human プレイヤーリスト構築
            var humanIndices = new List<int>();
            for (int i = 0; i < _players.PlayerCount; i++)
                if (!_modeConfig.IsCpuAt(i)) humanIndices.Add(i);

            // 3. アダプター初期化: DeviceType に基づいてデバイスを割り当て
            int sceneAdapterIdx = 0;
            foreach (int playerIdx in humanIndices)
            {
                var id = PlayerId.FromIndex(playerIdx);
                var deviceType = _modeConfig.DeviceTypes[playerIdx];

                if (deviceType == DeviceType.KeyboardWasd || deviceType == DeviceType.KeyboardArrows)
                {
                    // キーボード: シーン上のアダプターを使用
                    if (sceneAdapterIdx < sceneAdapters.Length)
                    {
                        var adapter = sceneAdapters[sceneAdapterIdx++];
                        adapter.Initialize(id, _timeProvider, inputActions);
                        _gameplayInputBridge.RegisterAdapter(adapter);
                    }
                }
                else if (deviceType == DeviceType.Gamepad && inputActions != null)
                {
                    // ゲームパッド: 動的アダプター生成 + デバイス制限
                    int gpIdx = _modeConfig.GamepadIndices[playerIdx];
                    var gamepads = Gamepad.all;
                    if (gpIdx >= 0 && gpIdx < gamepads.Count)
                    {
                        var go = new GameObject($"GamepadAdapter_P{playerIdx + 1}");
                        var adapter = go.AddComponent<PlayerInputAdapter>();
                        adapter.Initialize(id, _timeProvider, inputActions);
                        adapter.RestrictToDevice(gamepads[gpIdx]);
                        _gameplayInputBridge.RegisterAdapter(adapter);
                        _dynamicAdapters.Add(go);
                    }
                }
            }

            // 4. InputMapSwitcher 生成
            if (inputActions != null)
            {
                _inputMapSwitcher = new InputMapSwitcher(inputActions, _clock, _players.PlayerCount);

                // 4b. System.Pause アクション接続
                var systemMap = inputActions.FindActionMap("System");
                if (systemMap != null)
                {
                    _pauseAction = systemMap.FindAction("Pause");
                    if (_pauseAction != null)
                        _pauseAction.performed += OnPausePerformed;
                }

                // 5. UpgradeUI アクションマップとの接続（Human 分）
                foreach (int playerIdx in humanIndices)
                {
                    var id = PlayerId.FromIndex(playerIdx);
                    var map = inputActions.FindActionMap($"UpgradeUI_P{playerIdx + 1}");
                    if (map == null) continue;

                    var capturedId = id;
                    map["Navigate"].performed += ctx => _upgradeUIInputBridge.OnNavigate(capturedId, ctx);
                    map["Submit"].performed += ctx => _upgradeUIInputBridge.OnSubmit(capturedId, ctx);
                    _upgradeMaps.Add((map, id));
                }
            }
        }

        private void OnPausePerformed(InputAction.CallbackContext ctx)
        {
            _scheduler.TogglePause();
        }

        public void Dispose()
        {
            if (_pauseAction != null)
                _pauseAction.performed -= OnPausePerformed;

            foreach (var (map, _) in _upgradeMaps)
            {
                map?.Disable();
            }
            _upgradeMaps.Clear();

            foreach (var go in _dynamicAdapters)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            _dynamicAdapters.Clear();

            _inputMapSwitcher?.Dispose();
        }
    }
}
