using UnityEngine.SceneManagement;
using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Shared.Infrastructure.SceneTransition
{
    /// <summary>
    /// UnityEngine.SceneManagement を使ったシーン遷移実装。
    /// </summary>
    public sealed class UnitySceneTransitionService : ISceneTransitionService
    {
        public void LoadMatch() => SceneManager.LoadScene("Match");
        public void LoadTitle() => SceneManager.LoadScene("Title");
    }
}
