using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Network.Infrastructure;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Tests.PlayMode
{
    /// <summary>
    /// Fusion GameMode.Single を使ったネットワーク統合テスト。
    /// Boot シーン → Match シーンの完全な初期化チェーンを検証する。
    /// </summary>
    [TestFixture]
    public class NetworkIntegrationTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return NetworkTestHelper.SetupOnlineMatch(playerCount: 2);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return NetworkTestHelper.Teardown();
        }

        // =============================================
        // Test 1: DI 階層が正しく構築される
        // =============================================

        [UnityTest]
        public IEnumerator MatchScene_NetworkServiceBridge_IsNotNull()
        {
            yield return NetworkTestHelper.WaitForMatchRunnerInit();

            Assert.IsNotNull(NetworkServiceBridge.Current,
                "NetworkServiceBridge.Current should be set after Match scene loads");
        }

        // =============================================
        // Test 2: NetworkMatchRunner が初期化される
        // =============================================

        [UnityTest]
        public IEnumerator NetworkMatchRunner_ExistsInScene()
        {
            yield return NetworkTestHelper.WaitForMatchRunnerInit();

            var runner = Object.FindAnyObjectByType<NetworkMatchRunner>();
            Assert.IsNotNull(runner, "NetworkMatchRunner should exist in Match scene");
        }

        [UnityTest]
        public IEnumerator NetworkMatchRunner_AllAdaptersPresent()
        {
            yield return NetworkTestHelper.WaitForMatchRunnerInit();

            var runner = Object.FindAnyObjectByType<NetworkMatchRunner>();
            Assert.IsNotNull(runner);

            Assert.IsNotNull(runner.GetComponent<NetworkMatchStateAdapter>(),
                "NetworkMatchStateAdapter should be on NetworkMatchRunner GO");
            Assert.IsNotNull(runner.GetComponent<NetworkPlayerStateAdapter>(),
                "NetworkPlayerStateAdapter should be on NetworkMatchRunner GO");
            Assert.IsNotNull(runner.GetComponent<NetworkStageStateAdapter>(),
                "NetworkStageStateAdapter should be on NetworkMatchRunner GO");
            Assert.IsNotNull(runner.GetComponent<NetworkBombStateAdapter>(),
                "NetworkBombStateAdapter should be on NetworkMatchRunner GO");
            Assert.IsNotNull(runner.GetComponent<NetworkSlimeStateAdapter>(),
                "NetworkSlimeStateAdapter should be on NetworkMatchRunner GO");
        }

        // =============================================
        // Test 3: Bridge が正しいサービスを保持
        // =============================================

        [UnityTest]
        public IEnumerator NetworkServiceBridge_HasScheduler()
        {
            yield return NetworkTestHelper.WaitForMatchRunnerInit();

            var bridge = NetworkServiceBridge.Current;
            Assert.IsNotNull(bridge);
            Assert.IsNotNull(bridge.Scheduler, "Bridge should have MatchPhaseScheduler");
            Assert.IsNotNull(bridge.InputDispatcher, "Bridge should have NetworkInputDispatcher");
            Assert.IsNotNull(bridge.Clock, "Bridge should have MatchClock");
            Assert.IsNotNull(bridge.Players, "Bridge should have Players list");
            Assert.IsNotNull(bridge.Stage, "Bridge should have StageModel");
        }

        // =============================================
        // Test 4: ゲームが進行する（MatchClock が動く）
        // =============================================

        [UnityTest]
        public IEnumerator MatchClock_DecreasesOverTime()
        {
            yield return NetworkTestHelper.WaitForMatchRunnerInit();

            var bridge = NetworkServiceBridge.Current;
            Assert.IsNotNull(bridge);

            float initialRemaining = bridge.Clock.RemainingValue;

            // 数フレーム待機して Tick が進むことを確認
            for (int i = 0; i < 30; i++)
                yield return null;

            float afterRemaining = bridge.Clock.RemainingValue;
            Assert.Less(afterRemaining, initialRemaining,
                $"MatchClock should decrease: initial={initialRemaining}, after={afterRemaining}");
        }

        // =============================================
        // Test 5: 入力ディスパッチでプレイヤーが移動
        // =============================================

        [UnityTest]
        public IEnumerator InputDispatch_MovesPlayer()
        {
            yield return NetworkTestHelper.WaitForMatchRunnerInit();

            var bridge = NetworkServiceBridge.Current;
            Assert.IsNotNull(bridge);
            Assert.IsTrue(bridge.Players.Count >= 1, "Should have at least 1 player");

            var player = bridge.Players[0];
            var startPos = player.CurrentPosition;

            // 移動入力を直接ディスパッチ
            var input = new FloorBreakerInput
            {
                MoveHeld = true,
                MoveDirection = Direction8.N,
            };
            bridge.InputDispatcher.Dispatch(0, input);

            var endPos = player.CurrentPosition;
            Assert.AreNotEqual(startPos, endPos,
                $"Player should have moved: start={startPos}, end={endPos}");
        }

        // =============================================
        // Test 6: プレイヤー状態アダプターが正しい値を持つ
        // =============================================

        [UnityTest]
        public IEnumerator PlayerStateAdapter_SyncsHp()
        {
            yield return NetworkTestHelper.WaitForMatchRunnerInit();

            var bridge = NetworkServiceBridge.Current;
            Assert.IsNotNull(bridge);

            var runner = Object.FindAnyObjectByType<NetworkMatchRunner>();
            var adapter = runner.GetComponent<NetworkPlayerStateAdapter>();
            Assert.IsNotNull(adapter);

            // 数フレーム待って SyncFromDomain が実行されることを確認
            for (int i = 0; i < 5; i++)
                yield return null;

            // HP が同期されているか（adapter の Networked 値 = Domain 値）
            int domainHp = bridge.Players[0].Stats.CurrentHp.CurrentValue;
            int syncedHp = adapter.Hp[0];
            Assert.AreEqual(domainHp, syncedHp,
                $"Player HP should be synced: domain={domainHp}, networked={syncedHp}");
        }
    }
}
