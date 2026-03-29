using System;
using FloorBreaker.MatchFlow.Application;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// VContainer 管理下のサービス参照を NetworkBehaviour に橋渡しする。
    /// MatchLifetimeScope の RegisterBuildCallback でセットされ、
    /// NetworkBehaviour.Spawned() から参照される。
    /// </summary>
    public sealed class NetworkServiceBridge : IDisposable
    {
        /// <summary>
        /// 現在アクティブな���リッジ。MatchLifetimeScope の BuildCallback でセットされる。
        /// Scope 破棄時にクリアされる。
        /// </summary>
        public static NetworkServiceBridge Current { get; set; }

        public MatchPhaseScheduler Scheduler { get; }
        public NetworkInputDispatcher InputDispatcher { get; }

        public NetworkServiceBridge(
            MatchPhaseScheduler scheduler,
            NetworkInputDispatcher inputDispatcher)
        {
            Scheduler = scheduler;
            InputDispatcher = inputDispatcher;
        }

        public void Dispose()
        {
            if (Current == this)
                Current = null;
        }
    }
}
