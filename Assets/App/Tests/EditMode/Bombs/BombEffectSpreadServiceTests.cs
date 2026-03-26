using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;

namespace FloorBreaker.Tests.EditMode.Bombs
{
    [TestFixture]
    public class BombEffectSpreadServiceTests
    {
        private StageModel _stage;
        private TileTimerService _timerService;
        private PlayerDamageService _damageService;
        private SafeTileSearchService _safeTileSearch;
        private BombEffectSpreadService _service;
        private StageQueryService _queryService;
        private BombAreaResolver _areaResolver;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            _timerService = new TileTimerService(_stage);
            _damageService = new PlayerDamageService(1.5f, 1f);
            _safeTileSearch = new SafeTileSearchService();
            _queryService = new StageQueryService(_stage);
            _areaResolver = new BombAreaResolver(_queryService);

            _service = new BombEffectSpreadService(
                _stage, _timerService, _damageService, _safeTileSearch);
        }

        [TearDown]
        public void TearDown()
        {
            _timerService.Dispose();
            _stage.Dispose();
        }

        [Test]
        public void FallBomb_Distance0_AppliedImmediately()
        {
            var center = new GridPos(5, 5);
            var fallResolver = new FallBombResolver(_areaResolver);
            var spec = new BombSpec(BombType.Fall, 3, 1, 2, 4f, false, true, 0f, 3f, 5f);
            var result = fallResolver.Resolve(center, spec, _stage);

            _service.EnqueueFallBomb(result, center, new List<PlayerModel>(), null, 0.3f);

            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(center));
        }

        [Test]
        public void FallBomb_Distance1_NotAppliedUntilTick()
        {
            var center = new GridPos(5, 5);
            var fallResolver = new FallBombResolver(_areaResolver);
            var spec = new BombSpec(BombType.Fall, 3, 1, 2, 4f, false, true, 0f, 3f, 5f);
            var result = fallResolver.Resolve(center, spec, _stage);

            _service.EnqueueFallBomb(result, center, new List<PlayerModel>(), null, 0.3f);

            // 距離 1 のタイルはまだ Normal
            Assert.AreEqual(TileState.Normal, _stage.GetTileState(new GridPos(5, 6)));
            Assert.AreEqual(TileState.Normal, _stage.GetTileState(new GridPos(6, 5)));
        }

        [Test]
        public void FallBomb_Distance1_AppliedAfterInterval()
        {
            var center = new GridPos(5, 5);
            var fallResolver = new FallBombResolver(_areaResolver);
            var spec = new BombSpec(BombType.Fall, 3, 1, 2, 4f, false, true, 0f, 3f, 5f);
            var result = fallResolver.Resolve(center, spec, _stage);

            _service.EnqueueFallBomb(result, center, new List<PlayerModel>(), null, 0.3f);
            _service.Tick(0.3f);

            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(new GridPos(5, 6)));
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(new GridPos(6, 5)));
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(new GridPos(5, 4)));
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(new GridPos(4, 5)));
        }

        [Test]
        public void FireBomb_SpreadsAtFireInterval()
        {
            var center = new GridPos(5, 5);
            var fireResolver = new FireBombResolver(_areaResolver);
            var spec = new BombSpec(BombType.Fire, 3, 1, 1, 2f, false, false, 3.5f, 0f, 0f);
            var result = fireResolver.Resolve(center, spec, _stage);

            _service.EnqueueFireBomb(result, center, new List<PlayerModel>(), null, 0.15f);

            // 中央は即座
            Assert.AreEqual(TileState.OnFire, _stage.GetTileState(center));
            // 距離 1 はまだ
            Assert.AreEqual(TileState.Normal, _stage.GetTileState(new GridPos(5, 6)));

            _service.Tick(0.15f);
            Assert.AreEqual(TileState.OnFire, _stage.GetTileState(new GridPos(5, 6)));
        }

        [Test]
        public void FallBomb_Distance2_RequiresTwoIntervals()
        {
            var center = new GridPos(5, 5);
            var fallResolver = new FallBombResolver(_areaResolver);
            // 範囲2 のスペック
            var spec = new BombSpec(BombType.Fall, 3, 2, 2, 4f, false, true, 0f, 3f, 5f);
            var result = fallResolver.Resolve(center, spec, _stage);

            _service.EnqueueFallBomb(result, center, new List<PlayerModel>(), null, 0.3f);

            // 距離 2 のタイル
            var dist2Pos = new GridPos(5, 7);
            Assert.AreEqual(TileState.Normal, _stage.GetTileState(dist2Pos));

            _service.Tick(0.3f); // 距離1まで適用
            Assert.AreEqual(TileState.Normal, _stage.GetTileState(dist2Pos));

            _service.Tick(0.3f); // 距離2まで適用
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(dist2Pos));
        }

        [Test]
        public void WaveComplete_HasActiveWavesFalse()
        {
            var center = new GridPos(5, 5);
            var fallResolver = new FallBombResolver(_areaResolver);
            var spec = new BombSpec(BombType.Fall, 3, 1, 2, 4f, false, true, 0f, 3f, 5f);
            var result = fallResolver.Resolve(center, spec, _stage);

            _service.EnqueueFallBomb(result, center, new List<PlayerModel>(), null, 0.3f);
            Assert.IsTrue(_service.HasActiveWaves);

            _service.Tick(0.3f); // 全距離適用
            Assert.IsFalse(_service.HasActiveWaves);
        }

        [Test]
        public void DifferentIntervals_FireFasterThanFall()
        {
            var center = new GridPos(5, 5);

            // 炎ボム (0.15s)
            var fireResolver = new FireBombResolver(_areaResolver);
            var fireSpec = new BombSpec(BombType.Fire, 3, 1, 1, 2f, false, false, 3.5f, 0f, 0f);
            var fireResult = fireResolver.Resolve(center, fireSpec, _stage);
            _service.EnqueueFireBomb(fireResult, center, new List<PlayerModel>(), null, 0.15f);

            // 0.15s 後: 炎は距離 1 に到達
            _service.Tick(0.15f);
            Assert.AreEqual(TileState.OnFire, _stage.GetTileState(new GridPos(5, 6)));

            // 全炎クリア
            _stage.SetTileState(center, TileState.Normal);
            _stage.SetTileState(new GridPos(5, 6), TileState.Normal);
            _stage.SetTileState(new GridPos(6, 5), TileState.Normal);
            _stage.SetTileState(new GridPos(5, 4), TileState.Normal);
            _stage.SetTileState(new GridPos(4, 5), TileState.Normal);

            // 滑落ボム (0.3s)
            var fallResolver = new FallBombResolver(_areaResolver);
            var fallSpec = new BombSpec(BombType.Fall, 3, 1, 2, 4f, false, true, 0f, 3f, 5f);
            var fallResult = fallResolver.Resolve(center, fallSpec, _stage);
            _service.EnqueueFallBomb(fallResult, center, new List<PlayerModel>(), null, 0.3f);

            // 0.15s 後: 滑落はまだ距離 1 に未到達
            _service.Tick(0.15f);
            Assert.AreEqual(TileState.Normal, _stage.GetTileState(new GridPos(6, 5)));

            // さらに 0.15s (合計 0.3s): 滑落も到達
            _service.Tick(0.15f);
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(new GridPos(6, 5)));
        }
        /// <summary>
        /// 滑落ボム A の後に B が同じタイルに重なる場合、
        /// B のタイマーで上書きされ、A のタイマー完了後も Collapsing が維持されること。
        /// </summary>
        [Test]
        public void FallBomb_OverlappingWaves_TimerResetKeepsTileCollapsing()
        {
            var fallResolver = new FallBombResolver(_areaResolver);
            var spec = new BombSpec(BombType.Fall, 3, 2, 2, 4f, false, true, 0f, 3f, 5f);

            // 重なるタイル: (5,6) — A の距離1, B の距離1
            var centerA = new GridPos(5, 5);
            var centerB = new GridPos(5, 7);
            var overlap = new GridPos(5, 6);
            var players = new List<PlayerModel>();

            // A を投下 (t=0)
            var resultA = fallResolver.Resolve(centerA, spec, _stage);
            _service.EnqueueFallBomb(resultA, centerA, players, null, 0.3f);

            // A の距離1 を適用 (t=0.3)
            _service.Tick(0.3f);
            _timerService.Tick(0.3f);
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(overlap));

            // 1秒経過 (t=1.3) — A のタイマー残り = 3.0 - 1.0 = 2.0
            _service.Tick(1.0f);
            _timerService.Tick(1.0f);

            // B を投下 (t=1.3)
            var resultB = fallResolver.Resolve(centerB, spec, _stage);
            _service.EnqueueFallBomb(resultB, centerB, players, null, 0.3f);

            // B の距離1 を適用 (t=1.6) — overlap に StartCollapseTimer 上書き
            _service.Tick(0.3f);
            _timerService.Tick(0.3f);
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(overlap));

            // A のタイマーだけなら t=0.3+3.0=3.3 で Collapsed になるはず
            // B の上書きなら t=1.6+3.0=4.6 まで Collapsing が続くはず

            // t=3.3 相当まで進める (t=1.6 から 1.7 秒進める)
            _service.Tick(1.7f);
            _timerService.Tick(1.7f);

            // B の上書きが効いていれば、まだ Collapsing のはず
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(overlap),
                "タイマー上書きが効いていれば B の 3 秒が経過するまで Collapsing が続く");
        }
    }
}

