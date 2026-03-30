using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Upgrades.Application;
using FloorBreaker.MatchFlow.Application;

namespace FloorBreaker.Tests.EditMode.MatchFlow
{
    [TestFixture]
    public class UpgradePhaseUseCaseTests
    {
        private PlayerModel _player1;
        private PlayerModel _player2;
        private List<PlayerModel> _players;
        private UpgradeDraftService _draftP1;
        private UpgradeDraftService _draftP2;
        private UpgradePhaseUseCase _useCase;
        private SeededRandomProvider _random;

        [SetUp]
        public void SetUp()
        {
            var balance = new TestBalanceParameters();
            var catalog = new UpgradeCatalog();
            var availabilityRule = new UpgradeAvailabilityRule(balance);
            var rollRule = new UpgradeRollRule(catalog, availabilityRule);
            var applyService = new UpgradeApplyService(balance);

            _draftP1 = new UpgradeDraftService(rollRule, applyService, balance);
            _draftP2 = new UpgradeDraftService(rollRule, applyService, balance);
            _useCase = new UpgradePhaseUseCase(new List<UpgradeDraftService> { _draftP1, _draftP2 }, new UpgradeSelectionState(2), balance);

            var stats1 = new PlayerStats(10, 1f, 3f);
            var build1 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player1 = new PlayerModel(PlayerId.Player1, new GridPos(2, 2), stats1, build1);

            var stats2 = new PlayerStats(10, 1f, 3f);
            var build2 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player2 = new PlayerModel(PlayerId.Player2, new GridPos(7, 7), stats2, build2);

            _players = new List<PlayerModel> { _player1, _player2 };
            _random = new SeededRandomProvider(42);
        }

        [TearDown]
        public void TearDown()
        {
            _useCase.Dispose();
            _draftP1.Dispose();
            _draftP2.Dispose();
            _player1.Dispose();
            _player2.Dispose();
        }

        [Test]
        public void Start_GeneratesChoicesForBothPlayers()
        {
            _useCase.Start(_players, _random);

            Assert.IsTrue(_draftP1.CurrentChoices.CurrentValue.Count > 0);
            Assert.IsTrue(_draftP2.CurrentChoices.CurrentValue.Count > 0);
            Assert.AreEqual(DraftState.Choosing, _draftP1.State.CurrentValue);
            Assert.AreEqual(DraftState.Choosing, _draftP2.State.CurrentValue);
            Assert.IsTrue(_useCase.IsActive);
        }

        [Test]
        public void IsComplete_WhenBothDone_ReturnsTrue()
        {
            _useCase.Start(_players, _random);

            // 複数選択対応: 購入後に Skip で完了
            _player1.Stats.AddCoins(10);
            _player2.Stats.AddCoins(10);

            _draftP1.SelectChoice(0, _player1);
            _draftP1.Skip();
            _draftP2.SelectChoice(0, _player2);
            _draftP2.Skip();

            Assert.IsTrue(_useCase.IsComplete);
        }

        [Test]
        public void IsComplete_WhenBothSkipped_ReturnsTrue()
        {
            _useCase.Start(_players, _random);

            _draftP1.Skip();
            _draftP2.Skip();

            Assert.IsTrue(_useCase.IsComplete);
        }

        [Test]
        public void Timeout_AutoSkipsUnfinishedPlayers()
        {
            _useCase.Start(_players, _random);

            // Tick past timeout (10s)
            _useCase.Tick(10.1f);

            Assert.AreNotEqual(DraftState.Choosing, _draftP1.State.CurrentValue);
            Assert.AreNotEqual(DraftState.Choosing, _draftP2.State.CurrentValue);
            Assert.IsTrue(_useCase.IsComplete);
        }

        [Test]
        public void IsActive_FalseAfterComplete()
        {
            _useCase.Start(_players, _random);
            Assert.IsTrue(_useCase.IsActive);

            _draftP1.Skip();
            _draftP2.Skip();

            // Tick to process completion
            _useCase.Tick(0.1f);
            Assert.IsFalse(_useCase.IsActive);
        }

        // --- RemainingTime Observable テスト ---

        [Test]
        public void RemainingTime_InitiallyZero()
        {
            Assert.AreEqual(0f, _useCase.RemainingTime.CurrentValue, 0.001f);
        }

        [Test]
        public void RemainingTime_AfterStart_EqualsTimeout()
        {
            _useCase.Start(_players, _random);
            Assert.AreEqual(10f, _useCase.RemainingTime.CurrentValue, 0.001f);
        }

        [Test]
        public void RemainingTime_AfterTick_Decreases()
        {
            _useCase.Start(_players, _random);
            _useCase.Tick(3f);
            Assert.AreEqual(7f, _useCase.RemainingTime.CurrentValue, 0.01f);
        }

        [Test]
        public void RemainingTime_AfterReset_Zero()
        {
            _useCase.Start(_players, _random);
            _useCase.Tick(3f);
            _useCase.Reset();
            Assert.AreEqual(0f, _useCase.RemainingTime.CurrentValue, 0.001f);
        }

        private sealed class TestBalanceParameters : IBalanceParameters
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
            public float WallTargetPercent => 0.2f;
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
            public float InvulnerabilityDuration => 1.5f;
            public float BombFlightSpeed => 12f;
            public int BombMinFlightDistance => 3;
            public float StageShrinkAnimDuration => 1f;
            public float CountdownDuration => 4f;
            public float FireBombSpreadInterval => 0.15f;
            public float BreakBombSpreadInterval => 0.3f;
            public int HpRecoveryAmount => 3;
            public int HpRecoveryThreshold => 5;
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
