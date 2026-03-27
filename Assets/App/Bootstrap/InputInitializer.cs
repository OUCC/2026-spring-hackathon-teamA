using System;
using UnityEngine;
using UnityEngine.InputSystem;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Input.Application;
using FloorBreaker.Input.Infrastructure;
using FloorBreaker.MatchFlow.Application;

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
        private readonly MatchConfig _matchConfig;

        private InputMapSwitcher _inputMapSwitcher;
        private InputActionMap _upgradeP1Map;
        private InputActionMap _upgradeP2Map;

        public InputInitializer(
            GameplayInputBridge gameplayInputBridge,
            UpgradeUIInputBridge upgradeUIInputBridge,
            MatchClock clock,
            MatchConfig matchConfig)
        {
            _gameplayInputBridge = gameplayInputBridge;
            _upgradeUIInputBridge = upgradeUIInputBridge;
            _clock = clock;
            _matchConfig = matchConfig;
        }

        /// <summary>
        /// Input 配線を実行する。
        /// PlayerInputAdapter の検出、初期化、InputMapSwitcher 生成、
        /// UpgradeUI アクションマップとの接続を行う。
        /// </summary>
        public void Initialize()
        {
            // 1. PlayerInputAdapter 検出
            var inputAdapters = UnityEngine.Object.FindObjectsByType<PlayerInputAdapter>(
                FindObjectsSortMode.None);

            // アダプターから InputActionAsset を取得
            InputActionAsset inputActions = null;
            foreach (var adapter in inputAdapters)
            {
                if (adapter.InputActions != null)
                {
                    inputActions = adapter.InputActions;
                    break;
                }
            }

            // 2. P1, P2 アダプターを初期化
            // CPU モード時は P2 のアダプターを登録しない
            int maxAdapters = _matchConfig.IsCpuPlayer ? 1 : 2;
            for (int i = 0; i < inputAdapters.Length && i < maxAdapters; i++)
            {
                var adapter = inputAdapters[i];
                var id = i == 0 ? PlayerId.Player1 : PlayerId.Player2;
                adapter.Initialize(id, inputActions);
                _gameplayInputBridge.RegisterAdapter(adapter);
            }

            // 3. InputMapSwitcher 生成
            if (inputActions != null)
            {
                _inputMapSwitcher = new InputMapSwitcher(inputActions, _clock);

                // 4. UpgradeUI アクションマップとの接続
                _upgradeP1Map = inputActions.FindActionMap("UpgradeUI_P1");
                _upgradeP2Map = inputActions.FindActionMap("UpgradeUI_P2");

                if (_upgradeP1Map != null)
                {
                    _upgradeP1Map["Navigate"].performed += _upgradeUIInputBridge.OnNavigateP1;
                    _upgradeP1Map["Submit"].performed += _upgradeUIInputBridge.OnSubmitP1;
                }

                // CPU モード時は P2 の UpgradeUI 入力を接続しない
                if (_upgradeP2Map != null && !_matchConfig.IsCpuPlayer)
                {
                    _upgradeP2Map["Navigate"].performed += _upgradeUIInputBridge.OnNavigateP2;
                    _upgradeP2Map["Submit"].performed += _upgradeUIInputBridge.OnSubmitP2;
                }
            }
        }

        public void Dispose()
        {
            // UpgradeUI アクションマップのコールバック解除
            if (_upgradeP1Map != null)
            {
                _upgradeP1Map["Navigate"].performed -= _upgradeUIInputBridge.OnNavigateP1;
                _upgradeP1Map["Submit"].performed -= _upgradeUIInputBridge.OnSubmitP1;
            }
            if (_upgradeP2Map != null && !_matchConfig.IsCpuPlayer)
            {
                _upgradeP2Map["Navigate"].performed -= _upgradeUIInputBridge.OnNavigateP2;
                _upgradeP2Map["Submit"].performed -= _upgradeUIInputBridge.OnSubmitP2;
            }

            _inputMapSwitcher?.Dispose();
        }
    }
}
