using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Slimes.Presentation
{
    /// <summary>
    /// スライムの薄い View。SpriteRenderer を保持し、スプライト切替のみ担当。
    /// R3 購読やロジックは持たない。
    /// </summary>
    public sealed class SlimeView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private SlimeId _slimeId;
        public SpriteRenderer Renderer => _renderer;
        public SlimeId SlimeId => _slimeId;

        public void Initialize(SlimeId id, SlimeType type, SpriteRenderer renderer, SlimeSpriteConfig config)
        {
            _slimeId = id;
            _renderer = renderer;
            _renderer.sortingOrder = config.BaseSortingOrder;

            // HSV シフトでスプライト色を変更（乗算だとシアン基色で金/赤が崩れるため）
            var mat = _renderer.material;
            float hsvShift = config.GetHsvShift(type);
            if (mat != null && mat.HasProperty("_HsvShift"))
            {
                mat.EnableKeyword("HSV_ON");
                mat.SetFloat("_HsvShift", hsvShift);
                mat.SetFloat("_HsvSaturation", config.GetHsvSaturation(type));
                mat.SetFloat("_HsvBright", config.GetHsvBright(type));
            }
            else
            {
                // フォールバック: AllIn1SpriteShader が無い場合は従来の乗算 tint
                _renderer.color = config.GetTypeTint(type);
            }

            var sprite = config.GetSprite(Direction8.S);
            if (sprite != null)
            {
                _renderer.sprite = sprite;
            }
        }

        public void SetDirection(Direction8 dir, SlimeSpriteConfig config)
        {
            var sprite = config.GetSprite(dir);
            if (sprite != null)
            {
                _renderer.sprite = sprite;
            }
        }
    }
}
