using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using VContainer.Unity;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// Fusion 2 のシーン管理と VContainer の LifetimeScope 階層を統合する。
    /// シーンロード前に EnqueueParent を呼び出し、MatchLifetimeScope が
    /// ProjectLifetimeScope の子スコープとして構築されるようにする。
    ///
    /// また、既存の Title シーンをアンロードしてからマッチシーンをロードする。
    /// </summary>
    public class FusionSceneManager : NetworkSceneManagerDefault
    {
        private LifetimeScope _rootScope;
        private IDisposable _enqueueHandle;

        /// <summary>
        /// 現在シーンをロード中の Runner。MatchLifetimeScope が BuildCallback で
        /// NetworkServiceBridge.Register(runner, bridge) するために参照する。
        /// LoadSceneCoroutine の開始時にセットされ、完了後にクリアされる。
        /// </summary>
        public static NetworkRunner CurrentLoadingRunner { get; private set; }

        /// <summary>
        /// Boot シーンの ProjectLifetimeScope を設定する。
        /// EnqueueParent でマッチシーンの LifetimeScope が子になる。
        /// </summary>
        public void SetRootScope(LifetimeScope rootScope)
        {
            _rootScope = rootScope;
        }

        protected override IEnumerator LoadSceneCoroutine(SceneRef sceneRef, NetworkLoadSceneParameters sceneParams)
        {
            // Runner 参照を公開（MatchLifetimeScope が BuildCallback で使用）
            CurrentLoadingRunner = Runner;

            // Title シーンがロード中ならアンロード
            var titleScene = SceneManager.GetSceneByName("Title");
            if (titleScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(titleScene);
                while (unload != null && !unload.isDone)
                    yield return null;
            }

            // EnqueueParent: 次に生成される LifetimeScope の親を指定
            if (_rootScope != null)
            {
                Debug.Log($"[FusionSceneManager] EnqueueParent: rootScope={_rootScope.name}");
                _enqueueHandle = LifetimeScope.EnqueueParent(_rootScope);
            }
            else
            {
                Debug.LogError("[FusionSceneManager] _rootScope is null! DI hierarchy will not be established.");
            }

            // 基底クラスの LoadSceneCoroutine を実行
            // （内部で SceneManager.LoadSceneAsync が呼ばれ、MatchLifetimeScope が構築される）
            yield return base.LoadSceneCoroutine(sceneRef, sceneParams);

            // EnqueueParent のハンドルを解放
            _enqueueHandle?.Dispose();
            _enqueueHandle = null;

            // Runner 参照をクリア
            CurrentLoadingRunner = null;
        }
    }
}
