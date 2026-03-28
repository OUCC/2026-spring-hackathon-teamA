using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Stage.Presentation
{
    public sealed class TileAnimationService : IDisposable
    {
        private readonly TileSpriteConfig _config;
        private readonly Dictionary<GridPos, Tween> _activeTweens = new();

        public TileAnimationService(TileSpriteConfig config)
        {
            _config = config;
        }

        public void PlayCollapse(TileView view, bool permanent, float delay = 0f)
        {
            KillAnimation(view.Pos);

            var renderer = view.Renderer;
            var basePos = view.BasePosition;

            var seq = DOTween.Sequence();
            seq.SetDelay(delay);

            // スケール縮小 (沈み込み)
            seq.Append(view.transform
                .DOScale(new Vector3(0.3f, 0.1f, 1f), _config.CollapseAnimDuration)
                .SetEase(Ease.InBack));

            // Y 座標降下
            seq.Join(view.transform
                .DOMove(basePos + new Vector3(0f, -0.3f, 0f), _config.CollapseAnimDuration)
                .SetEase(Ease.InQuad));

            // アルファフェード
            seq.Join(DOTween.ToAlpha(
                () => renderer.color,
                c => renderer.color = c,
                0.2f,
                _config.CollapseAnimDuration).SetEase(Ease.InQuad));

            seq.OnComplete(() =>
            {
                _activeTweens.Remove(view.Pos);
                if (permanent)
                {
                    renderer.enabled = false;
                }
                else
                {
                    // Collapsed スプライトに切替
                    renderer.sprite = _config.CollapsedSprite;
                    renderer.color = _config.CollapsedColor;
                    view.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                    view.transform.position = basePos;
                }
            });

            seq.SetLink(view.gameObject);
            _activeTweens[view.Pos] = seq;
        }

        public void PlayRecovery(TileView view, TileType tileType = TileType.Normal)
        {
            KillAnimation(view.Pos);

            var renderer = view.Renderer;
            renderer.enabled = true;
            renderer.sprite = _config.NormalSprite;

            // タイプに応じた目標色を決定
            Color targetColor;
            switch (tileType)
            {
                case TileType.Gas:   targetColor = _config.GasColor;  break;
                case TileType.Warp:  targetColor = _config.WarpColor; break;
                default:             targetColor = _config.NormalColor; break;
            }

            // 小さい状態から開始
            view.transform.localScale = new Vector3(0.3f, 0.1f, 1f);
            renderer.color = new Color(
                targetColor.r, targetColor.g,
                targetColor.b, 0.2f);

            var seq = DOTween.Sequence();

            // スケール復帰 (ポンと出る)
            seq.Append(view.transform
                .DOScale(Vector3.one, _config.RecoveryAnimDuration)
                .SetEase(Ease.OutBack));

            // 位置復帰
            seq.Join(view.transform
                .DOMove(view.BasePosition, _config.RecoveryAnimDuration)
                .SetEase(Ease.OutQuad));

            // アルファ復帰
            seq.Join(DOTween.ToAlpha(
                () => renderer.color,
                c => renderer.color = c,
                targetColor.a,
                _config.RecoveryAnimDuration).SetEase(Ease.OutQuad));

            var finalData = new TileData { Type = tileType, Condition = TileCondition.Intact, WarpPairId = -1 };
            seq.OnComplete(() =>
            {
                _activeTweens.Remove(view.Pos);
                view.ApplyState(finalData, _config);
            });

            seq.SetLink(view.gameObject);
            _activeTweens[view.Pos] = seq;
        }

        public void PlayFirePulse(TileView view)
        {
            KillAnimation(view.Pos);

            var renderer = view.Renderer;
            float duration = 1f / Mathf.Max(0.1f, _config.FirePulseSpeed);

            var tween = DOTween.To(
                    () => renderer.color,
                    c => renderer.color = c,
                    _config.BurningPulseBright,
                    duration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(view.gameObject);

            _activeTweens[view.Pos] = tween;
        }

        public void StopFirePulse(TileView view)
        {
            KillAnimation(view.Pos);
        }

        public void PlayPermanentDestroy(TileView view, float delay = 0f)
        {
            KillAnimation(view.Pos);

            // All In 1 Sprite Shader ディゾルブを試行
            var mat = view.GetOrCreateMaterialInstance();
            if (mat != null && mat.HasProperty("_FadeAmount"))
            {
                mat.EnableKeyword("FADE_ON");
                mat.SetColor("_FadeBurnColor", _config.BurningPulseBright);
                mat.SetFloat("_FadeBurnWidth", 0.04f);
                mat.SetFloat("_FadeBurnGlow", 3f);
                mat.SetFloat("_FadeAmount", -0.1f);

                var renderer = view.Renderer;
                var seq = DOTween.Sequence();
                seq.SetDelay(delay);
                seq.Append(DOTween.To(
                    () => mat.GetFloat("_FadeAmount"),
                    v => mat.SetFloat("_FadeAmount", v),
                    1f, _config.CollapseAnimDuration)
                    .SetEase(Ease.InQuad));
                seq.OnComplete(() =>
                {
                    _activeTweens.Remove(view.Pos);
                    renderer.enabled = false;
                });
                seq.SetLink(view.gameObject);
                _activeTweens[view.Pos] = seq;
            }
            else
            {
                // フォールバック: 従来の collapse アニメーション
                PlayCollapse(view, permanent: true, delay: delay);
            }
        }

        public void KillAnimation(GridPos pos)
        {
            if (_activeTweens.TryGetValue(pos, out var tween))
            {
                tween.Kill();
                _activeTweens.Remove(pos);
            }
        }

        public void Dispose()
        {
            foreach (var tween in _activeTweens.Values)
            {
                tween.Kill();
            }
            _activeTweens.Clear();
        }
    }
}
