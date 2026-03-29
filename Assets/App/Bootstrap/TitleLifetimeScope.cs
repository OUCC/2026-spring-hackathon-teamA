using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using FloorBreaker.Input.Infrastructure;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Stage.Presentation;
using FloorBreaker.UI.RuntimeUI.Documents;
using FloorBreaker.UI.Title.Presentation;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Title シーンの DI ルート。ProjectLifetimeScope の子スコープ。
    /// TitlePresenter, KeyRebindingService を Scoped 登録する。
    /// </summary>
    public sealed class TitleLifetimeScope : LifetimeScope
    {
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private TileSpriteConfig _tileSpriteConfig;
        [SerializeField] private GameObject _tilePrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            // TitleUIDocument: シーン上の MonoBehaviour
            builder.RegisterComponentInHierarchy<TitleUIDocument>();

            // TileSpriteConfig: ステージプレビューで実タイル表示に使用
            if (_tileSpriteConfig != null)
                builder.RegisterInstance(_tileSpriteConfig);

            // LobbyConfigUseCase: ロビー設定を MatchModeConfig に適用
            builder.Register<LobbyConfigUseCase>(Lifetime.Scoped);

            // StageGenerationService: ステージ生成 Domain サービス
            builder.Register<StageGenerationService>(Lifetime.Scoped);

            // StagePreviewRenderer: オフスクリーンプレビュー
            if (_tilePrefab != null && _tileSpriteConfig != null)
            {
                builder.Register(c => new StagePreviewRenderer(
                    _tilePrefab, _tileSpriteConfig, c.Resolve<StageGenerationService>()),
                    Lifetime.Scoped);
            }

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
