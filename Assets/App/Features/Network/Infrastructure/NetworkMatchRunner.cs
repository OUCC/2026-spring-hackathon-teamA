using UnityEngine;
using Fusion;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// オ���ライン用ゲームループ。ホスト側で FixedUpdateNetwork() を駆動し、
    /// 全プレイヤーの入力をディスパッチ後、MatchPhaseScheduler を Tick する。
    /// Presentation の Tick は MatchTickRunner ��引き続き担当する（オンライン時も登録される）。
    /// </summary>
    public class NetworkMatchRunner : NetworkBehaviour
    {
        private NetworkServiceBridge _bridge;
        private bool _initialized;

        public override void Spawned()
        {
            _bridge = NetworkServiceBridge.Current;
            if (_bridge == null)
            {
                Debug.LogError("[NetworkMatchRunner] NetworkServiceBridge.Current is null");
                return;
            }
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
                    int playerIndex = playerRef.AsIndex;
                    _bridge.InputDispatcher.Dispatch(playerIndex, input);
                }
            }

            // 3. ゲームシミュレーション（MatchPhaseScheduler が全 Domain サービ���を駆動）
            _bridge.Scheduler.Tick(dt);
        }
    }
}
