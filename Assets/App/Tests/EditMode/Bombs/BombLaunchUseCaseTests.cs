using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;

namespace FloorBreaker.Tests.EditMode.Bombs
{
    [TestFixture]
    public class BombLaunchUseCaseTests
    {
        private StageModel _stage;
        private TileTimerService _timerService;
        private PlayerDamageService _damageService;
        private SafeTileSearchService _safeTileSearch;
        private BombEffectSpreadService _spreadService;
        private BombLaunchUseCase _useCase;
        private PlayerModel _player1;
        private PlayerModel _player2;
        private List<PlayerModel> _players;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            _timerService = new TileTimerService(_stage);
            _safeTileSearch = new SafeTileSearchService();
            _damageService = new PlayerDamageService(1.5f, 1f, _stage, _safeTileSearch);

            var queryService = new StageQueryService(_stage);
            var areaResolver = new BombAreaResolver(queryService);
            var landingResolver = new BombLandingResolver(_stage);
            var breakResolver = new BreakBombResolver(areaResolver);
            var fireResolver = new FireBombResolver(areaResolver);

            _spreadService = new BombEffectSpreadService(
                _stage, _timerService, _damageService, _safeTileSearch);

            _useCase = new BombLaunchUseCase(
                landingResolver, breakResolver, fireResolver,
                _stage, new TestBalanceParameters(), _spreadService);

            var stats1 = new PlayerStats(10, 1f, 3f);
            var build1 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player1 = new PlayerModel(PlayerId.Player1, new GridPos(2, 5), stats1, build1);

            var stats2 = new PlayerStats(10, 1f, 3f);
            var build2 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player2 = new PlayerModel(PlayerId.Player2, new GridPos(8, 5), stats2, build2);

            _players = new List<PlayerModel> { _player1, _player2 };
        }

        [TearDown]
        public void TearDown()
        {
            _player1.Dispose();
            _player2.Dispose();
            _timerService.Dispose();
            _stage.Dispose();
        }

        [Test]
        public void CreateBreakBombSpec_UsesBalanceRecoveryTime()
        {
            var spec = _useCase.CreateBreakBombSpec(_player1.Build);
            Assert.AreEqual(5f, spec.RecoveryTime);
            Assert.AreEqual(BombType.Break, spec.Type);
            Assert.IsTrue(spec.WallPenetration);
        }

        [Test]
        public void CreateFireBombSpec_UsesPlayerBuildValues()
        {
            var spec = _useCase.CreateFireBombSpec(_player1.Build);
            Assert.AreEqual(BombType.Fire, spec.Type);
            Assert.AreEqual(3.5f, spec.Duration);
            Assert.IsFalse(spec.WallPenetration);
        }

        [Test]
        public void ExecuteLanding_BreakBomb_CenterTileCollapsingImmediately()
        {
            var spec = _useCase.CreateBreakBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player1);
            var landingPos = new GridPos(5, 5);

            _useCase.ExecuteLanding(cmd, landingPos, _players, null);

