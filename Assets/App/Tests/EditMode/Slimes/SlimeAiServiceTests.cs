using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Tests.EditMode.Slimes
{
    [TestFixture]
    public class SlimeAiServiceTests
    {
        private StageModel _stage;
        private SlimeRegistry _registry;
        private SlimeAiService _ai;
        private PlayerModel _player;
        private List<PlayerModel> _players;
        private TestBalanceParameters _balance;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            _registry = new SlimeRegistry();

            var safeTileSearch = new SafeTileSearchService();
            var damageService = new PlayerDamageService(invulnerabilityDuration: 1.5f, forcedMoveDuration: 1f, _stage, safeTileSearch);

            var stats = new PlayerStats(10, 1f, 3f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player = new PlayerModel(PlayerId.Player1, new GridPos(5, 5), stats, build);
            _players = new List<PlayerModel> { _player };

            _balance = new TestBalanceParameters();
            _ai = new SlimeAiService(damageService, safeTileSearch, _registry, _players, _stage, _balance);
        }

        [TearDown]
        public void TearDown()
        {
            _player.Dispose();
            _stage.Dispose();
        }

        [Test]
        public void TickAll_MovesTowardPlayer()
        {
            // Place slime 3 tiles away from player (cardinal direction)
            var slimePos = new GridPos(5, 2);
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Normal, slimePos, 1f);
            _registry.Add(slime);

            // Tick with enough time for move accumulator to trigger
            // slimeSpeed = BaseMovementSpeed(1f) * SlimeSpeedMultiplier(0.5f) = 0.5f
            // moveThreshold = 1f, so need dt * 0.5 >= 1f => dt >= 2f
            _ai.TickAll(2.1f);

            // Slime should have moved closer to player (from Y=2 toward Y=5)
            Assert.AreEqual(new GridPos(5, 3), slime.Position);
        }

        [Test]
        public void TickAll_DoesNotAttackOnFirstAdjacentTick()
        {
            // Place slime cardinally adjacent to player (5,5) => (5,4)
            var slimePos = new GridPos(5, 4);
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Normal, slimePos, initialAttackCooldown: 0f);
            _registry.Add(slime);

            // First adjacent tick: should NOT attack (first-strike delay)
            _ai.TickAll(0.1f);
            Assert.AreEqual(10, _player.Stats.CurrentHp.CurrentValue);
        }

        [Test]
        public void TickAll_AttacksAfterFirstStrikeDelay()
        {
            var slimePos = new GridPos(5, 4);
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Normal, slimePos, initialAttackCooldown: 0f);
            _registry.Add(slime);

            // First tick: sets first-strike delay (SlimeAttackCooldown = 1s)
            _ai.TickAll(0.1f);
            Assert.AreEqual(10, _player.Stats.CurrentHp.CurrentValue);

            // Tick enough for cooldown to expire
            _ai.TickAll(1.1f);

            // Player should have taken SlimeAttackDamage (1)
            Assert.AreEqual(9, _player.Stats.CurrentHp.CurrentValue);
        }

        [Test]
        public void TickAll_RespectsAttackCooldown()
        {
            var slimePos = new GridPos(5, 4);
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Normal, slimePos, initialAttackCooldown: 0f);
            _registry.Add(slime);

            // First tick: first-strike delay
            _ai.TickAll(0.1f);

            // Wait for cooldown, then attack
            _ai.TickAll(1.1f);
            Assert.AreEqual(9, _player.Stats.CurrentHp.CurrentValue);

            // Clear invulnerability so second attack could land
            _player.Invulnerability.Tick(2f);

            // Shortly after: slime on cooldown, should not attack again
            _ai.TickAll(0.1f);
            Assert.AreEqual(9, _player.Stats.CurrentHp.CurrentValue);
        }

        private class TestBalanceParameters : IBalanceParameters
        {
            public int InitialHp => 10;
            public float BaseMovementSpeed => 1f;
            public float MaxMovementSpeed => 2f;
            public float MovementSpeedIncrement => 0.2f;
            public int BreakBombMaxFlightDistance => 3;
            public int BreakBombEffectRange => 1;
            public int BreakBombDamage => 2;
            public float BreakBombCollapseDuration => 3f;
            public float BreakBombRecoveryDuration => 5f;
            public float BreakBombCooldown => 4f;
            public float BreakBombCooldownMin => 1f;
            public float BreakBombCooldownReduction => 0.5f;
            public bool BreakBombDefaultWallPenetration => false;
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
