using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer.Unity;
using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Shared.Infrastructure.SceneTransition
{
    /// <summary>
    /// Additive シーンロード + VContainer EnqueueParent による遷移実装。
    /// Boot シーンの ProjectLifetimeScope をルートとし、
    /// 各ゲームプレイシーンを子スコープとしてアディティブロードする。
    /// </summary>
    public sealed class UnitySceneTransitionService : ISceneTransitionService
    {
        private readonly LifetimeScope _rootScope;
        private string _currentScene;

        public UnitySceneTransitionService(LifetimeScope rootScope)
        {
            _rootScope = rootScope;
        }

        public UniTask LoadMatchAsync() => TransitionTo("Match");

        public UniTask LoadTitleAsync() => TransitionTo("Title");

        private async UniTask TransitionTo(string sceneName)
        {
            if (_currentScene != null)
            {
                var unload = SceneManager.UnloadSceneAsync(_currentScene);
                await UniTask.WaitUntil(() => unload.isDone);
            }

            using (LifetimeScope.EnqueueParent(_rootScope))
            {
                var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                await UniTask.WaitUntil(() => load.isDone);
            }

            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
            _currentScene = sceneName;
        }
    }
}
