using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Slimes.Presentation
{
    [CreateAssetMenu(fileName = "SlimeSpriteConfig", menuName = "FloorBreaker/Slimes/SlimeSpriteConfig")]
    public sealed class SlimeSpriteConfig : ScriptableObject
    {
        [Header("Stand Sprites — Direction8 順: N, NE, E, SE, S, SW, W, NW")]
        [SerializeField] private Sprite[] _standSprites = new Sprite[8];

        [Header("Type Tints")]
        [SerializeField] private Color _normalTint = new Color(0.38f, 0.78f, 0.50f, 1f);
        [SerializeField] private Color _goldTint = new Color(0.94f, 0.75f, 0.25f, 1f);
        [SerializeField] private Color _redTint = new Color(0.88f, 0.25f, 0.25f, 1f);

        [Header("Scale")]
        [SerializeField] private float _slimeScale = 0.2f;

        [Header("Sorting")]
        [SerializeField] private int _baseSortingOrder = 5;

        [Header("Movement")]
        [SerializeField] private float _moveDuration = 0.15f;

        [Header("Spawn Animation")]
        [SerializeField] private float _spawnPopDuration = 0.3f;
        [SerializeField] private float _spawnPopScale = 1.3f;

        [Header("Death Animation")]
        [SerializeField] private float _deathDuration = 0.4f;
        [SerializeField] private float _deathShrinkScale = 0.1f;

        [Header("Death VFX (optional)")]
        [SerializeField] private GameObject _deathVfxPrefab;

        [Header("Attack VFX (optional)")]
        [SerializeField] private GameObject _attackVfxPrefab;

        // --- Accessors ---

        public Sprite GetSprite(Direction8 dir)
        {
            int idx = (int)dir;
            return idx >= 0 && idx < _standSprites.Length ? _standSprites[idx] : null;
        }

        public Color GetTypeTint(SlimeType type)
        {
            return type switch
            {
                SlimeType.Normal => _normalTint,
                SlimeType.Gold => _goldTint,
                SlimeType.Red => _redTint,
                _ => _normalTint
            };
        }

        public float SlimeScale => _slimeScale;
        public int BaseSortingOrder => _baseSortingOrder;
        public float MoveDuration => _moveDuration;
        public float SpawnPopDuration => _spawnPopDuration;
        public float SpawnPopScale => _spawnPopScale;
        public float DeathDuration => _deathDuration;
        public float DeathShrinkScale => _deathShrinkScale;
        public GameObject DeathVfxPrefab => _deathVfxPrefab;
        public GameObject AttackVfxPrefab => _attackVfxPrefab;
    }
}
