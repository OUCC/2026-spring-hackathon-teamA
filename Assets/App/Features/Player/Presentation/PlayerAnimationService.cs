using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Player.Presentation
{
    /// <summary>
    /// プレイヤーの全 DOTween アニメーションを一元管理する。
    /// 移動 tween とエフェクト tween を PlayerId 別に分離追跡。
    /// </summary>
    public sealed class PlayerAnimationService : IDisposable
    {
        private readonly PlayerSpriteConfig _config;
        private readonly Dictionary<PlayerId, Tween> _moveTweens = new();
        private readonly Dictionary<PlayerId, Tween> _effectTweens = new();

        public PlayerAnimationService(PlayerSpriteConfig config)
        {
            _config = config;
        }

        // ─── Movement ───────────────────────────────────────────

        public void PlayMove(PlayerView view, Vector3 target, float duration)
        {
            KillMoveTween(view.PlayerId);
            var tween = view.transform
                .DOMove(target, duration)
                .SetEase(Ease.Linear)
                .SetLink(view.gameObject);
            _moveTweens[view.PlayerId] = tween;
        }

        public void PlayForcedMove(PlayerView view, Vector3 target)
        {
            KillMoveTween(view.PlayerId);
            var start = view.transform.position;
            var mid = (start + target) * 0.5f + Vector3.up * _config.ForcedMoveArcHeight;
            var halfDur = _config.ForcedMoveDuration * 0.5f;

            var seq = DOTween.Sequence();
            seq.Append(view.transform.DOMove(mid, halfDur).SetEase(Ease.OutQuad));
            seq.Append(view.transform.DOMove(target, halfDur).SetEase(Ease.InQuad));
            seq.SetLink(view.gameObject);
            _moveTweens[view.PlayerId] = seq;
        }

        // ─── Damage Flash ───────────────────────────────────────

        public void PlayHitFlash(PlayerView view)
        {
            KillEffectTween(view.PlayerId);
            var mat = view.MaterialInstance;
            if (mat == null)
            {
                // フォールバック: シェーダー未対応の場合は従来の color flash
                PlayHitFlashFallback(view);
                return;
            }

            mat.EnableKeyword("HITEFFECT_ON");
            var flashDur = _config.HitFlashDuration * 0.5f;

            var seq = DOTween.Sequence();
            for (int i = 0; i < _config.HitFlashCount; i++)
            {
                seq.Append(DOTween.To(
                    () => mat.GetFloat("_HitEffectBlend"),
                    v => mat.SetFloat("_HitEffectBlend", v),
                    1f, flashDur));
                seq.Append(DOTween.To(
                    () => mat.GetFloat("_HitEffectBlend"),
                    v => mat.SetFloat("_HitEffectBlend", v),
                    0f, flashDur));
            }
            seq.OnComplete(() =>
            {
                mat.SetFloat("_HitEffectBlend", 0f);
                mat.DisableKeyword("HITEFFECT_ON");
            });
            seq.SetLink(view.gameObject);
            _effectTweens[view.PlayerId] = seq;
        }

        private void PlayHitFlashFallback(PlayerView view)
        {
            var renderer = view.Renderer;
            var originalColor = renderer.color;
            var flashDur = _config.HitFlashDuration * 0.5f;

            var seq = DOTween.Sequence();
            for (int i = 0; i < _config.HitFlashCount; i++)
            {
                seq.Append(DOTween.To(
                    () => renderer.color, c => renderer.color = c,
                    _config.HitFlashColor, flashDur));
                seq.Append(DOTween.To(
                    () => renderer.color, c => renderer.color = c,
                    originalColor, flashDur));
            }
            seq.SetLink(view.gameObject);
            _effectTweens[view.PlayerId] = seq;
        }

        // ─── Invulnerability Blink ──────────────────────────────

        public void StartInvulnerabilityBlink(PlayerView view)
        {
            KillEffectTween(view.PlayerId);
            var renderer = view.Renderer;
            var tween = DOTween.ToAlpha(
                    () => renderer.color, c => renderer.color = c,
                    _config.BlinkAlphaMin, _config.BlinkInterval)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(view.gameObject);
            _effectTweens[view.PlayerId] = tween;
        }

        public void StopInvulnerabilityBlink(PlayerView view, Color playerTint)
        {
            KillEffectTween(view.PlayerId);
            var c = view.Renderer.color;
            view.Renderer.color = new Color(playerTint.r, playerTint.g, playerTint.b, 1f);
        }

        // ─── Death ──────────────────────────────────────────────

        public void PlayDeath(PlayerView view)
        {
            KillMoveTween(view.PlayerId);
            KillEffectTween(view.PlayerId);
            var renderer = view.Renderer;

            var seq = DOTween.Sequence();
            seq.Append(view.transform
                .DOScale(_config.DeathShrinkScale, _config.DeathDuration)
                .SetEase(Ease.InBack));
            seq.Join(DOTween.ToAlpha(
                () => renderer.color, c => renderer.color = c,
                0f, _config.DeathDuration));
            seq.OnComplete(() => renderer.enabled = false);
            seq.SetLink(view.gameObject);
            _effectTweens[view.PlayerId] = seq;
        }

        // ─── Cleanup ────────────────────────────────────────────

        public void KillMoveTween(PlayerId id)
        {
            if (_moveTweens.TryGetValue(id, out var tween))
            {
                tween.Kill();
                _moveTweens.Remove(id);
            }
        }

        public void KillEffectTween(PlayerId id)
        {
            if (_effectTweens.TryGetValue(id, out var tween))
            {
                tween.Kill();
                _effectTweens.Remove(id);
            }
        }

        public void Dispose()
        {
            foreach (var t in _moveTweens.Values) t.Kill();
            foreach (var t in _effectTweens.Values) t.Kill();
            _moveTweens.Clear();
            _effectTweens.Clear();
        }
    }
}
