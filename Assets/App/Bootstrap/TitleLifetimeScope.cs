using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Audio;
using FloorBreaker.Shared.Infrastructure.SceneTransition;
using FloorBreaker.MatchFlow.Application;
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
            // 親 (ProjectLifetimeScope) が無い場合のフォールバック登録
            if (Parent == null)
            {
                Debug.Log("[TitleLifetimeScope] No parent scope — registering fallback globals");

                builder.Register<UnitySceneTransitionService>(Lifetime.Singleton)
                    .As<ISceneTransitionService>();
                builder.Register<MatchModeConfig>(Lifetime.Singleton);

                // AudioService: シーン内に AudioService があれば使う、なければ NullAudioService
                var audioService = FindAnyObjectByType<AudioService>();
                if (audioService != null)
                    builder.RegisterInstance<IAudioService>(audioService);
                else
                    builder.Register<IAudioService>(
                        c => new NullAudioService(), Lifetime.Singleton);
            }

            // TitleUIDocument: シーン上の MonoBehaviour
            builder.RegisterComponentInHierarchy<TitleUIDocument>();

            // KeyRebindingService: InputActionAsset が設定されている場合のみ登録
            if (_inputActions != null)
            {
                builder.RegisterInstance(_inputActions);
                builder.Register<KeyRebindingService>(Lifetime.Scoped);
            }

            // EntryPoint: TitleInitializer が TitlePresenter を生成・接続する
            builder.RegisterEntryPoint<TitleInitializer>();
        }
    }
}
