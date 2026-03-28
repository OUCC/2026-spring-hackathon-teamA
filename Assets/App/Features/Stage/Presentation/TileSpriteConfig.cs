using UnityEngine;

namespace FloorBreaker.Stage.Presentation
{
    [CreateAssetMenu(fileName = "TileSpriteConfig", menuName = "FloorBreaker/Stage/TileSpriteConfig")]
    public sealed class TileSpriteConfig : ScriptableObject
    {
        [Header("Sprites")]
        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _wallSprite;
        [SerializeField] private Sprite _burningSprite;
        [SerializeField] private Sprite _collapsingSprite;
        [SerializeField] private Sprite _collapsedSprite;
        [SerializeField] private Sprite _destroyedSprite;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new Color(0.83f, 0.77f, 0.66f, 1f);     // #D4C4A8
        [SerializeField] private Color _wallColor = new Color(0.42f, 0.36f, 0.29f, 1f);        // #6B5B4A
        [SerializeField] private Color _burningTint = new Color(1f, 0.53f, 0.27f, 1f);         // #FF8844
        [SerializeField] private Color _burningPulseBright = new Color(1f, 0.75f, 0.3f, 1f);   // #FFBF4D
        [SerializeField] private Color _burningFadeColor = new Color(1f, 0.9f, 0.3f, 0.6f);   // 消火間際の黄色（薄い）
        [SerializeField] private Color _collapsingTint = new Color(0.67f, 0.60f, 0.47f, 1f);   // #AA9977
        [SerializeField] private Color _collapsedColor = new Color(0.10f, 0.08f, 0.13f, 1f);   // #1A1420
        [SerializeField] private Color _destroyedColor = new Color(0f, 0f, 0f, 0f);

        [Header("New Tile Type Colors (placeholder)")]
        [SerializeField] private Color _bedrockColor = new Color(0.15f, 0.12f, 0.1f, 1f);
        [SerializeField] private Color _gasColor = new Color(0.4f, 0.8f, 0.4f, 0.7f);
        [SerializeField] private Color _warpColor = new Color(0.6f, 0.3f, 0.8f, 1f);
        [SerializeField] private Color _eternalFireTint = new Color(0.267f, 0.533f, 1f, 1f);  // #4488FF

        [Header("VFX Prefabs")]
        [SerializeField] private GameObject _fireVfxPrefab;
        [SerializeField] private GameObject _collapseDebrisPrefab;

        [Header("Animation")]
        [SerializeField] private float _collapseAnimDuration = 0.45f;
        [SerializeField] private float _recoveryAnimDuration = 0.35f;
        [SerializeField] private float _firePulseSpeed = 2f;
        [SerializeField] [Range(0f, 1f)] private float _shrinkWaveStagger = 0.7f;

        // Sprites
        public Sprite NormalSprite => _normalSprite;
        public Sprite WallSprite => _wallSprite;
        public Sprite BurningSprite => _burningSprite;
        public Sprite CollapsingSprite => _collapsingSprite;
        public Sprite CollapsedSprite => _collapsedSprite;
        public Sprite DestroyedSprite => _destroyedSprite;

        // Colors
        public Color NormalColor => _normalColor;
        public Color WallColor => _wallColor;
        public Color BurningTint => _burningTint;
        public Color BurningPulseBright => _burningPulseBright;
        public Color BurningFadeColor => _burningFadeColor;
        public Color CollapsingTint => _collapsingTint;
        public Color CollapsedColor => _collapsedColor;
        public Color DestroyedColor => _destroyedColor;
        public Color BedrockColor => _bedrockColor;
        public Color GasColor => _gasColor;
        public Color WarpColor => _warpColor;
        public Color EternalFireTint => _eternalFireTint;

        // VFX
        public GameObject FireVfxPrefab => _fireVfxPrefab;
        public GameObject CollapseDebrisPrefab => _collapseDebrisPrefab;

        // Animation
        public float CollapseAnimDuration => _collapseAnimDuration;
        public float RecoveryAnimDuration => _recoveryAnimDuration;
        public float FirePulseSpeed => _firePulseSpeed;
        public float ShrinkWaveStagger => _shrinkWaveStagger;
    }
}
