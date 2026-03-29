using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Network.Infrastructure;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Tests.PlayMode
{
    /// <summary>
    /// Multi-Peer（Host + Client 同時実行）統合テスト。
    /// 1プロセス内で Host と Client を起動し、状態同期・入力伝搬を自動検証する。
    /// </summary>
    [TestFixture]
    public class MultiPeerIntegrationTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return MultiPeerTestHelper.SetupHostAndClient(playerCount: 2);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return MultiPeerTestHelper.Teardown();
        }

        // =============================================
        // Test A: Host + Client 接続確認
        // =============================================

        [UnityTest]
        public IEnumerator HostAndClient_BothHaveBridge()
        {
            var hostBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.HostRunner);
            var clientBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.ClientRunner);

            Assert.IsNotNull(hostBridge, "Host should have a NetworkServiceBridge");
            Assert.IsNotNull(clientBridge, "Client should have a NetworkServiceBridge");
            Assert.AreNotSame(hostBridge, clientBridge, "Host and Client should have different bridges");
            yield return null;
        }

        // =============================================
        // Test B: ホストの盤面がクライアントに同期される
        // =============================================

        [UnityTest]
        public IEnumerator HostTileChange_SyncsToClient()
        {
            var hostBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.HostRunner);
            var clientBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.ClientRunner);
            Assert.IsNotNull(hostBridge);
            Assert.IsNotNull(clientBridge);

            // ホスト側でタイルを壁に変更
            var testPos = new GridPos(5, 5);
            hostBridge.Stage.SetTileData(testPos, new TileData
            {
                Type = TileType.Wall,
                Condition = TileCondition.Intact,
                WarpPairId = -1,
            });

            // RPC 伝搬を待機（数フレーム）
            yield return MultiPeerTestHelper.WaitFrames(30);

            // クライアント側で同じタイルが壁になっているか
            var clientTile = clientBridge.Stage.GetTileData(testPos);
            Assert.AreEqual(TileType.Wall, clientTile.Type,
                $"Client tile at {testPos} should be Wall, but was {clientTile.Type}");
        }

        // =============================================
        // Test C: クライアントの入力がホストに届く
        // =============================================

        [UnityTest]
        public IEnumerator ClientInput_MovesPlayerOnHost()
        {
            var hostBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.HostRunner);
            Assert.IsNotNull(hostBridge);
            Assert.IsTrue(hostBridge.Players.Count >= 2, "Should have at least 2 players");

            // Player2（クライアント側プレイヤー）の初期位置
            var player2 = hostBridge.Players[1];
            var startPos = player2.CurrentPosition;

            // クライアントからの入力をシミュレート
            // Host 側の InputDispatcher で Player2 として移動入力を送信
            var input = new FloorBreakerInput
            {
                MoveHeld = true,
                MoveDirection = Direction8.N,
            };
            hostBridge.InputDispatcher.Dispatch(1, input);

            var endPos = player2.CurrentPosition;
            Assert.AreNotEqual(startPos, endPos,
                $"Player2 should have moved: start={startPos}, end={endPos}");
            yield return null;
        }

        // =============================================
        // Test D: 両方のゲームクロックが進行する
        // =============================================

        [UnityTest]
        public IEnumerator BothClocks_Progress()
        {
            var hostBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.HostRunner);
            var clientBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.ClientRunner);
            Assert.IsNotNull(hostBridge);
            Assert.IsNotNull(clientBridge);

            float hostInitial = hostBridge.Clock.RemainingValue;

            // 数フレーム待機
            yield return MultiPeerTestHelper.WaitFrames(30);

            float hostAfter = hostBridge.Clock.RemainingValue;
            Assert.Less(hostAfter, hostInitial,
                $"Host clock should decrease: initial={hostInitial}, after={hostAfter}");

            // クライアント側もフェーズが同期されているか
            // （NetworkMatchStateAdapter 経由で MatchClock が更新される）
            var hostPhase = hostBridge.Clock.CurrentPhaseValue;
            var clientPhase = clientBridge.Clock.CurrentPhaseValue;
            Assert.AreEqual(hostPhase, clientPhase,
                $"Phases should match: host={hostPhase}, client={clientPhase}");
        }

        // =============================================
        // Test E: 開始時に全タイルが一致する
        // =============================================

        [UnityTest]
        public IEnumerator InitialStage_AllTilesMatch()
        {
            var hostBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.HostRunner);
            var clientBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.ClientRunner);
            Assert.IsNotNull(hostBridge);
            Assert.IsNotNull(clientBridge);

            // スナップショット RPC の伝搬を十分待つ
            yield return MultiPeerTestHelper.WaitFrames(60);

            var bounds = hostBridge.Stage.GetCurrentBounds();
            int mismatchCount = 0;

            for (int x = bounds.MinX; x <= bounds.MaxX; x++)
            {
                for (int y = bounds.MinY; y <= bounds.MaxY; y++)
                {
                    var pos = new GridPos(x, y);
                    var hostTile = hostBridge.Stage.GetTileData(pos);
                    var clientTile = clientBridge.Stage.GetTileData(pos);

                    if (hostTile.Type != clientTile.Type || hostTile.Condition != clientTile.Condition)
                        mismatchCount++;
                }
            }

            Assert.AreEqual(0, mismatchCount,
                $"All tiles should match between Host and Client. Mismatches: {mismatchCount}");
        }

        // =============================================
        // Test F: プレイヤーの HP がホスト→クライアントに同期
        // =============================================

        [UnityTest]
        public IEnumerator PlayerHp_SyncsToClient()
        {
            var hostBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.HostRunner);
            var clientBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.ClientRunner);
            Assert.IsNotNull(hostBridge);
            Assert.IsNotNull(clientBridge);

            // ホスト側で Player1 にダメージ
            var hostPlayer1 = hostBridge.Players[0];
            int hpBefore = hostPlayer1.Stats.CurrentHp.CurrentValue;
            hostPlayer1.Stats.TakeDamage(3);
            int hostHpAfter = hostPlayer1.Stats.CurrentHp.CurrentValue;
            Assert.AreEqual(hpBefore - 3, hostHpAfter, "Host HP should decrease by 3");

            // 同期を待つ
            yield return MultiPeerTestHelper.WaitFrames(30);

            // クライアント側で同じ HP か
            var clientPlayer1 = clientBridge.Players[0];
            int clientHp = clientPlayer1.Stats.CurrentHp.CurrentValue;
            Assert.AreEqual(hostHpAfter, clientHp,
                $"Client HP should match Host: host={hostHpAfter}, client={clientHp}");
        }

        // =============================================
        // Test G: プレイヤーの位置がホスト→クライアントに同期
        // =============================================

        [UnityTest]
        public IEnumerator PlayerPosition_SyncsToClient()
        {
            var hostBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.HostRunner);
            var clientBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.ClientRunner);
            Assert.IsNotNull(hostBridge);
            Assert.IsNotNull(clientBridge);

            // ホスト側で Player1 を移動
            var input = new FloorBreakerInput { MoveHeld = true, MoveDirection = Direction8.E };
            hostBridge.InputDispatcher.Dispatch(0, input);
            var hostPos = hostBridge.Players[0].CurrentPosition;

            // 同期を待つ
            yield return MultiPeerTestHelper.WaitFrames(30);

            var clientPos = clientBridge.Players[0].CurrentPosition;
            Assert.AreEqual(hostPos, clientPos,
                $"Client position should match Host: host={hostPos}, client={clientPos}");
        }
    }
}
