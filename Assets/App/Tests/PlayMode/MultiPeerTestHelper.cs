using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using VContainer;
using VContainer.Unity;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Network.Infrastructure;
using FloorBreaker.Bootstrap;

namespace FloorBreaker.Tests.PlayMode
{
    /// <summary>
    /// Multi-Peer PlayMode テスト用ヘルパー。
    /// 1プロセス内で Host + Client を同時起動し、Match シーンをロードする。
    /// 各 Runner は独立した物理シーン・DI コンテナ・NetworkServiceBridge を持つ。
    /// </summary>
    public static class MultiPeerTestHelper
    {
        public static NetworkRunner HostRunner { get; private set; }
        public static NetworkRunner ClientRunner { get; private set; }

        private const string TestSessionName = "PlayModeTest";

        /// <summary>
        /// Boot シーンロード → MatchModeConfig 設定 → Host + Client 起動 → Match ロード。
        /// </summary>
        public static IEnumerator SetupHostAndClient(int playerCount = 2)
        {
            // クリーンアップ
            NetworkServiceBridge.Current = null;

            // 1. Boot シーンロード
            var loadBoot = SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return new WaitUntil(() => loadBoot.isDone);
            yield return null; // Awake 待ち

            var rootScope = Object.FindAnyObjectByType<ProjectLifetimeScope>();
            if (rootScope == null)
            {
                Debug.LogError("[MultiPeerTestHelper] ProjectLifetimeScope not found");
                yield break;
            }

            // 2. MatchModeConfig 設定
            var container = rootScope.Container;
            var modeConfig = container.Resolve(typeof(MatchModeConfig)) as MatchModeConfig;
            modeConfig.IsOnline = true;
            modeConfig.IsHost = true;
            modeConfig.PlayerCount = playerCount;
            modeConfig.IsCpuSlot = new bool[4];
            // ホスト・クライアント共通の乱数シード（盤面一致のため）
            modeConfig.OnlineRandomSeed = 12345;

            // 3. Host Runner 起動
            yield return StartRunner(rootScope, GameMode.Host, isHost: true);
            Debug.Log($"[MultiPeerTestHelper] Host started: {HostRunner != null}");

            // 4. Client Runner 起動（同じ SessionName で接続）
            // クライアント側の MatchModeConfig を設定
            modeConfig.IsHost = false;
            yield return StartRunner(rootScope, GameMode.Client, isHost: false);
            Debug.Log($"[MultiPeerTestHelper] Client started: {ClientRunner != null}");

            // HostRunner の MatchModeConfig を Host に戻す（Host 側の DI で使われるため）
            modeConfig.IsHost = true;

            // 5. 両方の Match シーンロード + Bridge 登録を待機
            yield return WaitForBothBridges(timeoutSeconds: 15f);
        }

        public static IEnumerator Teardown()
        {
            if (ClientRunner != null)
            {
                yield return ClientRunner.Shutdown();
                if (ClientRunner != null && ClientRunner.gameObject != null)
                    Object.Destroy(ClientRunner.gameObject);
                ClientRunner = null;
            }

            if (HostRunner != null)
            {
                yield return HostRunner.Shutdown();
                if (HostRunner != null && HostRunner.gameObject != null)
                    Object.Destroy(HostRunner.gameObject);
                HostRunner = null;
            }

            NetworkServiceBridge.Current = null;
        }

        private static IEnumerator StartRunner(LifetimeScope rootScope, GameMode mode, bool isHost)
        {
            string name = isHost ? "[HostRunner]" : "[ClientRunner]";
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);

            var runner = go.AddComponent<NetworkRunner>();
            runner.ProvideInput = true;

            // Fusion コールバック（OnInput 等）を受け取るブリッジ
            var callbacks = go.AddComponent<FusionCallbacksBridge>();

            var sceneManager = go.AddComponent<FusionSceneManager>();
            sceneManager.SetRootScope(rootScope);

