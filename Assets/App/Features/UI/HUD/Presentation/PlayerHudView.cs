using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.UI.RuntimeUI.Controls;

namespace FloorBreaker.UI.HUD.Presentation
{
    /// <summary>
    /// 1 プレイヤー分の HUD VisualElement ラッパー。
    /// 横1行バー: タイマー | HP | FIRE列 | BREAK列 | コイン
    /// </summary>
    public sealed class PlayerHudView
    {
        private readonly Label _timerLabel;
        private readonly Label _hpLabel;
        private readonly VisualElement _hpFill;
        private readonly Label _coinLabel;
        private readonly VisualElement _fireCdFill;
        private readonly VisualElement _breakCdFill;
        private readonly Label _fireKeyLabel;
        private readonly Label _breakKeyLabel;

        // ボムステータス
        private readonly VisualElement _fireStatsRow;
        private readonly VisualElement _fireEffectGrid;
        private readonly VisualElement _fireFlightDots;
        private readonly VisualElement _fireAbilityIcons;
        private readonly VisualElement _breakStatsRow;
        private readonly VisualElement _breakEffectGrid;
        private readonly VisualElement _breakFlightDots;
        private readonly VisualElement _breakAbilityIcons;

        // 前回値（変化検出用）
        private int _prevFireFlight, _prevFireEffect, _prevBreakFlight, _prevBreakEffect;
        private bool _isFirstBombStats = true;

        public PlayerHudView(VisualElement hudRoot)
        {
            _timerLabel = hudRoot.Q<Label>("TimerLabel");
            _hpLabel = hudRoot.Q<Label>("HpLabel");
            _hpFill = hudRoot.Q("HpFill");
            _coinLabel = hudRoot.Q<Label>("CoinLabel");
            _fireCdFill = hudRoot.Q("FireCdFill");
            _breakCdFill = hudRoot.Q("BreakCdFill");
            _fireKeyLabel = hudRoot.Q<Label>("FireKeyLabel");
            _breakKeyLabel = hudRoot.Q<Label>("BreakKeyLabel");

            _fireStatsRow = hudRoot.Q("FireStatsRow");
            _fireEffectGrid = hudRoot.Q("FireEffectGrid");
            _fireFlightDots = hudRoot.Q("FireFlightDots");
            _fireAbilityIcons = hudRoot.Q("FireAbilityIcons");
            _breakStatsRow = hudRoot.Q("BreakStatsRow");
            _breakEffectGrid = hudRoot.Q("BreakEffectGrid");
            _breakFlightDots = hudRoot.Q("BreakFlightDots");
            _breakAbilityIcons = hudRoot.Q("BreakAbilityIcons");
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
            _fireCdFill.style.width = Length.Percent((1f - ratio) * 100f);
        }

