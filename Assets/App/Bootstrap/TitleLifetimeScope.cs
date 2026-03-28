using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using FloorBreaker.Input.Infrastructure;
using FloorBreaker.UI.RuntimeUI.Documents;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Title シーンの DI ルート。ProjectLifetimeScope の子スコープ。
    /// TitlePresenter, KeyRebindingService を Scoped 登録する。
    /// </summary>
    public sealed class TitleLifetimeScope : LifetimeScope
    {
        [SerializeField] private InputActionAsset _inputActions;

        protected override void Configure(IContainerBuilder builder)
        {
            // TitleUIDocument: シーン上の MonoBehaviour
            builder.RegisterComponentInHierarchy<TitleUIDocument>();

            // KeyRebindingService: InputActionAsset が設定されている場合のみ登録
            if (_inputActions != null)
            {
                builder.RegisterInstance(_inputActions); // Singleton (implicit)
                builder.Register<KeyRebindingService>(Lifetime.Scoped);
            }

            // EntryPoint: TitleInitializer が TitlePresenter を生成・接続する
            builder.RegisterEntryPoint<TitleInitializer>();
        }
    }
}