            // 距離0 (中央) は即座に適用
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(new GridPos(5, 5)));
        }

        [Test]
        public void ExecuteLanding_BreakBomb_AdjacentTilesCollapsingAfterSpread()
        {
            var spec = _useCase.CreateBreakBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player1);
            var landingPos = new GridPos(5, 5);

            _useCase.ExecuteLanding(cmd, landingPos, _players, null);

            // 距離1 はまだ適用されていない
            Assert.AreEqual(TileState.Normal, _stage.GetTileState(new GridPos(5, 6)));

            // Tick で広がり
            _spreadService.Tick(0.3f);

            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(new GridPos(5, 6)));
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(new GridPos(6, 5)));
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(new GridPos(5, 4)));
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(new GridPos(4, 5)));
        }

        [Test]
        public void ExecuteLanding_BreakBomb_StartsTimers()
        {
            var spec = _useCase.CreateBreakBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player1);

            _useCase.ExecuteLanding(cmd, new GridPos(5, 5), _players, null);
            _spreadService.Tick(0.3f);

            Assert.IsTrue(_timerService.HasActiveTimer(new GridPos(5, 5)));
            Assert.IsTrue(_timerService.HasActiveTimer(new GridPos(5, 6)));
        }

        [Test]
        public void ExecuteLanding_BreakBomb_DamagesPlayerOnCenterTile()
        {
            _player1.CurrentPosition = new GridPos(5, 5);
            var spec = _useCase.CreateBreakBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player2);

            _useCase.ExecuteLanding(cmd, new GridPos(5, 5), _players, null);

            // 中央タイルは即座にダメージ
            Assert.AreEqual(8, _player1.Stats.CurrentHp.CurrentValue); // 10 - 2
        }

        [Test]
        public void ExecuteLanding_BreakBomb_ForcesRelocate()
        {
            _player1.CurrentPosition = new GridPos(5, 5);
            var spec = _useCase.CreateBreakBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player2);

            _useCase.ExecuteLanding(cmd, new GridPos(5, 5), _players, null);

            Assert.IsTrue(_player1.ForcedMove.IsForced);
        }

        [Test]
        public void ExecuteLanding_BreakBomb_DestroysWalls()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.Wall);
            var spec = _useCase.CreateBreakBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player1);

            _useCase.ExecuteLanding(cmd, new GridPos(5, 5), _players, null);
            _spreadService.Tick(0.3f);

            // 壁は破壊された後 Collapsing に変わる
            Assert.AreEqual(TileState.Collapsing, _stage.GetTileState(new GridPos(6, 5)));
        }

        [Test]
        public void ExecuteLanding_FireBomb_CenterTileOnFireImmediately()
        {
            var spec = _useCase.CreateFireBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player1);

            _useCase.ExecuteLanding(cmd, new GridPos(5, 5), _players, null);

            Assert.AreEqual(TileState.OnFire, _stage.GetTileState(new GridPos(5, 5)));
        }

        [Test]
        public void ExecuteLanding_FireBomb_AdjacentTilesOnFireAfterSpread()
        {
            var spec = _useCase.CreateFireBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player1);

            _useCase.ExecuteLanding(cmd, new GridPos(5, 5), _players, null);

            // 距離1 はまだ
            Assert.AreEqual(TileState.Normal, _stage.GetTileState(new GridPos(5, 6)));

            _spreadService.Tick(0.15f);

            Assert.AreEqual(TileState.OnFire, _stage.GetTileState(new GridPos(5, 6)));
        }

        [Test]
        public void ExecuteLanding_FireBomb_StartsFireTimers()
        {
            var spec = _useCase.CreateFireBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player1);

            _useCase.ExecuteLanding(cmd, new GridPos(5, 5), _players, null);
            _spreadService.Tick(0.15f);

            Assert.IsTrue(_timerService.HasActiveTimer(new GridPos(5, 5)));
        }

        [Test]
        public void ExecuteLanding_FireBomb_ContactDamageNoRelocate()
        {
            _player1.CurrentPosition = new GridPos(5, 5);
            var spec = _useCase.CreateFireBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player2);

            _useCase.ExecuteLanding(cmd, new GridPos(5, 5), _players, null);

            Assert.AreEqual(9, _player1.Stats.CurrentHp.CurrentValue); // 10 - 1
            Assert.IsFalse(_player1.ForcedMove.IsForced);
        }

        [Test]
        public void ExecuteLanding_FireBomb_DestroysWalls()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.Wall);
            var spec = _useCase.CreateFireBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player1);

            _useCase.ExecuteLanding(cmd, new GridPos(5, 5), _players, null);
            _spreadService.Tick(0.15f);

            // 壁は破壊された後 OnFire に変わる
            Assert.AreEqual(TileState.OnFire, _stage.GetTileState(new GridPos(6, 5)));
        }

        [Test]
        public void ExecuteLanding_PlayerNotOnAffectedTile_NoDamage()
        {
            // player1 is at (2,5), bomb lands at (5,5) — player not in range
            var spec = _useCase.CreateBreakBombSpec(_player1.Build);
            var cmd = new BombFlightCommand(new GridPos(2, 5), Direction8.E, spec, PlayerId.Player2);

            _useCase.ExecuteLanding(cmd, new GridPos(5, 5), _players, null);
            _spreadService.Tick(0.3f);

            Assert.AreEqual(10, _player1.Stats.CurrentHp.CurrentValue);
        }

        private sealed class TestBalanceParameters : IBalanceParameters
        {
            public int InitialHp => 10;
            public float BaseMovementSpeed => 1f;
            public float MaxMovementSpeed => 3f;
            public float MovementSpeedIncrement => 0.2f;
            public int BreakBombMaxFlightDistance => 3;
            public int BreakBombEffectRange => 1;
            public int BreakBombDamage => 2;
            public float BreakBombCollapseDuration => 3f;
            public float BreakBombRecoveryDuration => 5f;
            public float BreakBombCooldown => 4f;
            public float BreakBombCooldownMin => 1f;
            public float BreakBombCooldownReduction => 0.5f;
            public bool BreakBombDefaultWallPenetration => true;
            public int FireBombMaxFlightDistance => 3;
            public int FireBombEffectRange => 1;
            public int FireBombContactDamage => 1;
            public int FireBombDotDamage => 1;
            public float FireBombDotInterval => 1f;
            public float FireBombDuration => 3.5f;
            public float FireBombCooldown => 2f;
            public float FireBombCooldownMin => 0.5f;
            public float FireBombCooldownReduction => 0.3f;
            public bool FireBombDefaultWallPenetration => false;
            public int StageSize => 30;
            public float WallSeedPercent => 0.08f;
            public float WallGrowthChance => 0.4f;
            public float WallTargetPercent => 0.2f;
            public int SpawnProtectionRadius => 2;
            public float SlimeSpawnCheckInterval => 5f;
            public float SlimeTargetRatio => 0.03f;
            public int SlimeMinDistanceFromPlayer => 5;
            public int SlimeHp => 1;
            public float SlimeSpeedMultiplier => 0.5f;
            public int SlimeDetectionRange => 5;
            public int SlimeAttackDamage => 1;
            public float SlimeAttackCooldown => 1f;
            public int SlimeSpawnRatioNormal => 10;
            public int SlimeSpawnRatioGold => 1;
            public int SlimeSpawnRatioRed => 1;
            public float PhaseDuration => 20f;
            public float UpgradeSelectionTimeout => 10f;
            public int UpgradeChoiceCount => 3;
            public int RerollCost => 1;
            public float ForcedMoveDuration => 1f;
            public int HpRecoveryAmount => 3;
            public int HpRecoveryThreshold => 5;
            public float InvulnerabilityDuration => 1.5f;
            public float BombFlightSpeed => 12f;
            public int BombMinFlightDistance => 3;
            public float StageShrinkAnimDuration => 1f;
            public float FireBombSpreadInterval => 0.15f;
            public float BreakBombSpreadInterval => 0.3f;
            public float DashCooldown => 1f;
            public float DashDoubleTapWindow => 0.3f;
            public int FireFlightRangeIncrement => 2;
            public int FireEffectRangeIncrement => 1;
            public int FireDamageIncrement => 1;
            public float FireDurationIncrement => 2f;
            public float FireCooldownReduction => 0.3f;
            public int BreakFlightRangeIncrement => 2;
            public int BreakEffectRangeIncrement => 1;
            public int BreakDamageIncrement => 1;
            public float BreakCollapseTimeIncrement => 2f;
            public float BreakCooldownReduction => 0.5f;
            public float InputBaseMoveInterval => 0.2f;
            public float InputInitialRepeatDelay => 0.15f;
            public float InputBufferTime => 0.04f;
            public float CpuThinkInterval => 0.2f;
            public float CpuBaseMoveInterval => 0.2f;
            public float CpuBombReleaseDelay => 0.08f;
            public float CpuUpgradeInitialDelay => 1.5f;
            public float CpuUpgradePurchaseInterval => 0.6f;
        }
    }
}
