using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Stage.Presentation;

namespace FloorBreaker.Bombs.Presentation
{
    /// <summary>
    /// BombFlightTracker の R3 イベントを購読し、
    /// View/AnimService/VfxPool へディスパッチする pure C# Presenter。
    /// </summary>
    public sealed class BombPresenter : IDisposable
    {
        private readonly BombFlightTracker _tracker;
        private readonly BombViewFactory _factory;
        private readonly BombAnimationService _animService;
        private readonly BombExplosionVfxPool _vfxPool;
        private readonly BombSpriteConfig _config;
        private readonly StageQueryService _stageQuery;
        private readonly Dictionary<GridPos, TileView> _tileViews;
        private readonly float _flightSpeed;
        private readonly IAudioService _audio;
        private readonly ICameraShakeService _cameraShake;
        private readonly IImpactFreezeService _impactFreeze;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Dictionary<PlayerId, BombFlightView> _activeFlights = new();

        public BombPresenter(
            BombFlightTracker tracker,
            BombViewFactory factory,
            BombAnimationService animService,
            BombExplosionVfxPool vfxPool,
            BombSpriteConfig config,
            StageQueryService stageQuery,
            Dictionary<GridPos, TileView> tileViews,
            float flightSpeed,
            IAudioService audio = null,
            ICameraShakeService cameraShake = null,
            IImpactFreezeService impactFreeze = null)
        {
            _tracker = tracker;
            _factory = factory;
            _animService = animService;
            _vfxPool = vfxPool;
            _config = config;
            _stageQuery = stageQuery;
            _tileViews = tileViews;
            _flightSpeed = flightSpeed;
            _audio = audio;
            _cameraShake = cameraShake;
            _impactFreeze = impactFreeze;

            tracker.FlightStarted.Subscribe(OnFlightStarted).AddTo(_subscriptions);
            tracker.BombLanded.Subscribe(OnBombLanded).AddTo(_subscriptions);
        }

        private void OnFlightStarted(BombFlightStartedEvent evt)
        {
            var startWorld = evt.Origin.ToWorldCenter().ToVector3(-2f);

            // 最大到達点を計算 (実際の着弾はもっと手前の可能性あり)
            var maxEndGridPos = evt.Origin + evt.Direction.ToOffset() * evt.Spec.MaxFlightDistance;
            var endWorld = maxEndGridPos.ToWorldCenter().ToVector3(-2f);

            var duration = _flightSpeed > 0f
                ? evt.Spec.MaxFlightDistance / _flightSpeed
                : 0.25f;

            var view = _factory.GetView(evt.Owner, evt.Spec.Type, startWorld);
            _animService.PlayFlight(view, startWorld, endWorld, duration);
            _activeFlights[evt.Owner] = view;

            var pos = new Float2(startWorld.x, startWorld.y);
            _audio?.PlaySfx(SfxIds.BombLaunch, pos);
        }

        private void OnBombLanded(BombLandedEvent evt)
        {
            // 1. 飛行 tween キル
            _animService.KillFlight(evt.Owner);

            // 2. ビュー取得 + プール返却
            if (_activeFlights.TryGetValue(evt.Owner, out var view))
            {
                var landingWorld = evt.LandingPos.ToWorldCenter().ToVector3(-2f);
                view.SetPositionImmediate(landingWorld);
                _factory.ReturnView(view);
                _activeFlights.Remove(evt.Owner);
            }

            // 3. 爆発 VFX + SE
            var vfxPos = evt.LandingPos.ToWorldCenter().ToVector3(0f);
            _vfxPool.Spawn(evt.Type, vfxPos);

            var landAudioPos = new Float2(vfxPos.x, vfxPos.y);
            var sfxId = evt.Type == BombType.Fire ? SfxIds.BombExplodeFire : SfxIds.BombExplodeFall;
            _audio?.PlaySfx(sfxId, landAudioPos);
            _cameraShake?.Shake(ShakeIntensity.Medium);
            _impactFreeze?.PlayImpact(ImpactLevel.Medium);

            // 4. インパクトフラッシュ
            PlayImpactHighlights(evt);
        }

        private void PlayImpactHighlights(BombLandedEvent evt)
        {
            if (_tileViews == null || _stageQuery == null) return;

            var affectedTiles = _stageQuery.GetTilesInCross(
                evt.LandingPos, evt.EffectRange, evt.WallPenetration);

            var flashColor = _config.GetImpactColor(evt.Type);
            var duration = _config.ImpactFlashDuration;
            var peakAlpha = _config.ImpactFlashAlpha;

            foreach (var pos in affectedTiles)
            {
                if (_tileViews.TryGetValue(pos, out var tileView))
                {
                    _animService.PlayImpactFlash(tileView, flashColor, duration, peakAlpha);
                }
            }
        }

        /// <summary>
        /// 毎フレーム呼び出す。爆発 VFX のタイマー管理。
        /// </summary>
        public void Tick(float deltaTime)
        {
            _vfxPool.Tick(deltaTime);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();

            // 飛行中のビューを返却
            foreach (var view in _activeFlights.Values)
            {
                _factory.ReturnView(view);
            }
            _activeFlights.Clear();

            _animService.Dispose();
            _vfxPool.Dispose();
        }
    }
}