        public void SetBreakCooldown(float ratio)
        {
            _breakCdFill.style.width = Length.Percent((1f - ratio) * 100f);
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

        public void SetFireKeyLabel(string text)
        {
            if (_fireKeyLabel != null) _fireKeyLabel.text = text;
        }

        public void SetBreakKeyLabel(string text)
        {
            if (_breakKeyLabel != null) _breakKeyLabel.text = text;
        }

        /// <summary>ボム飛距離と効果範囲を更新する。変化時にパルスアニメーション。</summary>
        public void SetBombStats(int fireFlightRange, int fireEffectRange,
            int breakFlightRange, int breakEffectRange)
        {
            bool fireChanged = fireFlightRange != _prevFireFlight || fireEffectRange != _prevFireEffect;
            bool breakChanged = breakFlightRange != _prevBreakFlight || breakEffectRange != _prevBreakEffect;

            BuildEffectGrid(_fireEffectGrid, fireEffectRange, "fire");
            BuildFlightDots(_fireFlightDots, fireFlightRange, "fire");
            BuildEffectGrid(_breakEffectGrid, breakEffectRange, "break");
            BuildFlightDots(_breakFlightDots, breakFlightRange, "break");

            // 初回は無アニメ、以降は変化した行だけパルス
            if (!_isFirstBombStats)
            {
                if (fireChanged) PulseElement(_fireStatsRow);
                if (breakChanged) PulseElement(_breakStatsRow);
            }
            _isFirstBombStats = false;
            _prevFireFlight = fireFlightRange;
            _prevFireEffect = fireEffectRange;
            _prevBreakFlight = breakFlightRange;
            _prevBreakEffect = breakEffectRange;
        }

        private static void PulseElement(VisualElement el)
        {
            if (el == null) return;
            el.AddToClassList("hud__bomb-stats-row--pulse");
            el.schedule.Execute(() =>
                el.RemoveFromClassList("hud__bomb-stats-row--pulse")).StartingIn(150);
        }

        // 能力状態の前回値（ポップイン検出用）
        private bool _prevFireShield, _prevFireWallPen, _prevFireBombPen;
        private bool _prevBreakBombPen, _prevLevitation, _prevDash, _prevDualShot;
        private bool _isFirstAbilities = true;

        /// <summary>FIRE 関連の能力アイコンを更新する。</summary>
        public void SetFireAbilities(bool hasFireShield, bool hasFireWallPen,
            bool hasFireBombPen, UpgradeIconMap iconMap)
        {
            if (_fireAbilityIcons == null) return;
            _fireAbilityIcons.Clear();
            bool anim = !_isFirstAbilities;
            if (hasFireShield) AddAbilityIcon(_fireAbilityIcons, "防火", "fire", UpgradeId.FireShield, iconMap, anim && !_prevFireShield);
            if (hasFireWallPen) AddAbilityIcon(_fireAbilityIcons, "壁貫", "fire", UpgradeId.FireWallPenetration, iconMap, anim && !_prevFireWallPen);
            if (hasFireBombPen) AddAbilityIcon(_fireAbilityIcons, "貫通", "fire", UpgradeId.FireBombPenetration, iconMap, anim && !_prevFireBombPen);
            _prevFireShield = hasFireShield;
            _prevFireWallPen = hasFireWallPen;
            _prevFireBombPen = hasFireBombPen;
        }

        /// <summary>BREAK 関連の能力アイコンを更新する。</summary>
        public void SetBreakAbilities(bool hasBreakBombPen, bool hasLevitation,
            bool hasDash, bool hasDualShot, UpgradeIconMap iconMap)
        {
            if (_breakAbilityIcons == null) return;
            _breakAbilityIcons.Clear();
            bool anim = !_isFirstAbilities;
            if (hasBreakBombPen) AddAbilityIcon(_breakAbilityIcons, "貫通", "break", UpgradeId.BreakBombPenetration, iconMap, anim && !_prevBreakBombPen);
            if (hasLevitation) AddAbilityIcon(_breakAbilityIcons, "浮遊", "general", UpgradeId.Levitation, iconMap, anim && !_prevLevitation);
            if (hasDash) AddAbilityIcon(_breakAbilityIcons, "突進", "general", UpgradeId.Dash, iconMap, anim && !_prevDash);
            if (hasDualShot) AddAbilityIcon(_breakAbilityIcons, "双射", "general", UpgradeId.DualShot, iconMap, anim && !_prevDualShot);
            _prevBreakBombPen = hasBreakBombPen;
            _prevLevitation = hasLevitation;
            _prevDash = hasDash;
            _prevDualShot = hasDualShot;
            _isFirstAbilities = false;
        }

        private static void AddAbilityIcon(VisualElement container, string label,
            string category, UpgradeId id, UpgradeIconMap iconMap, bool animate = false)
        {
            var icon = new VisualElement();
            icon.AddToClassList("hud__ability-icon");
            icon.AddToClassList($"hud__ability-icon--{category}");

            var tex = iconMap?.Get(id);
            if (tex != null)
            {
                icon.style.backgroundImage = new StyleBackground(tex);
            }
            else
            {
                var lbl = new Label(label);
                lbl.AddToClassList("hud__ability-label");
                icon.Add(lbl);
            }

            // ポップインアニメーション
            if (animate)
            {
                icon.style.scale = new Scale(Vector3.zero);
                icon.schedule.Execute(() =>
                    icon.AddToClassList("hud__ability-icon--pop-in")).StartingIn(16);
            }

            container.Add(icon);
        }

        /// <summary>
        /// 効果範囲グリッド: 縦3行固定、横 = range*2+1。
        /// 中心行の横幅が実際の範囲を表す。
        /// </summary>
        private static void BuildEffectGrid(VisualElement container, int range, string type)
        {
            if (container == null) return;
            container.Clear();

            int cols = range * 2 + 1;
            int center = range;

            // 各行を明示的に VisualElement(row) で包んで改行を防ぐ
            for (int r = 0; r < 3; r++)
            {
                var rowEl = new VisualElement();
                rowEl.style.flexDirection = FlexDirection.Row;
                rowEl.style.justifyContent = Justify.Center;

                for (int col = 0; col < cols; col++)
                {
                    var cell = new VisualElement();
                    cell.AddToClassList("hud__effect-cell");

                    bool isCenter = r == 1 && col == center;
                    bool isActive = r == 1 || col == center;

                    if (isCenter)
                        cell.AddToClassList("hud__effect-cell--center");
                    else if (isActive)
                        cell.AddToClassList($"hud__effect-cell--active-{type}");

                    rowEl.Add(cell);
                }

                container.Add(rowEl);
            }
        }

        private const int BaseFlightRange = 3;

        /// <summary>
        /// 飛距離をドットで表現。基本3マスは薄色、アップグレード分は明色。
        /// </summary>
        private static void BuildFlightDots(VisualElement container, int range, string type)
        {
            if (container == null) return;
            container.Clear();

            for (int i = 0; i < range; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("hud__flight-dot");
                if (i < BaseFlightRange)
                    dot.AddToClassList($"hud__flight-dot--{type}-base");
                else
                    dot.AddToClassList($"hud__flight-dot--{type}-upgraded");
                container.Add(dot);
            }

            var arrow = new Label("▶");
            arrow.AddToClassList("hud__flight-arrow");
            arrow.AddToClassList($"hud__flight-arrow--{type}");
            container.Add(arrow);
        }
    }
}
