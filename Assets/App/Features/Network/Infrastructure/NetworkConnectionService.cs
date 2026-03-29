using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;
using R3;

namespace FloorBreaker.Network.Infrastructure
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        InRoom,
    }

    /// <summary>
    /// NetworkRunner のライフサイクルを管理し、接続状態を R3 で公開する。
    /// VContainer の Singleton として登録される plain C# クラス。
    /// </summary>
    public sealed class NetworkConnectionService : IDisposable
    {
        private NetworkRunner _runner;
        private FusionCallbacksBridge _callbacksBridge;
        private LobbyController _lobbyController;
        private NetworkObject _lobbyControllerPrefab;
        private bool _needsLobbyControllerDiscovery;

        private readonly ReactiveProperty<ConnectionState> _state = new(ConnectionState.Disconnected);
        private readonly ReactiveProperty<int> _connectedPlayerCount = new(0);
        private readonly Subject<string> _errorOccurred = new();
        private readonly Subject<Unit> _matchStartRequested = new();

        public ReadOnlyReactiveProperty<ConnectionState> State => _state;
        public ReadOnlyReactiveProperty<int> ConnectedPlayerCount => _connectedPlayerCount;
        public Observable<string> ErrorOccurred => _errorOccurred;
        public Observable<Unit> MatchStartRequested => _matchStartRequested;

        public bool IsHost { get; private set; }
        public NetworkRunner Runner => _runner;
        public LobbyController LobbyController => _lobbyController;

        /// <summary>
        /// ローカルプレイヤーのインデックス（0-based）。
        /// Fusion Host Mode ではホスト=0、クライアント=1,2,3...
        /// Runner が未起動の場合は 0 を返す。
        /// </summary>
        public int LocalPlayerIndex => _runner != null ? _runner.LocalPlayer.AsIndex : 0;

        /// <summary>LobbyController が発見/生成された際に発火する（static event の代替）。</summary>
        public event Action<LobbyController> LobbyControllerDiscovered;

        /// <summary>入力コレクターを FusionCallbacksBridge にセットする。</summary>
        public void SetInputCollector(NetworkInputCollector collector)
        {
            _callbacksBridge?.SetInputCollector(collector);
        }

        /// <summary>ホストとしてルームを作成する。</summary>
        public async UniTask CreateRoomAsync(string roomCode, int maxPlayers)
        {
            if (_runner != null) await ShutdownAsync();

            _state.Value = ConnectionState.Connecting;
            IsHost = true;

            try
            {
                CreateRunner();

                var result = await _runner.StartGame(new StartGameArgs
                {
                    GameMode = GameMode.Host,
                    SessionName = roomCode,
                    PlayerCount = maxPlayers,
                });

                if (result.Ok)
                {
                    _state.Value = ConnectionState.InRoom;
                    _connectedPlayerCount.Value = 1;
                    SpawnLobbyController();
                }
                else
                {
                    _state.Value = ConnectionState.Disconnected;
                    CleanupRunner();
                    throw new InvalidOperationException($"部屋の作成に失敗しました: {result.ShutdownReason}");
                }
            }
            catch (Exception) when (_state.Value == ConnectionState.Connecting)
            {
                _state.Value = ConnectionState.Disconnected;
                CleanupRunner();
                throw;
            }
        }

        private void SpawnLobbyController()
        {
            if (_runner == null || !IsHost) return;

            if (_lobbyControllerPrefab == null)
                _lobbyControllerPrefab = Resources.Load<NetworkObject>("Network/LobbyController");

            if (_lobbyControllerPrefab != null)
            {
                var obj = _runner.Spawn(_lobbyControllerPrefab);
                _lobbyController = obj.GetComponent<LobbyController>();
                LobbyControllerDiscovered?.Invoke(_lobbyController);
            }
            else
            {
                Debug.LogWarning("[NetworkConnectionService] LobbyController prefab not found in Resources/Network/");
            }
        }

        /// <summary>クライアントとしてルームに参加する。</summary>
        public async UniTask JoinRoomAsync(string roomCode)
        {
            if (_runner != null) await ShutdownAsync();

            _state.Value = ConnectionState.Connecting;
            IsHost = false;

            try
            {
                CreateRunner();

                var result = await _runner.StartGame(new StartGameArgs
                {
                    GameMode = GameMode.Client,
                    SessionName = roomCode,
                });

                if (result.Ok)
                {
                    _state.Value = ConnectionState.InRoom;
                }
                else
                {
                    _state.Value = ConnectionState.Disconnected;
                    CleanupRunner();

                    string message = result.ShutdownReason switch
                    {
                        ShutdownReason.GameNotFound => "ルームが見つかりません",
                        ShutdownReason.GameIsFull => "ルームが満員です",
                        _ => $"接続に失敗しました: {result.ShutdownReason}",
                    };
                    throw new InvalidOperationException(message);
                }
            }
            catch (Exception) when (_state.Value == ConnectionState.Connecting)
            {
                _state.Value = ConnectionState.Disconnected;
                CleanupRunner();
                throw;
            }
        }

        /// <summary>接続を切断し Runner を破棄する。</summary>
        public async UniTask ShutdownAsync()
        {
            if (_runner != null)
            {
                await _runner.Shutdown();
            }
            CleanupRunner();
            _state.Value = ConnectionState.Disconnected;
            _connectedPlayerCount.Value = 0;
            IsHost = false;
        }

        // --- FusionCallbacksBridge からの委譲 ---

        internal void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            _connectedPlayerCount.Value = runner.ActivePlayers.Count();

            // クライアント側: ホストが Spawn した LobbyController を検出
            if (!IsHost && _lobbyController == null)
            {
                TryDiscoverLobbyController();
            }
        }

        internal void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            _connectedPlayerCount.Value = Math.Max(0, runner.ActivePlayers.Count());
        }

        /// <summary>
        /// クライアント側: LobbyController の検出を試みる。
        /// HandlePlayerJoined で検出できなかった場合、FusionCallbacksBridge.Update() から毎フレーム呼ばれる。
        /// </summary>
        internal void TryDiscoverLobbyController()
        {
            if (IsHost || _lobbyController != null)
            {
                _needsLobbyControllerDiscovery = false;
                return;
            }

            _lobbyController = UnityEngine.Object.FindAnyObjectByType<LobbyController>();
            if (_lobbyController != null)
            {
                _needsLobbyControllerDiscovery = false;
                Debug.Log("[NetworkConnectionService] LobbyController discovered");
                LobbyControllerDiscovered?.Invoke(_lobbyController);
            }
            else
            {
                _needsLobbyControllerDiscovery = true;
            }
        }

        /// <summary>毎フレーム呼ばれるポーリング。LobbyController 未検出時のリトライ。</summary>
        internal void PollDiscovery()
        {
            if (_needsLobbyControllerDiscovery)
                TryDiscoverLobbyController();
        }

        internal void HandleShutdown(ShutdownReason reason)
        {
            if (_state.Value == ConnectionState.Disconnected) return;

            _state.Value = ConnectionState.Disconnected;
            _connectedPlayerCount.Value = 0;
            CleanupRunner();

            if (reason != ShutdownReason.Ok)
            {
                string message = reason switch
                {
                    ShutdownReason.DisconnectedByPluginLogic => "ホストが切断しました",
                    ShutdownReason.ConnectionRefused => "接続が拒否されました",
                    _ => $"ネットワークエラーが発生しました: {reason}",
                };
                _errorOccurred.OnNext(message);
            }
        }

        internal void HandleDisconnected(NetDisconnectReason reason)
        {
            _state.Value = ConnectionState.Disconnected;
            _connectedPlayerCount.Value = 0;
            CleanupRunner();
            _errorOccurred.OnNext("ホストとの接続が切断されました");
        }

        internal void HandleConnectFailed(NetConnectFailedReason reason)
        {
            _state.Value = ConnectionState.Disconnected;
            CleanupRunner();
            _errorOccurred.OnNext("接続がタイムアウトしました");
        }

        private FusionSceneManager _sceneManager;
        private VContainer.Unity.LifetimeScope _rootScope;

        private void CreateRunner()
        {
            var runnerObj = new GameObject("[NetworkRunner]");
            UnityEngine.Object.DontDestroyOnLoad(runnerObj);
            _runner = runnerObj.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true; // 全ピアが入力を送信（クライアントも OnInput() が必要）

            _callbacksBridge = runnerObj.AddComponent<FusionCallbacksBridge>();
            _callbacksBridge.Initialize(this);

            // カスタムシーンマネージャ: VContainer の EnqueueParent を統合
            _sceneManager = runnerObj.AddComponent<FusionSceneManager>();
            if (_rootScope != null)
                _sceneManager.SetRootScope(_rootScope);
        }

        /// <summary>
        /// FusionSceneManager に VContainer ルートスコープを設定する。
        /// ProjectLifetimeScope の BuildCallback から呼ばれる（CreateRunner より前）。
        /// </summary>
        public void SetRootScope(VContainer.Unity.LifetimeScope rootScope)
        {
            _rootScope = rootScope;
            _sceneManager?.SetRootScope(rootScope);
        }

        /// <summary>Runner 経由でマッチシーンをロードする。Fusion がクライアントにも伝搬する。</summary>
        public void LoadMatchScene()
        {
            if (_runner == null) return;
            // Match シーンのビルドインデックス = 2
            _runner.LoadScene(SceneRef.FromIndex(2), LoadSceneMode.Additive);
        }

        private void CleanupRunner()
        {
            _lobbyController = null;
            if (_runner != null)
            {
                if (_runner.gameObject != null)
                    UnityEngine.Object.Destroy(_runner.gameObject);
                _runner = null;
                _callbacksBridge = null;
            }
        }

        public void Dispose()
        {
            CleanupRunner();
            _state.Dispose();
            _connectedPlayerCount.Dispose();
            _errorOccurred.Dispose();
            _matchStartRequested.Dispose();
        }
    }

    internal static class PlayerRefEnumerable
    {
        public static int Count(this IEnumerable<PlayerRef> players)
        {
            int count = 0;
            foreach (var _ in players) count++;
            return count;
        }
    }
}
