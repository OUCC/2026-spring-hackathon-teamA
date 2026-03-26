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
        private SlimeType _slimeType;

        public SpriteRenderer Renderer => _renderer;
        public SlimeId SlimeId => _slimeId;
        public SlimeType SlimeType => _slimeType;

        public void Initialize(SlimeId id, SlimeType type, SpriteRenderer renderer, SlimeSpriteConfig config)
        {
            _slimeId = id;
            _slimeType = type;
            _renderer = renderer;
            _renderer.color = config.GetTypeTint(type);
            _renderer.sortingOrder = config.BaseSortingOrder;

            var sprite = config.GetSprite(Direction8.S);
            if (sprite != null)
            {
                _renderer.sprite = sprite;
            }
        }

        public void SetPositionImmediate(Vector3 worldPos)
        {
            transform.position = worldPos;
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
