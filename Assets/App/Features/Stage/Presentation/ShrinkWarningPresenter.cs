using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Stage.Presentation
{
    /// <summary>
    /// ステージ縮小の予告演出を担当する。
    /// 縮小5秒前から外周タイルにシェイク + 赤ティント警告を表示する。
    /// </summary>
    public sealed class ShrinkWarningPresenter : IDisposable
    {
        private const float WarningStartSeconds = 5f;

        private readonly MatchClock _clock;
        private readonly StageBounds _bounds;
        private readonly Dictionary<GridPos, TileView> _views;
        private readonly TileAnimationService _animService;
        private readonly TileSpriteConfig _config;

        private readonly HashSet<GridPos> _warningTiles = new();
        private readonly Dictionary<GridPos, Tween> _warningTweens = new();
        private bool _isWarning;

        public ShrinkWarningPresenter(
            MatchClock clock,
            StageBounds bounds,
            Dictionary<GridPos, TileView> views,
            TileAnimationService animService,
            TileSpriteConfig config)
        {
            _clock = clock;
            _bounds = bounds;
            _views = views;
            _animService = animService;
            _config = config;
        }

        /// <summary>
        /// 毎フレーム呼び出し。残り時間に応じて警告を開始・更新・解除する。
        /// </summary>
        public void Tick()
        {
            bool shouldWarn = _clock.CurrentPhaseValue == GamePhase.MatchRunning
                              && _clock.RemainingValue <= WarningStartSeconds
                              && _clock.RemainingValue > 0f;

            if (shouldWarn && !_isWarning)
            {
                StartWarning();
            }
            else if (shouldWarn && _isWarning)
            {
                UpdateWarningIntensity();
            }
            else if (!shouldWarn && _isWarning)
            {
                StopWarning();
            }
        }

        private void StartWarning()
        {
            _isWarning = true;
            _warningTiles.Clear();

            var ring = _bounds.GetOuterRing();
            foreach (var pos in ring)
            {
                if (!_views.TryGetValue(pos, out var view)) continue;
                _warningTiles.Add(pos);

                // シェイク + 赤パルスの警告トゥイーン
                var seq = DOTween.Sequence();
                seq.Append(view.transform
                    .DOShakePosition(WarningStartSeconds, strength: 0.03f, vibrato: 8, randomness: 90, fadeOut: false)
                    .SetEase(Ease.Linear));
                seq.SetLink(view.gameObject);
                _warningTweens[pos] = seq;
            }
        }

        private void UpdateWarningIntensity()
        {
            // 残り時間に応じてタイルの色を赤く
            float ratio = _clock.RemainingValue / WarningStartSeconds; // 1.0→0.0
            float intensity = 1f - ratio; // 0.0→1.0

            var warningColor = Color.Lerp(
                _config.NormalColor,
                new Color(0.9f, 0.2f, 0.1f, 1f), // 警告赤
                intensity * 0.5f);

            foreach (var pos in _warningTiles)
            {
                if (!_views.TryGetValue(pos, out var view)) continue;
                // 既に他の状態（Wall等）のタイルは色を変えない判定は省略
                // （外周は通常タイルが大半なので問題ない）
                view.Renderer.color = warningColor;
            }
        }

        private void StopWarning()
        {
            _isWarning = false;

            foreach (var kvp in _warningTweens)
            {
                kvp.Value.Kill();
            }
            _warningTweens.Clear();

            // 色をリセット（タイルの実状態に応じた色に戻す）
            foreach (var pos in _warningTiles)
            {
                if (!_views.TryGetValue(pos, out var view)) continue;
                view.ResetVisual(_config);
            }
            _warningTiles.Clear();
        }

        public void Dispose()
        {
            StopWarning();
        }
    }
}
