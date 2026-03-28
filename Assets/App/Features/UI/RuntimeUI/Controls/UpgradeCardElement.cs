using UnityEngine;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Upgrades.Domain;

namespace FloorBreaker.UI.RuntimeUI.Controls
{
    /// <summary>
    /// 1 枚の強化カードを制御するラッパー。
    /// VisualTreeAsset からインスタンス化し、データ設定・状態切替を行う。
    /// </summary>
    public sealed class UpgradeCardElement
    {
        private readonly VisualElement _root;
        private readonly VisualElement _band;
        private readonly VisualElement _icon;
        private readonly Label _nameLabel;
        private readonly Label _descLabel;
        private readonly Label _costLabel;

        public VisualElement Root => _root;

        public UpgradeCardElement(VisualTreeAsset cardTemplate)
        {
            _root = cardTemplate.Instantiate();
            // テンプレートの TemplateContainer 内のルートを取得
            var card = _root.Q(className: "card") ?? _root;
            _band = card.Q("CategoryBand");
            _icon = card.Q("CardIcon");
            _nameLabel = card.Q<Label>("CardName");
            _descLabel = card.Q<Label>("CardDesc");
            _costLabel = card.Q<Label>("CardCost");
            _root = card;
        }

        public void SetData(UpgradeDefinition def, UpgradeIconMap iconMap = null)
        {
            _nameLabel.text = def.DisplayName;
            _descLabel.text = GetDescription(def.Id);
            _costLabel.text = def.Cost.ToString();

            // カテゴリ色帯
            _band.RemoveFromClassList("card__band--fire");
            _band.RemoveFromClassList("card__band--break");
            _band.RemoveFromClassList("card__band--general");
            _band.AddToClassList(GetBandClass(def.Id));

            // アイコン
            if (_icon != null)
            {
                _icon.RemoveFromClassList("card__icon--fire");
                _icon.RemoveFromClassList("card__icon--break");
                _icon.RemoveFromClassList("card__icon--general");

                var tex = iconMap?.Get(def.Id);
                if (tex != null)
                    _icon.style.backgroundImage = new StyleBackground(tex);
                _icon.AddToClassList(GetIconTintClass(def.Id));
            }

            // レアリティ装飾
            _root.RemoveFromClassList("card--common");
            _root.RemoveFromClassList("card--rare");
            _root.RemoveFromClassList("card--epic");
            _root.AddToClassList(GetRarityClass(def.Rarity));
        }

        public void SetSelected(bool selected)
        {
            _root.EnableInClassList("card--selected", selected);
        }

        public void SetLocked(bool locked)
        {
            _root.EnableInClassList("card--locked", locked);
        }

        public void SetDone(bool done)
        {
            _root.EnableInClassList("card--done", done);
        }

        private static string GetBandClass(UpgradeId id)
        {
            return UpgradeIdDisplayHelper.GetBandClass(id);
        }

        private static string GetIconTintClass(UpgradeId id)
        {
            return UpgradeIdDisplayHelper.GetCategory(id) switch
            {
                UpgradeIdDisplayHelper.UpgradeCategory.Fire => "card__icon--fire",
                UpgradeIdDisplayHelper.UpgradeCategory.Break => "card__icon--break",
                _ => "card__icon--general",
            };
        }

        private static string GetRarityClass(UpgradeRarity rarity)
        {
            return rarity switch
            {
                UpgradeRarity.Rare => "card--rare",
                UpgradeRarity.Epic => "card--epic",
                _ => "card--common",
            };
        }

        private static string GetDescription(UpgradeId id)
        {
            return id switch
            {
                UpgradeId.FireFlightRange => "飛距離+2マス",
                UpgradeId.FireEffectRange => "効果範囲+1マス",
                UpgradeId.FireDamage => "接触ダメージ+1",
                UpgradeId.FireDuration => "炎の持続+2秒",
                UpgradeId.FireWallPenetration => "炎が壁を貫通",
                UpgradeId.FireCooldown => "CD-0.3秒(下限0.5秒)",
                UpgradeId.FireBombPenetration => "炎ボムが障害物を貫通",
                UpgradeId.BreakFlightRange => "飛距離+2マス",
                UpgradeId.BreakEffectRange => "効果範囲+1マス",
                UpgradeId.BreakDamage => "崩落ダメージ+1",
                UpgradeId.BreakCollapseTime => "崩落持続+2秒",
                UpgradeId.BreakCooldown => "CD-0.5秒(下限1.0秒)",
                UpgradeId.BreakBombPenetration => "ブレークボムが障害物を貫通",
                UpgradeId.MoveSpeed => "移動速度+0.2(上限3.0)",
                UpgradeId.HpRecovery => "HP3回復",
                UpgradeId.FireShield => "炎ダメージ無効(1フェーズ)",
                UpgradeId.Levitation => "空中浮遊・崩落無効(1フェーズ)",
                UpgradeId.Dash => "2マス瞬間移動(障害無視)",
                UpgradeId.DualShot => "左右に同時発射",
                _ => "",
            };
        }
    }
}
