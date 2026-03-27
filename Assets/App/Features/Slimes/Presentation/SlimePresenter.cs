using System;
using System.Collections.Generic;
using UnityEngine;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Slimes.Presentation
{
    /// <summary>
    /// SlimeRegistry の R3 イベントを購読し、SlimeView / SlimeAnimationService へディスパッチする。
    /// pure C# クラス (MonoBehaviour ではない)。
    /// </summary>
    public sealed class SlimePresenter : IDisposable
    {
        private readonly SlimeViewFactory _factory;
        private readonly SlimeAnimationService _animService;
        private readonly SlimeSpriteConfig _config;
        private readonly IAudioService _audio;
        private readonly ICameraShakeService _cameraShake;
        private readonly Dictionary<SlimeId, SlimeView> _views = new();
        private readonly CompositeDisposable _subscriptions = new();

        public SlimePresenter(
            SlimeRegistry registry,
            SlimeViewFactory factory,
            SlimeAnimationService animService,
            SlimeSpriteConfig config,
            IAudioService audio,
            ICameraShakeService cameraShake)
        {
            _factory = factory;
            _animService = animService;
            _config = config;
            _audio = audio;
            _cameraShake = cameraShake;

            registry.Spawned.Subscribe(OnSlimeSpawned).AddTo(_subscriptions);
            registry.Moved.Subscribe(OnSlimeMoved).AddTo(_subscriptions);
            registry.Killed.Subscribe(OnSlimeKilled).AddTo(_subscriptions);
            registry.Attacked.Subscribe(OnSlimeAttacked).AddTo(_subscriptions);
        }

        private void OnSlimeSpawned(SlimeSpawnedEvent evt)
        {
            var view = _factory.CreateSlimeView(evt.Id, evt.Type, evt.Position);
            _views[evt.Id] = view;
            _animService.PlaySpawn(view);
            var pos = evt.Position.ToWorldCenter();
            _audio.PlaySfx(SfxIds.SlimeSpawn, pos);
        }

        private void OnSlimeMoved(SlimeMovedEvent evt)
        {
            if (!_views.TryGetValue(evt.Id, out var view)) return;

            var dir = DeriveMoveDirection(evt.OldPosition, evt.NewPosition);
            if (dir.HasValue)
            {
                view.SetDirection(dir.Value, _config);
            }

            var worldTarget = evt.NewPosition.ToWorldCenter().ToVector3(-1f);
            _animService.PlayMove(view, worldTarget);
        }

        private void OnSlimeKilled(SlimeKilledEvent evt)
        {
            if (!_views.TryGetValue(evt.Id, out var view)) return;

            _views.Remove(evt.Id);
            SpawnDeathVfx(view.transform.position);
            _animService.PlayDeath(view, () => _factory.DestroySlimeView(view));
            var deathPos = view.transform.position;
            _audio.PlaySfx(SfxIds.SlimeDeath, new Float2(deathPos.x, deathPos.y));
        }

        private void SpawnDeathVfx(Vector3 position)
        {
            var prefab = _config.DeathVfxPrefab;
            if (prefab == null) return;

            var go = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            // VFX スケールをスライムサイズに合わせる
            var scale = _config.SlimeScale * 2f;
            go.transform.localScale = new Vector3(scale, scale, scale);
            // ParticleSystem 完了後に自動破棄
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                UnityEngine.Object.Destroy(go, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            else
            {
                UnityEngine.Object.Destroy(go, 2f);
            }
        }

        private void OnSlimeAttacked(SlimeAttackedEvent evt)
        {
            // 攻撃 VFX をターゲット位置に再生
            var targetWorld = evt.TargetPosition.ToWorldCenter().ToVector3(-1f);
            SpawnAttackVfx(targetWorld);
            _audio.PlaySfx(SfxIds.SlimeAttack, new Float2(targetWorld.x, targetWorld.y));
            _cameraShake.Shake(ShakeIntensity.Light);

            // 攻撃者の突進アニメーション
            if (_views.TryGetValue(evt.AttackerId, out var attackerView))
            {
                var attackerWorld = evt.AttackerPosition.ToWorldCenter().ToVector3(-1f);
                var direction = targetWorld - attackerWorld;
                _animService.PlayAttack(attackerView, direction);
            }
        }

        private void SpawnAttackVfx(Vector3 position)
        {
            var prefab = _config.AttackVfxPrefab;
            if (prefab == null) return;

            var go = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            var scale = _config.SlimeScale * 3f;
            go.transform.localScale = new Vector3(scale, scale, scale);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                UnityEngine.Object.Destroy(go, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            else
            {
                UnityEngine.Object.Destroy(go, 1.5f);
            }
        }

        private static Direction8? DeriveMoveDirection(GridPos from, GridPos to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            if (dx == 0 && dy == 0) return null;

            // スライム AI は 4方向 (X or Y) のみ移動するが、念のため 8方向に対応
            if (dx > 0 && dy > 0) return Direction8.NE;
            if (dx > 0 && dy < 0) return Direction8.SE;
            if (dx < 0 && dy > 0) return Direction8.NW;
            if (dx < 0 && dy < 0) return Direction8.SW;
            if (dx > 0) return Direction8.E;
            if (dx < 0) return Direction8.W;
            if (dy > 0) return Direction8.N;
            return Direction8.S;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();

            foreach (var view in _views.Values)
            {
                if (view != null)
                {
                    _factory.DestroySlimeView(view);
                }
            }
            _views.Clear();
        }
    }
}
