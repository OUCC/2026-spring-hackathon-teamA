using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Player.Presentation
{
    /// <summary>
    /// プレイヤーの薄い View。SpriteRenderer を保持し、スプライト切替のみ担当。
    /// R3 購読やロジックは持たない。
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Material _materialInstance;
        private PlayerId _playerId;
        private Direction8 _currentDirection;
        private bool _isWalkFrame;

        public SpriteRenderer Renderer => _renderer;
        /// <summary>All In 1 Sprite Shader 用のマテリアルインスタンス。</summary>
        public Material MaterialInstance => _materialInstance;
        public PlayerId PlayerId => _playerId;

        public void Initialize(PlayerId id, SpriteRenderer renderer, PlayerSpriteConfig config)
        {
            _playerId = id;
            _renderer = renderer;
            _renderer.color = config.GetPlayerTint(id);
            _renderer.sortingOrder = config.BaseSortingOrder;
            _currentDirection = Direction8.S;
            _isWalkFrame = false;
            ApplySprite(config);
            InitializeShaderEffects(id, config);
        }

        private void InitializeShaderEffects(PlayerId id, PlayerSpriteConfig config)
        {
            // マテリアルインスタンスを作成 (sharedMaterial を汚さない)
            _materialInstance = _renderer.material;

            // Hit effect プリセット (まだ非表示)
            _materialInstance.SetColor("_HitEffectColor", config.ShaderHitEffectColor);
            _materialInstance.SetFloat("_HitEffectGlow", config.ShaderHitEffectGlow);
            _materialInstance.SetFloat("_HitEffectBlend", 0f);

            // アウトライン常時表示
            _materialInstance.EnableKeyword("OUTBASE_ON");
            _materialInstance.SetColor("_OutlineColor", config.GetOutlineColor(id));
            _materialInstance.SetFloat("_OutlineAlpha", 1f);
            _materialInstance.SetFloat("_OutlineWidth", config.OutlineWidth);
            _materialInstance.SetFloat("_OutlineGlow", config.OutlineGlow);
        }

        public void SetDirection(Direction8 dir, PlayerSpriteConfig config)
        {
            _currentDirection = dir;
            ApplySprite(config);
        }

        public void SetWalkFrame(bool isWalk, PlayerSpriteConfig config)
        {
            if (_isWalkFrame == isWalk) return;
            _isWalkFrame = isWalk;
            ApplySprite(config);
        }

        private void ApplySprite(PlayerSpriteConfig config)
        {
            var sprite = _isWalkFrame
                ? config.GetWalkSprite(_currentDirection)
                : config.GetStandSprite(_currentDirection);
            if (sprite != null)
            {
                _renderer.sprite = sprite;
            }
        }
    }
}
