using System;
using System.Collections.Generic;
using UnityEngine;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Stage.Presentation
{
    public sealed class StageShrinkAnimator : IDisposable
    {
        private readonly Dictionary<GridPos, TileView> _views;
        private readonly TileAnimationService _animService;
        private readonly TileSpriteConfig _config;
        private readonly float _totalDuration;
        private readonly ICameraShakeService _cameraShake;
        private readonly IAudioService _audio;
        private readonly IDisposable _subscription;

        private readonly List<GridPos> _pendingDestroys = new();
        private int _lastCollectFrame = -1;

        public bool IsShrinkAnimating { get; private set; }

        public StageShrinkAnimator(
            StageModel model,
            Dictionary<GridPos, TileView> views,
            TileAnimationService animService,
            TileSpriteConfig config,
            float shrinkAnimDuration,
            ICameraShakeService cameraShake,
            IAudioService audio)
        {
            _views = views;
            _animService = animService;
            _config = config;
            _totalDuration = shrinkAnimDuration;
            _cameraShake = cameraShake;
            _audio = audio;

            _subscription = model.TileChanged.Subscribe(HandleTileChanged);
        }

        private void HandleTileChanged(TileChangedEvent evt)
        {
            if (evt.NewState != TileState.PermanentlyDestroyed) return;

            int currentFrame = Time.frameCount;

            // 新しいフレームの場合、前フレームの蓄積をフラッシュしてからリセット
            if (currentFrame != _lastCollectFrame && _pendingDestroys.Count > 0)
            {
                FlushPendingWave();
            }

            _pendingDestroys.Add(evt.Pos);
            _lastCollectFrame = currentFrame;

            // バッチ検出: 4タイル以上同一フレームで破壊 → ウェーブと判定
            // (個別ボムによる PermanentlyDestroyed は通常1-5タイル、外周リングは100+)
            // LateUpdate でフラッシュするために IsShrinkAnimating をプリセット
            if (_pendingDestroys.Count >= 8)
            {
                IsShrinkAnimating = true;
            }
        }

        /// <summary>
        /// MonoBehaviour の LateUpdate から呼ぶ。
        /// 同一フレームのバッチを処理する。
        /// </summary>
        public void LateUpdate()
        {
            if (_pendingDestroys.Count == 0) return;
            if (Time.frameCount != _lastCollectFrame) return;

            // フレーム末尾でフラッシュ (Subscribe は同期なのでフレーム末に呼ばれる想定)
            // → 実際には次フレームの HandleTileChanged か明示的な LateUpdate で呼ぶ
        }

        /// <summary>
        /// 蓄積された PermanentlyDestroyed タイルのウェーブアニメーションを実行する。
        /// </summary>
        public void FlushPendingWave()
        {
            if (_pendingDestroys.Count == 0) return;

            var tiles = new List<GridPos>(_pendingDestroys);
            _pendingDestroys.Clear();

            bool isWave = tiles.Count >= 8;

            if (isWave)
            {
                IsShrinkAnimating = true;
                _cameraShake.Shake(ShakeIntensity.Heavy);
                SortClockwise(tiles);

                float staggerWindow = _totalDuration * _config.ShrinkWaveStagger;
                int count = tiles.Count;

                for (int i = 0; i < count; i++)
                {
                    if (!_views.TryGetValue(tiles[i], out var view)) continue;

                    float delay = count > 1
                        ? i * staggerWindow / (count - 1)
                        : 0f;

                    _animService.PlayPermanentDestroy(view, delay);
                }

                // 全アニメーション完了後にフラグを下ろす
                float totalAnimTime = staggerWindow + _config.CollapseAnimDuration;
                DOTween_DelayedCall(totalAnimTime, () => IsShrinkAnimating = false);
            }
            else
            {
                // 少数タイルの PermanentlyDestroyed (ボム等): 即時アニメーション
                foreach (var pos in tiles)
                {
                    if (_views.TryGetValue(pos, out var view))
                    {
                        _animService.PlayPermanentDestroy(view);
                    }
                }
            }
        }

        private static void SortClockwise(List<GridPos> tiles)
        {
            if (tiles.Count == 0) return;

            // 重心を計算
            float cx = 0f, cy = 0f;
            foreach (var t in tiles)
            {
                cx += t.X;
                cy += t.Y;
            }
            cx /= tiles.Count;
            cy /= tiles.Count;

            // 角度でソート (上→右→下→左 = 時計回り)
            tiles.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.X - cx, a.Y - cy);
                float angleB = Mathf.Atan2(b.X - cx, b.Y - cy);
                return angleA.CompareTo(angleB);
            });
        }

        private void DOTween_DelayedCall(float delay, System.Action callback)
        {
            DG.Tweening.DOVirtual.DelayedCall(delay, () => callback?.Invoke());
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _pendingDestroys.Clear();
            IsShrinkAnimating = false;
        }
    }
}
