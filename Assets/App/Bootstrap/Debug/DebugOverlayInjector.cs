using UnityEngine;
using VContainer.Unity;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// DebugOverlay シーンに配置するブートストラップ。
    /// MatchLifetimeScope を発見し、同一コンテナから InjectGameObject で注入する。
    /// VContainer 公式 API を使い、Scoped インスタンスをゲームと共有する。
    /// </summary>
    public sealed class DebugOverlayInjector : MonoBehaviour
    {
        private void Start()
        {
            var matchScope = LifetimeScope.Find<MatchLifetimeScope>();
            if (matchScope?.Container == null)
            {
                Debug.LogWarning("[DebugOverlayInjector] MatchLifetimeScope not found");
                return;
            }

            matchScope.Container.InjectGameObject(gameObject);
        }
    }
}
