using UnityEngine;
using Fusion;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// オンライン用ゲームループ。ホスト側で FixedUpdateNetwork() を駆動し、
    /// 全プレイヤーの入力をディスパッチ後、MatchPhaseScheduler を Tick する。
    /// 状態同期アダプターは同じ GO に事前配置され、TryInitialize() で接続される。
    /// Presentation の Tick は MatchTickRunner が引き続き担当。
    /// </summary>
    public class NetworkMatchRunner : NetworkBehaviour
    {
        private NetworkServiceBridge _bridge;
        private bool _initialized;

        private NetworkMatchStateAdapter _matchState;
        private NetworkPlayerStateAdapter _playerState;
        private NetworkStageStateAdapter _stageState;
        private NetworkBombStateAdapter _bombState;
        private NetworkSlimeStateAdapter _slimeState;

        public override void Spawned()
        {
            // 初期化は TryInitialize() で遅延実行する。
            // Spawned() 時点では MatchLifetimeScope の BuildCallback が未完了で
            // NetworkServiceBridge.Current が null の可能性があるため。
            Debug.Log($"[NetworkMatchRunner] Spawned: HasStateAuthority={Object.HasStateAuthority}");
        }

        /// <summary>
        /// NetworkServiceBridge が利用可能になるまで毎フレーム試行する。
        /// 成功したら true を返し、以降は即座に true を返す。
        /// </summary>
        private bool TryInitialize()
        {
            if (_initialized) return true;

            _bridge = NetworkServiceBridge.Get(Runner);
            if (_bridge == null) return false;

            _matchState = GetComponent<NetworkMatchStateAdapter>();
            _playerState = GetComponent<NetworkPlayerStateAdapter>();
            _stageState = GetComponent<NetworkStageStateAdapter>();
            _bombState = GetComponent<NetworkBombStateAdapter>();
            _slimeState = GetComponent<NetworkSlimeStateAdapter>();

            _matchState?.Initialize(_bridge.Clock);
            _playerState?.Initialize(_bridge.Players, _bridge.Cooldowns);
            _stageState?.Initialize(_bridge.Stage);
            _bombState?.Initialize(_bridge.BombFlightTracker);
            _slimeState?.Initialize(_bridge.SlimeRegistry);

            _initialized = true;
            Debug.Log("[NetworkMatchRunner] Initialized successfully");
            return true;
        }

        public override void FixedUpdateNetwork()
        {
            if (!TryInitialize()) return;
            if (!Object.HasStateAuthority) return;

            float dt = Runner.DeltaTime;

            _bridge.InputDispatcher.TickCooldowns(dt);

            foreach (var playerRef in Runner.ActivePlayers)
            {
                if (Runner.TryGetInputForPlayer(playerRef, out FloorBreakerInput input))
                {
                    _bridge.InputDispatcher.Dispatch(playerRef.AsIndex, input);
                }
            }

            _bridge.Scheduler.Tick(dt);

            _matchState?.SyncFromDomain();
            _playerState?.SyncFromDomain();
            _stageState?.FlushBatch();
            _slimeState?.FlushMoveBatch();
        }

        public override void Render()
        {
            if (!TryInitialize()) return;
            if (Object.HasStateAuthority) return;

            _matchState?.SyncToLocal();
            _playerState?.SyncToLocal();
        }
    }
}
