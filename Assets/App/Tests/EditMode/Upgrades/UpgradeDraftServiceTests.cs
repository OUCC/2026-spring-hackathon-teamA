using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Upgrades.Application;

namespace FloorBreaker.Tests.EditMode.Upgrades
{
    [TestFixture]
    public class UpgradeDraftServiceTests
    {
        private PlayerModel _player;
        private UpgradeDraftService _draft;
        private SeededRandomProvider _random;

        [SetUp]
        public void SetUp()
        {
            var stats = new PlayerStats(10, 1f, 3f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player = new PlayerModel(PlayerId.Player1, new GridPos(5, 5), stats, build);

            var balance = new TestBalanceParameters();
            var catalog = new UpgradeCatalog();
            var availabilityRule = new UpgradeAvailabilityRule(balance);
            var rollRule = new UpgradeRollRule(catalog, availabilityRule);
            var applyService = new UpgradeApplyService(balance);

            _draft = new UpgradeDraftService(rollRule, applyService, balance);
            _random = new SeededRandomProvider(42);
        }

        [TearDown]
        public void TearDown()
        {
            _draft.Dispose();
            _player.Dispose();
        }

        [Test]
        public void GenerateChoices_SetsStateToChoosing()
        {
            _draft.GenerateChoices(_player, _random);
            Assert.AreEqual(DraftState.Choosing, _draft.State.CurrentValue);
            Assert.AreEqual(3, _draft.CurrentChoices.CurrentValue.Count);
        }

        [Test]
        public void SelectChoice_ValidIndex_AppliesAndSetsSelected()
        {
            _draft.GenerateChoices(_player, _random);

            // Give enough coins to afford any upgrade
            _player.Stats.AddCoins(10);

            var result = _draft.SelectChoice(0, _player);
            Assert.IsTrue(result);
            // 複数選択対応: SelectChoice は Choosing を維持する (Skip で完了)
            Assert.AreEqual(DraftState.Choosing, _draft.State.CurrentValue);
        }

        [Test]
        public void SelectChoice_InsufficientCoins_ReturnsFalse()
        {
            _draft.GenerateChoices(_player, _random);

            // Player has 0 coins, all upgrades cost >= 2
            var result = _draft.SelectChoice(0, _player);
            Assert.IsFalse(result);
            Assert.AreEqual(DraftState.Choosing, _draft.State.CurrentValue);
        }

        [Test]
        public void Reroll_SpendsCoin_GeneratesNewChoices()
        {
            _draft.GenerateChoices(_player, _random);
            var firstChoices = _draft.CurrentChoices.CurrentValue;

            _player.Stats.AddCoins(1);
            var result = _draft.Reroll(_player, _random);

            Assert.IsTrue(result);
            Assert.AreEqual(0, _player.Stats.Coins.CurrentValue);
            // New choices generated (may differ from first)
            Assert.AreEqual(3, _draft.CurrentChoices.CurrentValue.Count);
        }

        [Test]
        public void Reroll_NoCoins_ReturnsFalse()
        {
            _draft.GenerateChoices(_player, _random);

            var result = _draft.Reroll(_player, _random);
            Assert.IsFalse(result);
        }

        [Test]
        public void Skip_SetsStateToSkipped()
        {
            _draft.GenerateChoices(_player, _random);
            _draft.Skip();
            Assert.AreEqual(DraftState.Skipped, _draft.State.CurrentValue);
        }

        [Test]
        public void TimeOut_SetsStateToTimedOut()
        {
            _draft.GenerateChoices(_player, _random);
            _draft.TimeOut();
            Assert.AreEqual(DraftState.TimedOut, _draft.State.CurrentValue);
        }

        private class TestBalanceParameters : IBalanceParameters
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
            public int FireBombMaxFlightDistance => 3;
            public int FireBombEffectRange => 1;
            public int FireBombContactDamage => 1;
            public int FireBombDotDamage => 1;
            public float FireBombDotInterval => 1f;
            public float FireBombDuration => 3.5f;
            public float FireBombCooldown => 2f;
            public float FireBombCooldownMin => 0.5f;
            public bool FireBombDefaultWallPenetration => false;
            public int StageSize => 30;
            public float WallSeedPercent => 0.08f;
            public float WallGrowthChance => 0.4f;
            public float WallTargetPercent => 0.15f;
            public int SpawnProtectionRadius => 2;
            public float SlimeSpawnCheckInterval => 5f;
            public float SlimeTargetRatio => 0.03f;
            public int SlimeMinDistanceFromPlayer => 5;
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
            public float CountdownDuration => 4f;
            public float FireBombSpreadInterval => 0.15f;
            public float BreakBombSpreadInterval => 0.3f;
            public float DashCooldown => 1f;
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