            var startTask = runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = TestSessionName,
                PlayerCount = 4,
                SceneManager = sceneManager,
            });

            while (!startTask.IsCompleted)
                yield return null;

            if (!startTask.Result.Ok)
            {
                Debug.LogError($"[MultiPeerTestHelper] {name} start failed: {startTask.Result.ShutdownReason}");
                Object.Destroy(go);
                yield break;
            }

            if (isHost)
            {
                HostRunner = runner;
                // Host が Match シーンをロード → Fusion が Client にも伝搬
                runner.LoadScene(SceneRef.FromIndex(2), LoadSceneMode.Additive);
            }
            else
            {
                ClientRunner = runner;
                // Client は Host のシーンロードが Fusion で自動伝搬される
            }
        }

        private static IEnumerator WaitForBothBridges(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var hostBridge = HostRunner != null ? NetworkServiceBridge.Get(HostRunner) : null;
                var clientBridge = ClientRunner != null ? NetworkServiceBridge.Get(ClientRunner) : null;

                if (hostBridge != null && clientBridge != null)
                {
                    Debug.Log("[MultiPeerTestHelper] Both bridges registered");
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            var hb = HostRunner != null ? NetworkServiceBridge.Get(HostRunner) : null;
            var cb = ClientRunner != null ? NetworkServiceBridge.Get(ClientRunner) : null;
            Debug.LogError($"[MultiPeerTestHelper] Bridge timeout: host={hb != null}, client={cb != null}");
        }

        /// <summary>指定フレーム数待機する。</summary>
        public static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        /// <summary>
        /// クライアント Runner に NetworkInputCollector をセットし、入力注入を可能にする。
        /// Fusion の OnInput() 経由でホストに入力が届くようになる。
        /// </summary>
        public static NetworkInputCollector SetupClientInput()
        {
            if (ClientRunner == null) return null;

            var clientBridge = NetworkServiceBridge.Get(ClientRunner);
            if (clientBridge == null) return null;

            // クライアントのローカルプレイヤー (Player2 = index 1)
            var localPlayer = clientBridge.Players.Count > 1 ? clientBridge.Players[1] : clientBridge.Players[0];

            // IBalanceParameters を ProjectLifetimeScope から取得
            var rootScope = Object.FindAnyObjectByType<ProjectLifetimeScope>();
            var balance = rootScope.Container.Resolve(typeof(IBalanceParameters)) as IBalanceParameters;

            var collector = new NetworkInputCollector(balance, localPlayer);

            // クライアント Runner の FusionCallbacksBridge にセット
            var callbacks = ClientRunner.GetComponent<FusionCallbacksBridge>();
            callbacks?.SetInputCollector(collector);

            return collector;
        }

        /// <summary>
        /// クライアント入力に移動を注入する。Fusion OnInput() 経由でホストに届く。
        /// </summary>
        public static void InjectClientMove(NetworkInputCollector collector, Direction8 direction)
        {
            collector?.InjectMoveForTest(direction);
        }

        /// <summary>
        /// ホスト Runner で LobbyController をスポーンし、両方で検出されるまで待機。
        /// </summary>
        public static IEnumerator SpawnLobbyController()
        {
            if (HostRunner == null) yield break;

            var prefab = Resources.Load<NetworkObject>("Network/LobbyController");
            if (prefab == null)
            {
                Debug.LogError("[MultiPeerTestHelper] LobbyController prefab not found");
                yield break;
            }

            HostRunner.Spawn(prefab);

            // LobbyController が両 Runner で見えるようになるまで待機
            float elapsed = 0f;
            while (elapsed < 5f)
            {
                var hostLobby = FindLobbyForRunner(HostRunner);
                var clientLobby = FindLobbyForRunner(ClientRunner);
                if (hostLobby != null && clientLobby != null)
                {
                    Debug.Log("[MultiPeerTestHelper] LobbyController found on both runners");
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            Debug.LogError("[MultiPeerTestHelper] LobbyController discovery timeout");
        }

        /// <summary>指定 Runner のシーン内から LobbyController を見つける。</summary>
        public static LobbyController FindLobbyForRunner(NetworkRunner runner)
        {
            if (runner == null) return null;
            foreach (var obj in Object.FindObjectsByType<LobbyController>(FindObjectsSortMode.None))
            {
                if (obj.Runner == runner) return obj;
            }
            return null;
        }
    }
}
