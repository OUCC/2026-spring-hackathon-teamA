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

        public void ApplyState(TileState state, TileSpriteConfig config)
        {
            switch (state)
            {
                case TileState.Normal:
                    _renderer.sprite = config.NormalSprite;
                    _renderer.color = config.NormalColor;
                    _renderer.enabled = true;
                    transform.localScale = Vector3.one;
                    transform.position = _basePosition;
                    break;

                case TileState.Wall:
                    _renderer.sprite = config.WallSprite;
                    _renderer.color = config.WallColor;
                    _renderer.enabled = true;
                    transform.localScale = Vector3.one;
                    transform.position = _basePosition;
                    break;

                case TileState.OnFire:
                    _renderer.sprite = config.BurningSprite;
                    _renderer.color = config.BurningTint;
                    _renderer.enabled = true;
                    transform.localScale = Vector3.one;
                    transform.position = _basePosition;
                    break;

                case TileState.Collapsing:
                    _renderer.sprite = config.CollapsingSprite;
                    _renderer.color = config.CollapsingTint;
                    _renderer.enabled = true;
                    break;

                case TileState.Collapsed:
                    _renderer.sprite = config.CollapsedSprite;
                    _renderer.color = config.CollapsedColor;
                    _renderer.enabled = true;
                    transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                    transform.position = _basePosition;
                    break;

                case TileState.PermanentlyDestroyed:
                    _renderer.sprite = config.DestroyedSprite;
                    _renderer.color = config.DestroyedColor;
                    _renderer.enabled = false;
                    transform.localScale = Vector3.one;
                    transform.position = _basePosition;
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
