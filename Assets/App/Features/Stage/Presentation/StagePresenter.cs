using System;
using System.Collections.Generic;
using UnityEngine;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Stage.Presentation
{
    public sealed class StagePresenter : IDisposable
    {
        private readonly Dictionary<GridPos, TileView> _views;
        private readonly TileAnimationService _animService;
        private readonly TileFireVfxPool _fireVfxPool;
        private readonly TileSpriteConfig _config;
        private readonly IAudioService _audio;
        private readonly IDisposable _subscription;

        private StageShrinkAnimator _shrinkAnimator;
        private TileTimerService _tileTimerService;

        public StagePresenter(
            StageModel model,
            Dictionary<GridPos, TileView> views,
            TileAnimationService animService,
            TileFireVfxPool fireVfxPool,
            TileSpriteConfig config,
            IAudioService audio)
        {
            _views = views;
            _animService = animService;
            _fireVfxPool = fireVfxPool;
            _config = config;
            _audio = audio;

            _subscription = model.TileChanged.Subscribe(HandleTileChanged);
        }

        public void SetShrinkAnimator(StageShrinkAnimator shrinkAnimator)
        {
            _shrinkAnimator = shrinkAnimator;
        }

        public void SetTileTimerService(TileTimerService tileTimerService)
        {
            _tileTimerService = tileTimerService;
        }

        /// <summary>
        /// 毎フレーム呼び出し: 炎タイルの VFX スケールを残り時間に応じて減衰させる。
        /// </summary>
        public void TickFireDecay()
        {
            if (_tileTimerService == null) return;

            foreach (var pos in _tileTimerService.GetActivePositions(TileTimerType.Fire))
            {
                float ratio = _tileTimerService.GetFireRemainingRatio(pos);
                if (ratio < 0f) continue;

                // VFX スケール: 1.0 → 0.3 に減衰
                float vfxScale = Mathf.Lerp(0.3f, 1.0f, ratio);
                _fireVfxPool.SetScale(pos, vfxScale);
            }
        }

        /// <summary>
        /// 毎フレーム呼び出し: 崩落復帰が近いタイルにグロー効果を適用する。
        /// </summary>
        public void TickRecoveryPreview()
        {
            if (_tileTimerService == null) return;

            foreach (var pos in _tileTimerService.GetActivePositions(TileTimerType.Recovery))
            {
                float ratio = _tileTimerService.GetRecoveryRemainingRatio(pos);
                if (ratio < 0f || ratio > 0.4f) continue; // 復帰40%以内（約2秒前）のみ

                if (!_views.TryGetValue(pos, out var view)) continue;

                // 復帰が近づくにつれて色を明るくする
                float brightness = Mathf.Lerp(1.0f, 0.3f, ratio / 0.4f);
                var baseColor = _config.CollapsedColor;
                var glowColor = Color.Lerp(baseColor, _config.NormalColor, brightness * 0.5f);
                view.Renderer.color = glowColor;
            }
        }

        private void HandleTileChanged(TileChangedEvent evt)
        {
            if (!_views.TryGetValue(evt.Pos, out var view)) return;

            // 縮小アニメーション中の PermanentlyDestroyed はスキップ
            // (StageShrinkAnimator がウェーブ演出を担当)
            if (evt.NewCondition == TileCondition.PermanentlyDestroyed
                && _shrinkAnimator != null
                && _shrinkAnimator.IsShrinkAnimating)
            {
                return;
            }

            // 旧状態のクリーンアップ
            CleanupOldState(evt.Pos, evt.OldCondition, view);

            // 新状態の適用
            ApplyNewState(evt, view);
        }

        private void CleanupOldState(GridPos pos, TileCondition oldCondition, TileView view)
        {
            switch (oldCondition)
            {
                case TileCondition.OnFire:
                case TileCondition.EternalFire:
                    _animService.StopFirePulse(view);
                    _fireVfxPool.DespawnAt(pos);
                    break;

                case TileCondition.Collapsing:
                case TileCondition.Collapsed:
                    _animService.KillAnimation(pos);
                    break;
            }
        }

        private void ApplyNewState(TileChangedEvent evt, TileView view)
        {
            var pos = evt.Pos;

            switch (evt.NewCondition)
            {
                case TileCondition.Intact:
                    // 復帰 or タイプ変更: タイプに応じたスプライトへ
                    if (TileData.IsHoleCondition(evt.OldCondition) || TileData.IsBurning(evt.OldCondition))
                    {
                        _animService.PlayRecovery(view, evt.NewType);
                    }
                    else
                    {
                        view.ApplyState(evt.NewData, _config);
                    }
                    break;

                case TileCondition.OnFire:
                    view.ApplyState(evt.NewData, _config);
                    _animService.PlayFirePulse(view);
                    _fireVfxPool.SpawnAt(pos, view.BasePosition);
                    _audio.PlaySfx(SfxIds.TileFire, new Float2(view.BasePosition.x, view.BasePosition.y));
                    break;

                case TileCondition.EternalFire:
                    view.ApplyState(evt.NewData, _config);
                    _animService.PlayFirePulse(view);
                    _fireVfxPool.SpawnAt(pos, view.BasePosition);
                    _audio.PlaySfx(SfxIds.TileFire, new Float2(view.BasePosition.x, view.BasePosition.y));
                    break;

                case TileCondition.Collapsing:
                    _animService.PlayCollapse(view, permanent: false);
                    _audio.PlaySfx(SfxIds.TileCollapse, new Float2(view.BasePosition.x, view.BasePosition.y));
                    break;

                case TileCondition.Collapsed:
                    view.ApplyState(evt.NewData, _config);
                    break;

                case TileCondition.PermanentlyDestroyed:
                    _animService.PlayPermanentDestroy(view);
                    _audio.PlaySfx(SfxIds.TileDestroy, new Float2(view.BasePosition.x, view.BasePosition.y));
                    break;
            }
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }
    }
}
