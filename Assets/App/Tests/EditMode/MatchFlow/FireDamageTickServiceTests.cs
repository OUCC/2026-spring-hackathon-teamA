using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.MatchFlow.Application;

namespace FloorBreaker.Tests.EditMode.MatchFlow
{
    [TestFixture]
    public class FireDamageTickServiceTests
    {
        private StageModel _stage;
        private SlimeRegistry _slimeRegistry;
        private PlayerDamageService _damageService;
        private SafeTileSearchService _safeTileSearch;
        private FireDamageTickService _service;
        private PlayerModel _player1;
        private List<PlayerModel> _players;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            _slimeRegistry = new SlimeRegistry();
            _damageService = new PlayerDamageService(1.5f, 1f);
            _safeTileSearch = new SafeTileSearchService();
            _service = new FireDamageTickService(
                _damageService, _safeTileSearch, _slimeRegistry, new TestBalanceParameters());

            var stats = new PlayerStats(10, 1f, 2f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player1 = new PlayerModel(PlayerId.Player1, new GridPos(5, 5), stats, build);
            _players = new List<PlayerModel> { _player1 };
        }

        [TearDown]
        public void TearDown()
        {
            _player1.Dispose();
            _stage.Dispose();
        }

        [Test]
        public void Player_OnFireTile_Takes1DamageAfter1Second()
        {
            _stage.SetTileState(new GridPos(5, 5), TileState.OnFire);

            // Tick for 1 second (dotInterval = 1f)
            _service.Tick(1.0f, _players, _stage);

            Assert.AreEqual(9, _player1.Stats.CurrentHp.CurrentValue);
        }

        [Test]
        public void Player_LeavesFireTile_ResetsAccumulator()
        {
            _stage.SetTileState(new GridPos(5, 5), TileState.OnFire);

            // Tick 0.5s on fire tile (accumulates but not enough for damage)
            _service.Tick(0.5f, _players, _stage);
            Assert.AreEqual(10, _player1.Stats.CurrentHp.CurrentValue);

            // Move player off fire tile
            _player1.CurrentPosition = new GridPos(4, 4);
            _service.Tick(0.1f, _players, _stage);
            Assert.AreEqual(10, _player1.Stats.CurrentHp.CurrentValue);

            // Move player back on fire tile - accumulator was reset
            _player1.CurrentPosition = new GridPos(5, 5);

            // Clear invulnerability (not triggered, but be safe)
            _player1.Invulnerability.Tick(2f);

            // Need another full 1.0s for damage since accumulator was reset
            _service.Tick(0.9f, _players, _stage);
            Assert.AreEqual(10, _player1.Stats.CurrentHp.CurrentValue);

            _service.Tick(0.1f, _players, _stage);
            Assert.AreEqual(9, _player1.Stats.CurrentHp.CurrentValue);
        }

        [Test]
        public void Player_NotOnFire_NoDamage()
        {
            // Tile is Normal (default), not OnFire
            _service.Tick(2.0f, _players, _stage);
            Assert.AreEqual(10, _player1.Stats.CurrentHp.CurrentValue);
        }

        private sealed class TestBalanceParameters : IBalanceParameters
        {
            public int InitialHp => 10;
            public float BaseMovementSpeed => 1f;
            public float MaxMovementSpeed => 2f;
            public float MovementSpeedIncrement => 0.2f;
            public int FallBombMaxFlightDistance => 3;
            public int FallBombEffectRange => 1;
            public int FallBombDamage => 2;
            public float FallBombCollapseDuration => 3f;
            public float FallBombRecoveryDuration => 5f;
            public float FallBombCooldown => 4f;
            public float FallBombCooldownMin => 1f;
            public float FallBombCooldownReduction => 0.5f;
            public bool FallBombDefaultWallPenetration => true;
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
            public float InvulnerabilityDuration => 1.5f;
            public float BombFlightSpeed => 12f;
            public float StageShrinkAnimDuration => 1f;
            public int HpRecoveryAmount => 3;
            public int HpRecoveryThreshold => 5;
        }
    }
}
