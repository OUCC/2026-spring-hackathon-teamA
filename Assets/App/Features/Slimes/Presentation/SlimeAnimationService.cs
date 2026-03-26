using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Slimes.Presentation
{
    /// <summary>
    /// スライムの全 DOTween アニメーションを一元管理する。
    /// 移動 tween とエフェクト tween を SlimeId 別に分離追跡。
    /// </summary>
    public sealed class SlimeAnimationService : IDisposable
    {
        private readonly SlimeSpriteConfig _config;
        private readonly Dictionary<SlimeId, Tween> _moveTweens = new();
        private readonly Dictionary<SlimeId, Tween> _effectTweens = new();

        // スクワッシュ＆ストレッチ パラメータ
        private const float SquashScaleX = 1.2f;  // 横に潰れる
        private const float SquashScaleY = 0.75f;  // 縦に縮む
        private const float StretchScaleX = 0.85f; // 横に細くなる
        private const float StretchScaleY = 1.15f; // 縦に伸びる
        private const float SquashDuration = 0.06f;
        private const float StretchDuration = 0.04f;

        // 攻撃アニメーション パラメータ
        private const float AttackLungeDistance = 0.3f; // 突進距離 (ワールド単位)
        private const float AttackLungeDuration = 0.08f;
        private const float AttackReturnDuration = 0.12f;

        public SlimeAnimationService(SlimeSpriteConfig config)
        {
            _config = config;
        }

        // ─── Spawn ─────────────────────────────────────────────

        public void PlaySpawn(SlimeView view)
        {
            KillEffectTween(view.SlimeId);
            var t = view.transform;
            t.localScale = Vector3.zero;

            var seq = DOTween.Sequence();
            var halfDur = _config.SpawnPopDuration * 0.5f;
            var popScale = _config.SlimeScale * _config.SpawnPopScale;
            var normalScale = _config.SlimeScale;

            seq.Append(t.DOScale(new Vector3(popScale, popScale, 1f), halfDur).SetEase(Ease.OutBack));
            seq.Append(t.DOScale(new Vector3(normalScale, normalScale, 1f), halfDur).SetEase(Ease.InOutQuad));
            seq.SetLink(view.gameObject);
            _effectTweens[view.SlimeId] = seq;
        }

        // ─── Movement (スクワッシュ＆ストレッチ付き) ───────────

        public void PlayMove(SlimeView view, Vector3 target)
        {
            KillMoveTween(view.SlimeId);
            var t = view.transform;
            var s = _config.SlimeScale;

            var seq = DOTween.Sequence();

            // 1. 予備動作: スクワッシュ (溜め)
            seq.Append(t.DOScale(
                new Vector3(s * SquashScaleX, s * SquashScaleY, 1f), SquashDuration)
                .SetEase(Ease.OutQuad));

            // 2. ストレッチしながら移動開始
            seq.Append(t.DOScale(
                new Vector3(s * StretchScaleX, s * StretchScaleY, 1f), StretchDuration)
                .SetEase(Ease.InQuad));
            seq.Join(t.DOMove(target, _config.MoveDuration).SetEase(Ease.OutQuad));

            // 3. 移動完了で元のスケールに戻る (着地バウンス)
            seq.Append(t.DOScale(
                new Vector3(s * SquashScaleX, s * SquashScaleY, 1f), 0.04f)
                .SetEase(Ease.OutQuad));
            seq.Append(t.DOScale(
                new Vector3(s, s, 1f), 0.06f)
                .SetEase(Ease.OutBack));

            seq.SetLink(view.gameObject);
            _moveTweens[view.SlimeId] = seq;
        }

        // ─── Attack (突進モーション) ──────────────────────────

        public void PlayAttack(SlimeView view, Vector3 targetDirection)
        {
            KillEffectTween(view.SlimeId);
            var t = view.transform;
            var startPos = t.position;
            var lungePos = startPos + targetDirection.normalized * AttackLungeDistance;
            var s = _config.SlimeScale;

            var seq = DOTween.Sequence();

            // 1. 予備動作: 身体を縮める (溜め)
            seq.Append(t.DOScale(
                new Vector3(s * 0.8f, s * 1.1f, 1f), 0.06f)
                .SetEase(Ease.OutQuad));

            // 2. 突進: ターゲット方向に素早く移動
            seq.Append(t.DOMove(lungePos, AttackLungeDuration).SetEase(Ease.OutQuad));
            seq.Join(t.DOScale(
                new Vector3(s * 1.3f, s * 0.7f, 1f), AttackLungeDuration)
                .SetEase(Ease.OutQuad));

            // 3. 戻り: 元の位置に戻る
            seq.Append(t.DOMove(startPos, AttackReturnDuration).SetEase(Ease.InOutQuad));
            seq.Join(t.DOScale(
                new Vector3(s, s, 1f), AttackReturnDuration)
                .SetEase(Ease.OutBack));

            seq.SetLink(view.gameObject);
            _effectTweens[view.SlimeId] = seq;
        }

        // ─── Death ─────────────────────────────────────────────

        public void PlayDeath(SlimeView view, Action onComplete)
        {
            KillMoveTween(view.SlimeId);
            KillEffectTween(view.SlimeId);
            var renderer = view.Renderer;

            var seq = DOTween.Sequence();
            seq.Append(view.transform
                .DOScale(_config.DeathShrinkScale, _config.DeathDuration)
                .SetEase(Ease.InBack));
            seq.Join(DOTween.ToAlpha(
                () => renderer.color, c => renderer.color = c,
                0f, _config.DeathDuration));
            seq.OnComplete(() => onComplete?.Invoke());
            seq.SetLink(view.gameObject);
            _effectTweens[view.SlimeId] = seq;
        }

        // ─── Cleanup ───────────────────────────────────────────

        public void KillMoveTween(SlimeId id)
        {
            if (_moveTweens.TryGetValue(id, out var tween))
            {
                tween.Kill();
                _moveTweens.Remove(id);
            }
        }

        public void KillEffectTween(SlimeId id)
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
