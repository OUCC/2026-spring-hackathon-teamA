using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Slimes.Domain;
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
        public static NetworkServiceBridge Current { get; set; }

        public MatchPhaseScheduler Scheduler { get; }
        public NetworkInputDispatcher InputDispatcher { get; }
        public MatchClock Clock { get; }
        public IReadOnlyList<PlayerModel> Players { get; }
        public IReadOnlyList<BombCooldownState> Cooldowns { get; }
        public StageModel Stage { get; }
        public SlimeRegistry SlimeRegistry { get; }
        public BombFlightTracker BombFlightTracker { get; }

        public NetworkServiceBridge(
            MatchPhaseScheduler scheduler,
            NetworkInputDispatcher inputDispatcher,
            MatchClock clock,
            MatchPlayers players,
            StageModel stage,
            SlimeRegistry slimeRegistry,
            BombFlightTracker bombFlightTracker)
        {
            Scheduler = scheduler;
            InputDispatcher = inputDispatcher;
            Clock = clock;
            Players = players.All;
            Cooldowns = players.Cooldowns;
            Stage = stage;
            SlimeRegistry = slimeRegistry;
            BombFlightTracker = bombFlightTracker;
        }

        public void Dispose()
        {
            if (Current == this)
                Current = null;
        }
    }
}
