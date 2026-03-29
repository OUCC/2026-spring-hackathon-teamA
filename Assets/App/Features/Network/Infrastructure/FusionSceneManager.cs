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
        /// Boot シーンの ProjectLifetimeScope を設定する。
        /// EnqueueParent でマッチシーンの LifetimeScope が子になる。
        /// </summary>
        public void SetRootScope(LifetimeScope rootScope)
        {
            _rootScope = rootScope;
        }

        protected override IEnumerator LoadSceneCoroutine(SceneRef sceneRef, NetworkLoadSceneParameters sceneParams)
        {
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
                _enqueueHandle = LifetimeScope.EnqueueParent(_rootScope);
            }

            // 基底クラスの LoadSceneCoroutine を実行
            // （内部で SceneManager.LoadSceneAsync が呼ばれ、MatchLifetimeScope が構築される）
            yield return base.LoadSceneCoroutine(sceneRef, sceneParams);

            // EnqueueParent のハンドルを解放
            _enqueueHandle?.Dispose();
            _enqueueHandle = null;
        }
    }
}
