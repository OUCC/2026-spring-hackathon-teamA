using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Stage.Presentation;

namespace FloorBreaker.Bombs.Presentation
{
    /// <summary>
    /// ボムの全 DOTween アニメーションを一元管理する。
    /// 飛行 tween を PlayerId 別に追跡、インパクトフラッシュは別リストで管理。
    /// </summary>
    public sealed class BombAnimationService : IDisposable
    {
        private readonly BombSpriteConfig _config;
        private readonly Dictionary<PlayerId, Tween> _flightTweens = new();
        private readonly List<Tween> _impactTweens = new();

        public BombAnimationService(BombSpriteConfig config)
        {
            _config = config;
        }

        // ─── Flight ───────────────────────────────────────────────

        public void PlayFlight(BombFlightView view, Vector3 start, Vector3 end, float duration)
        {
            KillFlight(view.Owner);
            view.SetPositionImmediate(start);
            var tween = view.transform
                .DOMove(end, duration)
                .SetEase(Ease.Linear)
                .SetLink(view.gameObject);
            _flightTweens[view.Owner] = tween;
        }

        public void KillFlight(PlayerId owner)
        {
            if (_flightTweens.TryGetValue(owner, out var tween))
            {
                tween.Kill();
                _flightTweens.Remove(owner);
            }
        }

        // ─── Impact Flash ─────────────────────────────────────────

        public void PlayImpactFlash(TileView tileView, Color flashColor, float duration, float peakAlpha)
        {
            var renderer = tileView.Renderer;
            var originalColor = renderer.color;
            var targetColor = Color.Lerp(originalColor, flashColor, peakAlpha);

            var halfDur = duration * 0.5f;
            var seq = DOTween.Sequence();
            seq.Append(DOTween.To(
                () => renderer.color, c => renderer.color = c,
                targetColor, halfDur).SetEase(Ease.OutQuad));
            seq.Append(DOTween.To(
                () => renderer.color, c => renderer.color = c,
                originalColor, halfDur).SetEase(Ease.InQuad));
            seq.SetLink(tileView.gameObject);
            _impactTweens.Add(seq);
        }

        // ─── Cleanup ──────────────────────────────────────────────

        public void Dispose()
        {
            foreach (var t in _flightTweens.Values) t.Kill();
            _flightTweens.Clear();
            foreach (var t in _impactTweens) t.Kill();
            _impactTweens.Clear();
        }
    }
}
