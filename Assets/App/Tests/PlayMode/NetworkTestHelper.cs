using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using VContainer.Unity;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Network.Infrastructure;
using FloorBreaker.Bootstrap;

namespace FloorBreaker.Tests.PlayMode
{
    /// <summary>
    /// PlayMode テスト用ヘルパー。
    /// Boot シーンをロードして ProjectLifetimeScope を構築し、
    /// Fusion Runner を GameMode.Single で起動してネットワーク統合テストを可能にする。
    /// </summary>
    public static class NetworkTestHelper
    {
        private static NetworkRunner _runner;
        private static bool _initialized;

        /// <summary>
        /// Boot シーン → MatchModeConfig 設定 → Runner 起動 → Match シーンロード。
        /// テストの [UnitySetUp] から呼ぶ。
        /// </summary>
        public static IEnumerator SetupOnlineMatch(int playerCount = 2)
        {
            if (_initialized) yield break;

            // 1. Boot シーンロード（ProjectLifetimeScope が構築される）
            var loadBoot = SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return new WaitUntil(() => loadBoot.isDone);

            // ProjectLifetimeScope が Awake で DI 構築するまで1フレーム待機
            yield return null;

            // 2. ProjectLifetimeScope から MatchModeConfig を取得
            var rootScope = Object.FindAnyObjectByType<ProjectLifetimeScope>();
            if (rootScope == null)
            {
                Debug.LogError("[NetworkTestHelper] ProjectLifetimeScope not found in Boot scene");
                yield break;
            }

            var container = rootScope.Container;
            var modeConfig = container.Resolve(typeof(MatchModeConfig)) as MatchModeConfig;
            if (modeConfig == null)
            {
                Debug.LogError("[NetworkTestHelper] MatchModeConfig not found");
                yield break;
            }

            // 3. オンラインモード設定
            modeConfig.IsOnline = true;
            modeConfig.IsHost = true;
            modeConfig.PlayerCount = playerCount;
            modeConfig.IsCpuSlot = new bool[4];
            // DeviceTypes はデフォルト（KeyboardWasd）

            // 4. NetworkConnectionService を取得して Runner 起動
            var connectionService = container.Resolve(typeof(NetworkConnectionService)) as NetworkConnectionService;
            if (connectionService == null)
            {
                Debug.LogError("[NetworkTestHelper] NetworkConnectionService not found");
                yield break;
            }

            // GameMode.Single で起動（ネットワーク接続なし）
            yield return StartRunnerSingle(connectionService);

            // 5. Match シーンをロード（Runner から直接呼ぶ — ConnectionService の _runner は未設定のため）
            if (_runner != null)
            {
                _runner.LoadScene(SceneRef.FromIndex(2), LoadSceneMode.Additive);
                Debug.Log("[NetworkTestHelper] LoadScene called for Match (index 2)");
            }

            // 6. シーンロード完了 + DI 構築完了を待機
            yield return WaitForBridge(timeoutSeconds: 15f);

            _initialized = true;
            Debug.Log("[NetworkTestHelper] Setup complete");
        }

        /// <summary>
        /// テスト終了後のクリーンアップ。[UnityTearDown] から呼ぶ。
        /// </summary>
        public static IEnumerator Teardown()
        {
            if (_runner != null)
            {
                yield return _runner.Shutdown();
                _runner = null;
            }

            NetworkServiceBridge.Current = null;
            _initialized = false;
        }

        /// <summary>GameMode.Single で Runner を起動する。</summary>
        private static IEnumerator StartRunnerSingle(NetworkConnectionService connectionService)
        {
            // NetworkConnectionService の内部メソッドは使えないため、直接 Runner を作成
            var runnerObj = new GameObject("[TestNetworkRunner]");
            Object.DontDestroyOnLoad(runnerObj);
            _runner = runnerObj.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;

            var callbacksBridge = runnerObj.AddComponent<FusionCallbacksBridge>();
            callbacksBridge.Initialize(connectionService);

            var sceneManager = runnerObj.AddComponent<FusionSceneManager>();
            var rootScope = Object.FindAnyObjectByType<ProjectLifetimeScope>();
            if (rootScope != null)
                sceneManager.SetRootScope(rootScope);

            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SceneManager = sceneManager,
            });

            // StartGame の完了を待機
            while (!startTask.IsCompleted)
                yield return null;

            if (!startTask.Result.Ok)
            {
                Debug.LogError($"[NetworkTestHelper] Runner start failed: {startTask.Result.ShutdownReason}");
                yield break;
            }

            // connectionService に Runner を認識させる
            // （内部フィールドにアクセスできないため、LoadMatchScene 用に Runner を公開プロパティから取得）
            Debug.Log("[NetworkTestHelper] Runner started in Single mode");
        }

        /// <summary>NetworkServiceBridge.Current が非 null になるまで待機。</summary>
        private static IEnumerator WaitForBridge(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (NetworkServiceBridge.Current == null && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (NetworkServiceBridge.Current == null)
            {
                Debug.LogError($"[NetworkTestHelper] NetworkServiceBridge.Current still null after {timeoutSeconds}s");
            }
            else
            {
                Debug.Log("[NetworkTestHelper] NetworkServiceBridge.Current is set");
            }
        }

        /// <summary>NetworkMatchRunner が初期化完了するまで待機。</summary>
        public static IEnumerator WaitForMatchRunnerInit(float timeoutSeconds = 10f)
        {
            float elapsed = 0f;
            NetworkMatchRunner runner = null;

            while (elapsed < timeoutSeconds)
            {
                runner = Object.FindAnyObjectByType<NetworkMatchRunner>();
                if (runner != null)
                {
                    // _initialized フィールドは private なので、
                    // Bridge の存在で初期化完了を推定
                    if (NetworkServiceBridge.Current != null)
                    {
                        // 数フレーム待って FixedUpdateNetwork が実行されることを確認
                        yield return null;
                        yield return null;
                        yield return null;
                        Debug.Log("[NetworkTestHelper] NetworkMatchRunner is ready");
                        yield break;
                    }
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            Debug.LogError("[NetworkTestHelper] NetworkMatchRunner init timeout");
        }
    }
}
