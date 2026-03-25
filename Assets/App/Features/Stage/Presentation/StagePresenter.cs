using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Domain.Grid;

using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Stage.Presentation
{
    public sealed class StagePresenter : IDisposable
    {
        private readonly Dictionary<GridPos, TileView> _views;
        private readonly TileAnimationService _animService;
        private readonly TileFireVfxPool _fireVfxPool;
        private readonly TileSpriteConfig _config;
        private readonly IDisposable _subscription;

        private StageShrinkAnimator _shrinkAnimator;

        public StagePresenter(
            StageModel model,
            Dictionary<GridPos, TileView> views,
            TileAnimationService animService,
            TileFireVfxPool fireVfxPool,
            TileSpriteConfig config)
        {
            _views = views;
            _animService = animService;
            _fireVfxPool = fireVfxPool;
            _config = config;

            _subscription = model.TileChanged.Subscribe(HandleTileChanged);
        }

        public void SetShrinkAnimator(StageShrinkAnimator shrinkAnimator)
        {
            _shrinkAnimator = shrinkAnimator;
        }

        private void HandleTileChanged(TileChangedEvent evt)
        {
            if (!_views.TryGetValue(evt.Pos, out var view)) return;

            // 縮小アニメーション中の PermanentlyDestroyed はスキップ
            // (StageShrinkAnimator がウェーブ演出を担当)
            if (evt.NewState == TileState.PermanentlyDestroyed
                && _shrinkAnimator != null
                && _shrinkAnimator.IsShrinkAnimating)
            {
                return;
            }

            // 旧状態のクリーンアップ
            CleanupOldState(evt.Pos, evt.OldState, view);

            // 新状態の適用
            ApplyNewState(evt.Pos, evt.NewState, view);
        }

        private void CleanupOldState(GridPos pos, TileState oldState, TileView view)
        {
            switch (oldState)
            {
                case TileState.OnFire:
                    _animService.StopFirePulse(view);
                    _fireVfxPool.DespawnAt(pos);
                    break;

                case TileState.Collapsing:
                case TileState.Collapsed:
                    _animService.KillAnimation(pos);
                    break;
            }
        }

        private void ApplyNewState(GridPos pos, TileState newState, TileView view)
        {
            switch (newState)
            {
                case TileState.Normal:
                    _animService.PlayRecovery(view);
                    break;

                case TileState.Wall:
                    view.ApplyState(TileState.Wall, _config);
                    break;

                case TileState.OnFire:
                    view.ApplyState(TileState.OnFire, _config);
                    _animService.PlayFirePulse(view);
                    _fireVfxPool.SpawnAt(pos, view.BasePosition);
                    break;

                case TileState.Collapsing:
                    _animService.PlayCollapse(view, permanent: false);
                    break;

                case TileState.Collapsed:
                    view.ApplyState(TileState.Collapsed, _config);
                    break;

                case TileState.PermanentlyDestroyed:
                    _animService.PlayPermanentDestroy(view);
                    break;
            }
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }
    }
}
