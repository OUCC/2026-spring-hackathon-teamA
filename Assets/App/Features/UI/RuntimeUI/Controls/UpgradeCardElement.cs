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
            _nameLabel = card.Q<Label>("CardName");
            _descLabel = card.Q<Label>("CardDesc");
            _costLabel = card.Q<Label>("CardCost");
            _root = card;
        }

        public void SetData(UpgradeDefinition def)
        {
            _nameLabel.text = def.DisplayName;
            _descLabel.text = GetDescription(def.Id);
            _costLabel.text = def.Cost.ToString();

            // カテゴリ色帯
            _band.RemoveFromClassList("card__band--fire");
            _band.RemoveFromClassList("card__band--fall");
            _band.RemoveFromClassList("card__band--general");
            _band.AddToClassList(GetBandClass(def.Id));
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

        private static string GetDescription(UpgradeId id)
        {
            return id switch
            {
                UpgradeId.FireFlightRange => "最大飛行距離+1",
                UpgradeId.FireEffectRange => "効果範囲+1",
                UpgradeId.FireDamage => "接触ダメージ+1",
                UpgradeId.FireFlightDamage => "飛行中に当たり判定追加",
                UpgradeId.FireDuration => "炎の持続時間+2秒",
                UpgradeId.FireWallPenetration => "炎が壁を貫通",
                UpgradeId.FireCooldown => "クールダウン-0.3秒",
                UpgradeId.FallFlightRange => "最大飛行距離+1",
                UpgradeId.FallEffectRange => "効果範囲+1",
                UpgradeId.FallDamage => "崩落ダメージ+1",
                UpgradeId.FallFlightDamage => "飛行中に当たり判定追加",
                UpgradeId.FallCollapseTime => "崩落持続時間+2秒",
                UpgradeId.FallCooldown => "クールダウン-0.5秒",
                UpgradeId.MoveSpeed => "移動速度+0.2",
                UpgradeId.HpRecovery => "HP3回復",
                _ => "",
            };
        }
    }
}
