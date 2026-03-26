using UnityEngine;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Bombs.Presentation
{
    [CreateAssetMenu(fileName = "BombSpriteConfig", menuName = "FloorBreaker/Bombs/BombSpriteConfig")]
    public sealed class BombSpriteConfig : ScriptableObject
    {
        [Header("Bomb Sprites")]
        [SerializeField] private Sprite _breakBombSprite;
        [SerializeField] private Sprite _fireBombSprite;

        [Header("Bomb Colors")]
        [SerializeField] private Color _breakBombTint = new Color(0.19f, 0.50f, 0.75f, 1f); // #3080C0
        [SerializeField] private Color _fireBombTint = new Color(0.94f, 0.50f, 0.19f, 1f); // #F08030

        [Header("Scale / Sorting")]
        [SerializeField] private float _bombScale = 0.18f;
        [SerializeField] private int _bombSortingOrder = 15;

        [Header("Trail")]
        [SerializeField] private float _trailStartWidth = 0.15f;
        [SerializeField] private float _trailEndWidth = 0f;
        [SerializeField] private float _trailDuration = 0.15f;
        [SerializeField] private Gradient _breakTrailGradient;
        [SerializeField] private Gradient _fireTrailGradient;

        [Header("Explosion VFX")]
        [SerializeField] private GameObject _explosionVfxPrefabFire;
        [SerializeField] private GameObject _explosionVfxPrefabBreak;
        [SerializeField] private float _explosionVfxScale = 0.8f;
        [SerializeField] private float _explosionVfxDuration = 0.8f;

        [Header("Impact Flash")]
        [SerializeField] private float _impactFlashDuration = 0.3f;
        [SerializeField] private float _impactFlashAlpha = 0.5f;
        [SerializeField] private Color _breakImpactColor = new Color(0.19f, 0.50f, 0.75f, 0.5f);
        [SerializeField] private Color _fireImpactColor = new Color(0.94f, 0.50f, 0.19f, 0.5f);

        // --- Accessors ---

        public Sprite GetBombSprite(BombType type) =>
            type == BombType.Break ? _breakBombSprite : _fireBombSprite;

        public Color GetBombTint(BombType type) =>
            type == BombType.Break ? _breakBombTint : _fireBombTint;

        public Gradient GetTrailGradient(BombType type) =>
            type == BombType.Break ? _breakTrailGradient : _fireTrailGradient;

        public GameObject GetExplosionPrefab(BombType type) =>
            type == BombType.Break ? _explosionVfxPrefabBreak : _explosionVfxPrefabFire;

        public Color GetImpactColor(BombType type) =>
            type == BombType.Break ? _breakImpactColor : _fireImpactColor;

        public float BombScale => _bombScale;
        public int BombSortingOrder => _bombSortingOrder;
        public float TrailStartWidth => _trailStartWidth;
        public float TrailEndWidth => _trailEndWidth;
        public float TrailDuration => _trailDuration;
        public float ExplosionVfxScale => _explosionVfxScale;
        public float ExplosionVfxDuration => _explosionVfxDuration;
        public float ImpactFlashDuration => _impactFlashDuration;
        public float ImpactFlashAlpha => _impactFlashAlpha;
    }
}
