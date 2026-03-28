using System.Collections.Generic;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.UI.RuntimeUI.Controls;

namespace FloorBreaker.UI.HUD.Presentation
{
    /// <summary>
    /// 1 プレイヤー分の HUD VisualElement ラッパー。
    /// タイマー・HP・コイン・CD・取得済み強化の表示更新メソッドを提供する。
    /// </summary>
    public sealed class PlayerHudView
    {
        private readonly Label _timerLabel;
        private readonly Label _hpLabel;
        private readonly VisualElement _hpFill;
        private readonly Label _coinLabel;
        private readonly VisualElement _fireCdFill;
        private readonly VisualElement _breakCdFill;
        private readonly VisualElement _acquiredRow;

        public PlayerHudView(VisualElement hudRoot)
        {
            _timerLabel = hudRoot.Q<Label>("TimerLabel");
            _hpLabel = hudRoot.Q<Label>("HpLabel");
            _hpFill = hudRoot.Q("HpFill");
            _coinLabel = hudRoot.Q<Label>("CoinLabel");
            _fireCdFill = hudRoot.Q("FireCdFill");
            _breakCdFill = hudRoot.Q("BreakCdFill");
            _acquiredRow = hudRoot.Q("AcquiredUpgrades");
        }

        public void SetTimer(int seconds)
        {
            _timerLabel.text = seconds.ToString();
        }

        public void SetHp(int current, int max)
        {
            _hpLabel.text = current.ToString();
            float ratio = max > 0 ? (float)current / max : 0f;
            _hpFill.style.width = Length.Percent(ratio * 100f);
        }

        public void SetCoins(int coins)
        {
            _coinLabel.text = coins.ToString();
        }

        public void SetFireCooldown(float ratio)
        {
            float fillPercent = (1f - ratio) * 100f;
            _fireCdFill.style.width = Length.Percent(fillPercent);
        }

        public void SetBreakCooldown(float ratio)
        {
            float fillPercent = (1f - ratio) * 100f;
            _breakCdFill.style.width = Length.Percent(fillPercent);
        }

        public void PunchHp()
        {
            _hpLabel.AddToClassList("hud__hp-value--punch");
            _hpLabel.schedule.Execute(() =>
                _hpLabel.RemoveFromClassList("hud__hp-value--punch")).StartingIn(50);
        }

        public void PunchCoin()
        {
            _coinLabel.AddToClassList("hud__coin-value--punch");
            _coinLabel.schedule.Execute(() =>
                _coinLabel.RemoveFromClassList("hud__coin-value--punch")).StartingIn(50);
        }

        public void SetAcquiredUpgrades(IReadOnlyList<UpgradeId> upgrades)
        {
            _acquiredRow.Clear();
            foreach (var id in upgrades)
            {
                var badge = new Label(UpgradeIdDisplayHelper.GetShortLabel(id));
                badge.AddToClassList("hud__upgrade-badge");
                badge.AddToClassList(UpgradeIdDisplayHelper.GetBadgeClass(id));
                _acquiredRow.Add(badge);
            }
        }
    }
}
