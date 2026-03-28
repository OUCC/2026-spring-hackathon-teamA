using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Stage.Presentation
{
    public sealed class TileView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Material _materialInstance;
        private GridPos _pos;
        private Vector3 _basePosition;

        public SpriteRenderer Renderer => _renderer;
        public GridPos Pos => _pos;
        public Vector3 BasePosition => _basePosition;

        /// <summary>
        /// All In 1 Sprite Shader 用のマテリアルインスタンスを遅延取得する。
        /// 900 タイル全てをインスタンス化しないよう、必要時のみ呼ぶこと。
        /// </summary>
        public Material GetOrCreateMaterialInstance()
        {
            if (_materialInstance == null)
                _materialInstance = _renderer.material; // 初回のみインスタンス化
            return _materialInstance;
        }

        public void Initialize(GridPos pos, SpriteRenderer renderer)
        {
            _pos = pos;
            _renderer = renderer;
            _basePosition = transform.position;
        }

        public void ApplyState(TileData data, TileSpriteConfig config)
        {
            // Condition が Intact 以外の場合は Condition ベースで表示
            switch (data.Condition)
            {
                case TileCondition.OnFire:
                    _renderer.sprite = config.BurningSprite;
                    _renderer.color = config.BurningTint;
                    _renderer.enabled = true;
                    transform.localScale = Vector3.one;
                    transform.position = _basePosition;
                    return;

                case TileCondition.EternalFire:
                    _renderer.sprite = config.BurningSprite;
                    _renderer.color = config.EternalFireTint;
                    _renderer.enabled = true;
                    transform.localScale = Vector3.one;
                    transform.position = _basePosition;
                    return;

                case TileCondition.Collapsing:
                    _renderer.sprite = config.CollapsingSprite;
                    _renderer.color = config.CollapsingTint;
                    _renderer.enabled = true;
                    return;

                case TileCondition.Collapsed:
                    _renderer.sprite = config.CollapsedSprite;
                    _renderer.color = config.CollapsedColor;
                    _renderer.enabled = true;
                    transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                    transform.position = _basePosition;
                    return;

                case TileCondition.PermanentlyDestroyed:
                    _renderer.sprite = config.DestroyedSprite;
                    _renderer.color = config.DestroyedColor;
                    _renderer.enabled = false;
                    transform.localScale = Vector3.one;
                    transform.position = _basePosition;
                    return;
            }

            // Condition == Intact: Type ベースでベーススプライトを表示
            _renderer.enabled = true;
            transform.localScale = Vector3.one;
            transform.position = _basePosition;

            switch (data.Type)
            {
                case TileType.Normal:
                    _renderer.sprite = config.NormalSprite;
                    _renderer.color = config.NormalColor;
                    break;

                case TileType.Wall:
                    _renderer.sprite = config.WallSprite;
                    _renderer.color = config.WallColor;
                    break;

                case TileType.Bedrock:
                    _renderer.sprite = config.WallSprite;
                    _renderer.color = config.BedrockColor;
                    break;

                case TileType.Gas:
                    _renderer.sprite = config.NormalSprite;
                    _renderer.color = config.GasColor;
                    break;

                case TileType.Warp:
                    _renderer.sprite = config.NormalSprite;
                    _renderer.color = config.WarpColor;
                    break;
            }
        }

        public void ResetVisual(TileSpriteConfig config)
        {
            _renderer.enabled = true;
            _renderer.color = config.NormalColor;
            _renderer.sprite = config.NormalSprite;
            transform.localScale = Vector3.one;
            transform.position = _basePosition;
        }
    }
}
