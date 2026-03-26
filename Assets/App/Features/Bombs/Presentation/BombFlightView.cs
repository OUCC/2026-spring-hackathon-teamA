using UnityEngine;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Bombs.Presentation
{
    /// <summary>
    /// ボム飛行弾の薄い View。SpriteRenderer + TrailRenderer を保持し、表示切替のみ担当。
    /// R3 購読やロジックは持たない。
    /// </summary>
    public sealed class BombFlightView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private TrailRenderer _trail;
        private PlayerId _owner;
        private BombType _type;

        public SpriteRenderer Renderer => _renderer;
        public TrailRenderer Trail => _trail;
        public PlayerId Owner => _owner;
        public BombType Type => _type;

        public void Initialize(PlayerId owner, BombType type,
            SpriteRenderer renderer, TrailRenderer trail, BombSpriteConfig config)
        {
            _owner = owner;
            _type = type;
            _renderer = renderer;
            _trail = trail;

            _renderer.sprite = config.GetBombSprite(type);
            _renderer.color = config.GetBombTint(type);
            _renderer.sortingOrder = config.BombSortingOrder;

            // Trail setup
            _trail.sortingOrder = config.BombSortingOrder - 1;
            _trail.widthMultiplier = 1f;
            _trail.startWidth = config.TrailStartWidth;
            _trail.endWidth = config.TrailEndWidth;
            _trail.time = config.TrailDuration;
            _trail.numCornerVertices = 0;
            _trail.numCapVertices = 0;
            _trail.generateLightingData = false;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;

            var gradient = config.GetTrailGradient(type);
            if (gradient != null)
            {
                _trail.colorGradient = gradient;
            }

            // Use default sprite material for trail
            _trail.material = _renderer.material;
        }

        public void Reinitialize(PlayerId owner, BombType type, BombSpriteConfig config)
        {
            _owner = owner;
            _type = type;
            _renderer.sprite = config.GetBombSprite(type);
            _renderer.color = config.GetBombTint(type);

            var gradient = config.GetTrailGradient(type);
            if (gradient != null)
            {
                _trail.colorGradient = gradient;
            }
        }

        public void SetPositionImmediate(Vector3 worldPos)
        {
            transform.position = worldPos;
        }

        public void Show()
        {
            _renderer.enabled = true;
            _trail.Clear();
            _trail.emitting = true;
        }

        public void Hide()
        {
            _renderer.enabled = false;
            _trail.emitting = false;
            _trail.Clear();
        }
    }
}
