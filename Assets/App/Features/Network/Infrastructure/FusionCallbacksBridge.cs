using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// NetworkRunner の GameObject に付与し、INetworkRunnerCallbacks を実装する。
    /// 受け取ったコールバックを NetworkConnectionService に委譲する薄いブリッジ。
    /// 注: Initialize() はセッター注入だが、Fusion が AddComponent で MonoBehaviour を追加するため
    /// コンストラクタ DI は使用不可能（Fusion アーキテクチャ制約）。
    /// </summary>
    public sealed class FusionCallbacksBridge : MonoBehaviour, INetworkRunnerCallbacks
    {
        private NetworkConnectionService _service;
        private NetworkInputCollector _inputCollector;

        public void Initialize(NetworkConnectionService service) => _service = service;
        public void SetInputCollector(NetworkInputCollector collector) => _inputCollector = collector;

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
            => _service?.HandlePlayerJoined(runner, player);

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
            => _service?.HandlePlayerLeft(runner, player);

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
            => _service?.HandleShutdown(shutdownReason);

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
            => _service?.HandleDisconnected(reason);

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
            => _service?.HandleConnectFailed(reason);

        // --- Phase 1 で未使用のコールバック ---

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_inputCollector != null)
                input.Set(_inputCollector.CollectInput(runner));
        }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    }
}
