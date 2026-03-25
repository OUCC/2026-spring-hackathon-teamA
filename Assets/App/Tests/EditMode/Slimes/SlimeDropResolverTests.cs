using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Tests.EditMode.Slimes
{
    [TestFixture]
    public class SlimeDropResolverTests
    {
        private PlayerModel _player;
        private SlimeDropResolver _resolver;
        private SeededRandomProvider _random;

        [SetUp]
        public void SetUp()
        {
            var stats = new PlayerStats(10, 1f, 2f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player = new PlayerModel(PlayerId.Player1, new GridPos(5, 5), stats, build);

            var catalog = new UpgradeCatalog();
            var applyService = new UpgradeApplyService(new TestBalanceParameters());
            _resolver = new SlimeDropResolver(catalog, applyService);
            _random = new SeededRandomProvider(42);
        }

        [TearDown]
        public void TearDown()
        {
            _player.Dispose();
        }

        [Test]
        public void Resolve_NormalSlime_GivesCoin()
        {
            var slime = new SlimeModel(SlimeId.Next(), SlimeType.Normal, new GridPos(3, 3));
            _resolver.Resolve(slime, _player, _random);
            Assert.AreEqual(1, _player.Stats.Coins.CurrentValue);
        }

        [Test]
        public void Resolve_GoldSlime_Gives5Coins()
        {
            var slime = new SlimeModel(SlimeId.Next(), SlimeType.Gold, new GridPos(3, 3));
            _resolver.Resolve(slime, _player, _random);
            Assert.AreEqual(5, _player.Stats.Coins.CurrentValue);
        }

        [Test]
        public void Resolve_RedSlime_AppliesUpgrade()
        {
            var slime = new SlimeModel(SlimeId.Next(), SlimeType.Red, new GridPos(3, 3));

            // Snapshot build values before
            int fireFlightBefore = _player.Build.FireFlightRange;
            int fireEffectBefore = _player.Build.FireEffectRange;
            int fireDamageBefore = _player.Build.FireDamage;
            float fireDurationBefore = _player.Build.FireDuration;
            float fireCD_Before = _player.Build.FireCooldown;
            int fallFlightBefore = _player.Build.FallFlightRange;
            int fallEffectBefore = _player.Build.FallEffectRange;
            int fallDamageBefore = _player.Build.FallDamage;
            float fallCollapseBefore = _player.Build.FallCollapseTime;
            float fallCD_Before = _player.Build.FallCooldown;
            float moveSpeedBefore = _player.Stats.MoveSpeed;

            _resolver.Resolve(slime, _player, _random);

            // At least one stat should have changed (random upgrade from unlimited stackables)
            bool anyChanged =
                _player.Build.FireFlightRange != fireFlightBefore ||
                _player.Build.FireEffectRange != fireEffectBefore ||
                _player.Build.FireDamage != fireDamageBefore ||
                _player.Build.FireDuration != fireDurationBefore ||
                _player.Build.FireCooldown != fireCD_Before ||
                _player.Build.FallFlightRange != fallFlightBefore ||
                _player.Build.FallEffectRange != fallEffectBefore ||
                _player.Build.FallDamage != fallDamageBefore ||
                _player.Build.FallCollapseTime != fallCollapseBefore ||
                _player.Build.FallCooldown != fallCD_Before ||
                _player.Stats.MoveSpeed != moveSpeedBefore;

            Assert.IsTrue(anyChanged, "Red slime should apply a random upgrade");
        }

        [Test]
        public void Resolve_NullKiller_NoDrops()
        {
            var slime = new SlimeModel(SlimeId.Next(), SlimeType.Normal, new GridPos(3, 3));
            _resolver.Resolve(slime, null, _random);

            // Player coins unchanged (not involved)
            Assert.AreEqual(0, _player.Stats.Coins.CurrentValue);
        }

        private class TestBalanceParameters : IBalanceParameters
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
            public bool FallBombDefaultWallPenetration => false;
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
            public float WallTargetPercent => 0.15f;
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
            public float StageShrinkAnimDuration => 1f;
        }
    }
}
