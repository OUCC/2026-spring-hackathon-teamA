using Cysharp.Threading.Tasks;

namespace FloorBreaker.Shared.Application.Interfaces
{
    /// <summary>
    /// シーン遷移を抽象化するインターフェース。
    /// Presentation 層が UnityEngine.SceneManagement に直接依存しないようにする。
    /// </summary>
    public interface ISceneTransitionService
    {
        UniTask LoadMatchAsync();
        UniTask LoadTitleAsync();
    }
}
