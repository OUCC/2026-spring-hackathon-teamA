using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer.Unity;
using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Boot シーンの EntryPoint。起動時に最初のゲームプレイシーンをアディティブロードする。
    /// DebugMode 時は Title をスキップし Match + DebugOverlay を直接ロードする。
    /// </summary>
    public sealed class BootInitializer : IAsyncStartable
    {
        private readonly ISceneTransitionService _sceneTransition;
        private readonly BootConfig _config;

        public BootInitializer(ISceneTransitionService sceneTransition, BootConfig config)
        {
            _sceneTransition = sceneTransition;
            _config = config;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            if (_config.DebugMode)
            {
                await _sceneTransition.LoadMatchAsync();
                // DebugOverlay をアディティブロード (EnqueueParent 不要 — LifetimeScope を持たない)
                var loadOp = SceneManager.LoadSceneAsync("DebugOverlay", LoadSceneMode.Additive);
                await UniTask.WaitUntil(() => loadOp.isDone);
            }
            else
            {
                await _sceneTransition.LoadTitleAsync();
            }
        }
    }

    /// <summary>
    /// Boot 起動設定。Inspector の _debugMode フラグを保持する。
    /// </summary>
    public sealed class BootConfig
    {
        public bool DebugMode { get; }

        public BootConfig(bool debugMode)
        {
            DebugMode = debugMode;
        }
    }
}
