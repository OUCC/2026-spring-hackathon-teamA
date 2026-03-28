using System;
using R3;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.UI.RuntimeUI.Controls;

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
            MatchClock clock,
            UpgradeIconMap iconMap = null)
        {
            _view = view;
            _build = build;
            _cooldown = cooldown;
            _clock = clock;

            // 初期値を反映
            _view.SetHp(stats.CurrentHp.CurrentValue, stats.MaxHp);
            _view.SetCoins(stats.Coins.CurrentValue);

            _hpSub = stats.CurrentHp.Pairwise().Subscribe(pair =>
            {
                _view.SetHp(pair.Current, stats.MaxHp);
                if (pair.Current != pair.Previous) _view.PunchHp();
            });
            _coinsSub = stats.Coins.Pairwise().Subscribe(pair =>
            {
                _view.SetCoins(pair.Current);
                if (pair.Current > pair.Previous) _view.PunchCoin();
            });
            _upgradesSub = build.AcquiredUpgrades.Subscribe(
                upgrades => _view.SetAcquiredUpgrades(upgrades, iconMap));
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

            float breakMax = _build.BreakCooldown;
            float breakRemaining = _cooldown.BreakBombRemaining.CurrentValue;
            _view.SetBreakCooldown(breakMax > 0f ? breakRemaining / breakMax : 0f);
        }

        public void Dispose()
        {
            _hpSub.Dispose();
            _coinsSub.Dispose();
            _upgradesSub.Dispose();
        }
    }
}
