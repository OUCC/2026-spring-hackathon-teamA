using System;
using FloorBreaker.Stage.Presentation;
using FloorBreaker.Player.Presentation;
using FloorBreaker.Bombs.Presentation;
using FloorBreaker.Slimes.Presentation;
using FloorBreaker.UI.HUD.Presentation;
using FloorBreaker.UI.UpgradeOverlay.Presentation;
using FloorBreaker.UI.Result.Presentation;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// ランタイム生成される Presenter 群のホルダー。
    /// PresentationInitializer が Initialize で生成して格納し、
    /// MatchTickRunner が TickPresenters で毎フレーム駆動する。
    /// </summary>
    public sealed class MatchPresenters : IDisposable
    {
        public StagePresenter Stage { get; set; }
        public StageShrinkAnimator ShrinkAnimator { get; set; }
        public ShrinkWarningPresenter ShrinkWarning { get; set; }
        public PlayerPresenter[] Players { get; set; } = Array.Empty<PlayerPresenter>();
        public BombPresenter Bomb { get; set; }
        public SlimePresenter Slime { get; set; }
        public PlayerHudPresenter[] Huds { get; set; } = Array.Empty<PlayerHudPresenter>();
        public UpgradeOverlayPresenter UpgradeOverlay { get; set; }
        public ResultPresenter Result { get; set; }

        public void TickPresenters(float deltaTime)
        {
            foreach (var player in Players)
                player?.Tick(deltaTime);
            Bomb?.Tick(deltaTime);
            foreach (var hud in Huds)
                hud?.UpdatePerFrame();
            UpgradeOverlay?.UpdateCountdown();
            Stage?.TickFireDecay();
            Stage?.TickRecoveryPreview();
            ShrinkWarning?.Tick();
        }

        public void Dispose()
        {
            Stage?.Dispose();
            ShrinkAnimator?.Dispose();
            ShrinkWarning?.Dispose();
            foreach (var player in Players)
                player?.Dispose();
            Bomb?.Dispose();
            Slime?.Dispose();
            foreach (var hud in Huds)
                hud?.Dispose();
            UpgradeOverlay?.Dispose();
            Result?.Dispose();
        }
    }
}
