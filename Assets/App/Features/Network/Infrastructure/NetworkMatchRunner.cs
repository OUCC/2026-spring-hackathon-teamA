using System.Collections.Generic;
using UnityEngine;
using Fusion;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// オンライン用ゲームループ。ホスト側で FixedUpdateNetwork() を駆動し、
    /// 全プレイヤーの入力をディスパッチ後、MatchPhaseScheduler を Tick する。
    /// 状態同期アダプターの更新も担当する。
    /// Presentation の Tick は MatchTickRunner が引き続き担当。
    /// </summary>
    public class NetworkMatchRunner : NetworkBehaviour
    {
        private NetworkServiceBridge _bridge;
        private bool _initialized;

        // 状態同期アダプター（Spawned 時に同じ GO のコンポーネントとして追加）
        private NetworkMatchStateAdapter _matchState;
        private readonly List<NetworkPlayerStateAdapter> _playerStates = new();
        private NetworkStageStateAdapter _stageState;
        private NetworkBombStateAdapter _bombState;
        private NetworkSlimeStateAdapter _slimeState;

        public override void Spawned()
        {
            _bridge = NetworkServiceBridge.Current;
            if (_bridge == null)
            {
                Debug.LogError("[NetworkMatchRunner] NetworkServiceBridge.Current is null");
                return;
            }

            InitializeAdapters();
            _initialized = true;
        }

        private void InitializeAdapters()
        {
            // MatchState
            _matchState = gameObject.AddComponent<NetworkMatchStateAdapter>();
            _matchState.Initialize(_bridge.Clock);

            // PlayerState (プレイヤーごと)
            for (int i = 0; i < _bridge.Players.Count; i++)
            {
                var adapter = gameObject.AddComponent<NetworkPlayerStateAdapter>();
                adapter.Initialize(_bridge.Players[i], _bridge.Cooldowns[i]);
                _playerStates.Add(adapter);
            }

            // StageState
            _stageState = gameObject.AddComponent<NetworkStageStateAdapter>();
            _stageState.Initialize(_bridge.Stage);

            // BombState
            _bombState = gameObject.AddComponent<NetworkBombStateAdapter>();
            _bombState.Initialize(_bridge.BombFlightTracker);

            // SlimeState
            _slimeState = gameObject.AddComponent<NetworkSlimeStateAdapter>();
            _slimeState.Initialize(_bridge.SlimeRegistry);
        }

        public override void FixedUpdateNetwork()
        {
            // ホストのみがゲームロジックを実行する
            if (!Object.HasStateAuthority) return;
            if (!_initialized) return;

            float dt = Runner.DeltaTime;

            // 1. ダッシュクールダウン減算
            _bridge.InputDispatcher.TickCooldowns(dt);

            // 2. 全プレイヤーの入力を処理
            foreach (var playerRef in Runner.ActivePlayers)
            {
                if (Runner.TryGetInputForPlayer(playerRef, out FloorBreakerInput input))
                {
                    int playerIndex = playerRef.AsIndex;
                    _bridge.InputDispatcher.Dispatch(playerIndex, input);
                }
            }

            // 3. ゲームシミュレーション
            _bridge.Scheduler.Tick(dt);

            // 4. 状態同期: Domain → [Networked]
            _matchState.SyncFromDomain();
            foreach (var ps in _playerStates)
                ps.SyncFromDomain();

            // 5. バッチ RPC 送信
            _stageState.FlushBatch();
            _slimeState.FlushMoveBatch();
        }

        public override void Render()
        {
            // クライアント側: [Networked] → Domain ミラー
            if (Object.HasStateAuthority) return;
            if (!_initialized) return;

            _matchState?.SyncToLocal();
            foreach (var ps in _playerStates)
                ps.SyncToLocal();
        }
    }
}
