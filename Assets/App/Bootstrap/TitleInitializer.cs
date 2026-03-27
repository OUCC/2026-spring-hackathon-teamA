using VContainer.Unity;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Input.Infrastructure;
using FloorBreaker.UI.RuntimeUI.Documents;
using FloorBreaker.UI.Title.Presentation;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Title シーンの初期化 EntryPoint。
    /// TitlePresenter を生成し、BGM・ボタン・キーコンフィグを接続する。
    /// </summary>
    public sealed class TitleInitializer : IStartable
    {
        private readonly TitleUIDocument _doc;
        private readonly IAudioService _audio;
        private readonly MatchModeConfig _modeConfig;
        private readonly ISceneTransitionService _sceneTransition;
        private readonly KeyRebindingService _rebindService;

        public TitleInitializer(
            TitleUIDocument doc,
            IAudioService audio,
            MatchModeConfig modeConfig,
            ISceneTransitionService sceneTransition,
            KeyRebindingService rebindService = null)
        {
            _doc = doc;
            _audio = audio;
            _modeConfig = modeConfig;
            _sceneTransition = sceneTransition;
            _rebindService = rebindService;
        }

        public void Start()
        {
            // 保存済みキーバインドを適用
            _rebindService?.LoadOverrides();

            // TitlePresenter を生成（ボタンコールバック・BGM・音量・キーコンフィグを接続）
            new TitlePresenter(_doc, _audio, _rebindService, _modeConfig, _sceneTransition);
        }
    }
}
