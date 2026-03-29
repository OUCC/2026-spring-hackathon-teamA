using System.Collections;
using System.Linq;
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
        // Test C: Player2 への入力ディスパッチが正しく動作する
        // =============================================

        [UnityTest]
        public IEnumerator Player2Input_DispatchedOnHost_MovesPlayer()
        {
            var hostBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.HostRunner);
            Assert.IsNotNull(hostBridge);
            Assert.IsTrue(hostBridge.Players.Count >= 2, "Should have at least 2 players");

            // Player2 の初期位置
            var player2 = hostBridge.Players[1];
            var startPos = player2.CurrentPosition;

            // ホスト側で Player2(index=1) として移動入力をディスパッチ
            var input = new FloorBreakerInput { MoveHeld = true, MoveDirection = Direction8.N };
            hostBridge.InputDispatcher.Dispatch(1, input);

            // ディスパッチ後即座に位置が変わる（Domain 直接呼び出し）
            var endPos = player2.CurrentPosition;
            Assert.AreNotEqual(startPos, endPos,
                $"Player2 should have moved: start={startPos}, end={endPos}");

            // 位置がクライアントに同期されるか確認
            yield return MultiPeerTestHelper.WaitFrames(30);

            var clientBridge = NetworkServiceBridge.Get(MultiPeerTestHelper.ClientRunner);
            var clientPos = clientBridge.Players[1].CurrentPosition;
            Assert.AreEqual(endPos, clientPos,
                $"Client Player2 position should match Host: host={endPos}, client={clientPos}");
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

        // =============================================
        // Test H: ロビー — ステージ選択がクライアントに同期
        // =============================================

        [UnityTest]
        public IEnumerator Lobby_StageName_SyncsToClient()
        {
            // LobbyController をスポーン
            yield return MultiPeerTestHelper.SpawnLobbyController();

            var hostLobby = MultiPeerTestHelper.FindLobbyForRunner(MultiPeerTestHelper.HostRunner);
            var clientLobby = MultiPeerTestHelper.FindLobbyForRunner(MultiPeerTestHelper.ClientRunner);
            Assert.IsNotNull(hostLobby, "Host should have LobbyController");
            Assert.IsNotNull(clientLobby, "Client should have LobbyController");

            // ホスト側でステージ名を変更
            hostLobby.SetLobbyConfig(2, new[] { false, false, false, false }, "GasWorks");

            // Fusion が [Networked] プロパティを同期するまで待機
            yield return MultiPeerTestHelper.WaitFrames(30);

            Assert.AreEqual("GasWorks", clientLobby.StageName.ToString(),
                $"Client StageName should be 'GasWorks', was '{clientLobby.StageName}'");
        }

        // =============================================
        // Test I: ロビー — プレイヤー数がクライアントに同期
        // =============================================

        [UnityTest]
        public IEnumerator Lobby_PlayerCount_SyncsToClient()
        {
            yield return MultiPeerTestHelper.SpawnLobbyController();

            var hostLobby = MultiPeerTestHelper.FindLobbyForRunner(MultiPeerTestHelper.HostRunner);
            var clientLobby = MultiPeerTestHelper.FindLobbyForRunner(MultiPeerTestHelper.ClientRunner);
            Assert.IsNotNull(hostLobby);
            Assert.IsNotNull(clientLobby);

            // ホスト側でプレイヤー数と CPU スロットを変更
            hostLobby.SetLobbyConfig(3, new[] { false, false, true, false }, "Standard");

            yield return MultiPeerTestHelper.WaitFrames(30);

            Assert.AreEqual(3, clientLobby.PlayerCount,
                $"Client PlayerCount should be 3, was {clientLobby.PlayerCount}");
            Assert.AreEqual(
                LobbyController.EncodeCpuSlots(new[] { false, false, true, false }),
                clientLobby.CpuSlotMask,
                "Client CpuSlotMask should match host");
        }
    }
}
