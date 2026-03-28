using System;
using System.Collections.Generic;
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
        private readonly MatchPlayers _players;

        private InputMapSwitcher _inputMapSwitcher;
        private readonly List<(InputActionMap map, PlayerId id)> _upgradeMaps = new();

        public InputInitializer(
            GameplayInputBridge gameplayInputBridge,
            UpgradeUIInputBridge upgradeUIInputBridge,
            MatchClock clock,
            MatchConfig matchConfig,
            MatchPlayers players)
        {
            _gameplayInputBridge = gameplayInputBridge;
            _upgradeUIInputBridge = upgradeUIInputBridge;
            _clock = clock;
            _matchConfig = matchConfig;
            _players = players;
        }

        public void Initialize()
        {
            // 1. PlayerInputAdapter 検出
            var inputAdapters = UnityEngine.Object.FindObjectsByType<PlayerInputAdapter>(
                FindObjectsSortMode.None);

            InputActionAsset inputActions = null;
            foreach (var adapter in inputAdapters)
            {
                if (adapter.InputActions != null)
                {
                    inputActions = adapter.InputActions;
                    break;
                }
            }

            // 2. アダプターを初期化（CPU モード時は最後のプレイヤーを除外）
            int humanCount = _matchConfig.IsCpuPlayer
                ? _players.PlayerCount - 1
                : _players.PlayerCount;
            int maxAdapters = Math.Min(inputAdapters.Length, humanCount);

            for (int i = 0; i < maxAdapters; i++)
            {
                var adapter = inputAdapters[i];
                var id = PlayerId.FromIndex(i);
                adapter.Initialize(id, inputActions);
                _gameplayInputBridge.RegisterAdapter(adapter);
            }

            // 3. InputMapSwitcher 生成
            if (inputActions != null)
            {
                _inputMapSwitcher = new InputMapSwitcher(inputActions, _clock, _players.PlayerCount);

                // 4. UpgradeUI アクションマップとの接続（N 人分）
                for (int i = 0; i < humanCount; i++)
                {
                    var id = PlayerId.FromIndex(i);
                    var map = inputActions.FindActionMap($"UpgradeUI_P{i + 1}");
                    if (map == null) continue;

                    // クロージャで PlayerId をキャプチャ
                    var capturedId = id;
                    map["Navigate"].performed += ctx => _upgradeUIInputBridge.OnNavigate(capturedId, ctx);
                    map["Submit"].performed += ctx => _upgradeUIInputBridge.OnSubmit(capturedId, ctx);
                    _upgradeMaps.Add((map, id));
                }
            }
        }

        public void Dispose()
        {
            // UpgradeUI のコールバックは Lambda キャプチャなので -= で個別解除できない。
            // マップ自体を Disable して参照を切る。
            foreach (var (map, _) in _upgradeMaps)
            {
                map?.Disable();
            }
            _upgradeMaps.Clear();

            _inputMapSwitcher?.Dispose();
        }
    }
}
