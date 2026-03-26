using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Player.Presentation
{
    [CreateAssetMenu(fileName = "PlayerSpriteConfig", menuName = "FloorBreaker/Player/PlayerSpriteConfig")]
    public sealed class PlayerSpriteConfig : ScriptableObject
    {
        [Header("Stand Sprites — Direction8 順: N, NE, E, SE, S, SW, W, NW")]
        [SerializeField] private Sprite[] _standSprites = new Sprite[8];

        [Header("Walk Sprites — Direction8 順")]
        [SerializeField] private Sprite[] _walkSprites = new Sprite[8];

        [Header("Player Tints")]
        [SerializeField] private Color _player1Tint = new Color(0.6f, 0.85f, 1f, 1f);
        [SerializeField] private Color _player2Tint = new Color(1f, 0.55f, 0.55f, 1f);

        [Header("Movement")]
        [SerializeField] private float _moveDuration = 0.12f;
        [SerializeField] private float _forcedMoveDuration = 0.4f;
        [SerializeField] private float _forcedMoveArcHeight = 0.6f;
        [SerializeField] private float _walkFrameInterval = 0.08f;
        [SerializeField] private float _baseMoveInterval = 0.2f;

        [Header("Damage")]
        [SerializeField] private float _hitFlashDuration = 0.08f;
        [SerializeField] private int _hitFlashCount = 3;
        [SerializeField] private Color _hitFlashColor = Color.white;

        [Header("Invulnerability")]
        [SerializeField] private float _blinkInterval = 0.08f;
        [SerializeField] private float _blinkAlphaMin = 0.25f;

        [Header("Death")]
        [SerializeField] private float _deathDuration = 0.6f;
        [SerializeField] private float _deathShrinkScale = 0.2f;

        [Header("Scale")]
        [SerializeField] private float _playerScale = 0.22f;

        [Header("Sorting")]
        [SerializeField] private int _baseSortingOrder = 10;

        // --- Accessors ---

        public Sprite GetStandSprite(Direction8 dir)
        {
            int idx = (int)dir;
            return idx >= 0 && idx < _standSprites.Length ? _standSprites[idx] : null;
        }

        public Sprite GetWalkSprite(Direction8 dir)
        {
            int idx = (int)dir;
            return idx >= 0 && idx < _walkSprites.Length ? _walkSprites[idx] : null;
        }

        public Color GetPlayerTint(PlayerId id) =>
            id == PlayerId.Player1 ? _player1Tint : _player2Tint;

        public float MoveDuration => _moveDuration;
        public float ForcedMoveDuration => _forcedMoveDuration;
        public float ForcedMoveArcHeight => _forcedMoveArcHeight;
        public float WalkFrameInterval => _walkFrameInterval;
        public float BaseMoveInterval => _baseMoveInterval;
        public float HitFlashDuration => _hitFlashDuration;
        public int HitFlashCount => _hitFlashCount;
        public Color HitFlashColor => _hitFlashColor;
        public float BlinkInterval => _blinkInterval;
        public float BlinkAlphaMin => _blinkAlphaMin;
        public float DeathDuration => _deathDuration;
        public float DeathShrinkScale => _deathShrinkScale;
        public float PlayerScale => _playerScale;
        public int BaseSortingOrder => _baseSortingOrder;
    }
}
