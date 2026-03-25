using System;
using R3;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;

namespace FloorBreaker.UI.HUD.Presentation
{
    /// <summary>
    /// 1 プレイヤー分の HUD を駆動する Presenter。
    /// R3 購読で HP/コイン/強化一覧を更新し、Tick でタイマーと CD ゲージを更新する。
    /// </summary>
    public sealed class PlayerHudPresenter : IDisposable
    {
        private readonly PlayerHudView _view;
        private readonly PlayerBuild _build;
        private readonly BombCooldownState _cooldown;
        private readonly MatchClock _clock;

        private readonly IDisposable _hpSub;
        private readonly IDisposable _coinsSub;
        private readonly IDisposable _upgradesSub;

        public PlayerHudPresenter(
            PlayerHudView view,
            PlayerStats stats,
            PlayerBuild build,
            BombCooldownState cooldown,
            MatchClock clock)
        {
            _view = view;
            _build = build;
            _cooldown = cooldown;
            _clock = clock;

            _hpSub = stats.CurrentHp.Subscribe(hp => _view.SetHp(hp, stats.MaxHp));
            _coinsSub = stats.Coins.Subscribe(coins => _view.SetCoins(coins));
            _upgradesSub = build.AcquiredUpgrades.Subscribe(
                upgrades => _view.SetAcquiredUpgrades(upgrades));
        }

        /// <summary>
        /// 毎フレーム呼び出し: タイマーと CD ゲージを更新する。
        /// </summary>
        public void UpdatePerFrame()
        {
            // タイマー
            int seconds = (int)MathF.Ceiling(_clock.RemainingValue);
            _view.SetTimer(seconds);

            // CD ゲージ
            float fireMax = _build.FireCooldown;
            float fireRemaining = _cooldown.FireBombRemaining.CurrentValue;
            _view.SetFireCooldown(fireMax > 0f ? fireRemaining / fireMax : 0f);

            float fallMax = _build.FallCooldown;
            float fallRemaining = _cooldown.FallBombRemaining.CurrentValue;
            _view.SetFallCooldown(fallMax > 0f ? fallRemaining / fallMax : 0f);
        }

        public void Dispose()
        {
            _hpSub.Dispose();
            _coinsSub.Dispose();
            _upgradesSub.Dispose();
        }
    }
}
