using UnityEngine;
using Fusion;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// オンライン用ゲームループ。ホスト側で FixedUpdateNetwork() を駆動し、
    /// 全プレイヤーの入力をディスパッチ後、MatchPhaseScheduler を Tick する。
    /// 状態同期アダプターは同じプレハブに事前配置され、Initialize() で接続される。
    /// Presentation の Tick は MatchTickRunner が引き続き担当。
    /// </summary>
    public class NetworkMatchRunner : NetworkBehaviour
    {
        private NetworkServiceBridge _bridge;
        private bool _initialized;

        // 同じ GO に事前配置されたアダプター（プレハブに含まれる）
        private NetworkMatchStateAdapter _matchState;
        private NetworkPlayerStateAdapter _playerState;
        private NetworkStageStateAdapter _stageState;
        private NetworkBombStateAdapter _bombState;
        private NetworkSlimeStateAdapter _slimeState;

        public override void Spawned()
        {
            _bridge = NetworkServiceBridge.Current;

            // クライアント側では bridge が null の場合がある（MatchLifetimeScope がまだ構築されていない）
            // その場合はアダプターの初期化を遅延する
            if (_bridge == null)
            {
                Debug.LogWarning("[NetworkMatchRunner] NetworkServiceBridge.Current is null (client side, will retry)");
                return;
            }

            InitializeAdapters();
        }

        /// <summary>クライアント側で遅延初期化を試みる。</summary>
        public void TryLateInitialize()
        {
            if (_initialized) return;
            _bridge = NetworkServiceBridge.Current;
            if (_bridge == null) return;
            InitializeAdapters();
        }

        private void InitializeAdapters()
        {
            // プレハブに事前配置されたコンポーネントを取得
            _matchState = GetComponent<NetworkMatchStateAdapter>();
            _playerState = GetComponent<NetworkPlayerStateAdapter>();
            _stageState = GetComponent<NetworkStageStateAdapter>();
            _bombState = GetComponent<NetworkBombStateAdapter>();
            _slimeState = GetComponent<NetworkSlimeStateAdapter>();

            // Domain 参照を注入
            _matchState?.Initialize(_bridge.Clock);
            _playerState?.Initialize(_bridge.Players, _bridge.Cooldowns);
            _stageState?.Initialize(_bridge.Stage);
            _bombState?.Initialize(_bridge.BombFlightTracker);
            _slimeState?.Initialize(_bridge.SlimeRegistry);

            _initialized = true;
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
                    _bridge.InputDispatcher.Dispatch(playerRef.AsIndex, input);
                }
            }

            // 3. ゲームシミュレーション
            _bridge.Scheduler.Tick(dt);

            // 4. 状態同期: Domain → [Networked]
            _matchState?.SyncFromDomain();
            _playerState?.SyncFromDomain();

            // 5. バッチ RPC 送信
            _stageState?.FlushBatch();
            _slimeState?.FlushMoveBatch();
        }

        public override void Render()
        {
            // クライアント側: 遅延初期化の試行
            if (!_initialized)
            {
                TryLateInitialize();
                if (!_initialized) return;
            }

            // クライアント側: [Networked] → Domain ミラー
            if (Object.HasStateAuthority) return;

            _matchState?.SyncToLocal();
            _playerState?.SyncToLocal();
        }
    }
}
