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
        public PlayerPresenter PlayerP1 { get; set; }
        public PlayerPresenter PlayerP2 { get; set; }
        public BombPresenter Bomb { get; set; }
        public SlimePresenter Slime { get; set; }
        public PlayerHudPresenter HudP1 { get; set; }
        public PlayerHudPresenter HudP2 { get; set; }
        public UpgradeOverlayPresenter UpgradeOverlay { get; set; }
        public ResultPresenter Result { get; set; }

        public void TickPresenters(float deltaTime)
        {
            PlayerP1?.Tick(deltaTime);
            PlayerP2?.Tick(deltaTime);
            Bomb?.Tick(deltaTime);
            HudP1?.UpdatePerFrame();
            HudP2?.UpdatePerFrame();
            UpgradeOverlay?.UpdateCountdown();
        }

        public void Dispose()
        {
            Stage?.Dispose();
            ShrinkAnimator?.Dispose();
            PlayerP1?.Dispose();
            PlayerP2?.Dispose();
            Bomb?.Dispose();
            Slime?.Dispose();
            HudP1?.Dispose();
            HudP2?.Dispose();
            UpgradeOverlay?.Dispose();
            Result?.Dispose();
        }
    }
}
