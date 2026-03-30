using System;
using VContainer;
using VContainer.Unity;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Input.Infrastructure;
using FloorBreaker.Network.Infrastructure;
using FloorBreaker.Stage.Presentation;
using FloorBreaker.UI.RuntimeUI.Documents;
using FloorBreaker.UI.Title.Presentation;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Title シーンの初期化 EntryPoint。
    /// TitlePresenter を生成し、BGM・ボタン・キーコンフィグを接続する。
    /// </summary>
    public sealed class TitleInitializer : IStartable, IDisposable
    {
        private readonly TitleUIDocument _doc;
        private readonly IAudioService _audio;
        private readonly MatchModeConfig _modeConfig;
        private readonly ISceneTransitionService _sceneTransition;
        private readonly IRandomProvider _random;
        private readonly NetworkConnectionService _connectionService;
        private readonly LobbyConfigUseCase _lobbyConfig;
        private readonly IObjectResolver _resolver;
        private TitleInputBridge _inputBridge;

        public TitleInitializer(
            TitleUIDocument doc,
            IAudioService audio,
            MatchModeConfig modeConfig,
            ISceneTransitionService sceneTransition,
            IRandomProvider random,
            NetworkConnectionService connectionService,
            LobbyConfigUseCase lobbyConfig,
            IObjectResolver resolver)
        {
            _doc = doc;
            _audio = audio;
            _modeConfig = modeConfig;
            _sceneTransition = sceneTransition;
            _random = random;
            _connectionService = connectionService;
            _lobbyConfig = lobbyConfig;
            _resolver = resolver;
        }

        public void Start()
        {
            // optional 依存を手動解決（VContainer が optional パラメータを解決できないため）
            var rebindService = TryResolve<KeyRebindingService>();
            var tileSpriteConfig = TryResolve<TileSpriteConfig>();
            var previewRenderer = TryResolve<StagePreviewRenderer>();

            // 保存済みキーバインドを適用
            rebindService?.LoadOverrides();

            // ロビーPresenter を生成
            var lobbyPresenter = new NetworkLobbyPresenter(
                _doc, _connectionService, _modeConfig, _lobbyConfig, _audio, _sceneTransition, _random,
                tileSpriteConfig, previewRenderer);

            // TitleInputBridge を生成（TitleInitializer が Dispose で破棄する）
            var inputActions = rebindService?.Actions;
            _inputBridge = new TitleInputBridge(inputActions);

            // TitlePresenter を生成（ボタンコールバック・BGM・音量・キーコンフィグを接続）
            new TitlePresenter(_doc, _audio, rebindService, _modeConfig, _sceneTransition,
                lobbyPresenter: lobbyPresenter, tileSpriteConfig: tileSpriteConfig,
                previewRenderer: previewRenderer, random: _random,
                inputBridge: _inputBridge);
        }

        public void Dispose()
        {
            _inputBridge?.Dispose();
            _inputBridge = null;
        }

        private T TryResolve<T>() where T : class
        {
            try { return _resolver.Resolve<T>(); }
            catch (System.Exception) { return null; }
        }
    }
}
