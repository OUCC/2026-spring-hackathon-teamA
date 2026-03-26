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
        private PlayerId _playerId;
        private Direction8 _currentDirection;
        private bool _isWalkFrame;

        public SpriteRenderer Renderer => _renderer;
        public PlayerId PlayerId => _playerId;
        public Direction8 CurrentDirection => _currentDirection;

        public void Initialize(PlayerId id, SpriteRenderer renderer, PlayerSpriteConfig config)
        {
            _playerId = id;
            _renderer = renderer;
            _renderer.color = config.GetPlayerTint(id);
            _renderer.sortingOrder = config.BaseSortingOrder;
            _currentDirection = Direction8.S;
            _isWalkFrame = false;
            ApplySprite(config);
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

        public void SetPositionImmediate(Vector3 worldPos)
        {
            transform.position = worldPos;
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
