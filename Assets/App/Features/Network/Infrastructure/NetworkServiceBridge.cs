using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
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
    /// Multi-Peer 対応: Runner ごとに Bridge を辞書で管理。
    /// NetworkBehaviour は Get(Runner) で自分の Runner に対応する Bridge を取得する。
    /// </summary>
    public sealed class NetworkServiceBridge : IDisposable
    {
        // Runner → Bridge の辞書（Multi-Peer 対応）
        private static readonly Dictionary<NetworkRunner, NetworkServiceBridge> _instances = new();

        /// <summary>Runner に対応する Bridge を登録する。</summary>
        public static void Register(NetworkRunner runner, NetworkServiceBridge bridge)
        {
            if (runner != null)
                _instances[runner] = bridge;
        }

        /// <summary>Runner に対応する Bridge を取得する。</summary>
        public static NetworkServiceBridge Get(NetworkRunner runner)
        {
            return runner != null && _instances.TryGetValue(runner, out var b) ? b : null;
        }

        /// <summary>Runner の Bridge を登録解除する。</summary>
        public static void Unregister(NetworkRunner runner)
        {
            if (runner != null)
                _instances.Remove(runner);
        }

        /// <summary>
        /// 後方互換: Single-Peer / PlayMode テスト用。
        /// 辞書の最初の Bridge を返す。Multi-Peer では Get(Runner) を使うこと。
        /// </summary>
        public static NetworkServiceBridge Current
        {
            get => _instances.Count > 0 ? _instances.Values.First() : null;
            set
            {
                // 後方互換: Register(null, value) 相当。テストコードからのみ使用。
                // value が null なら全クリア。
                if (value == null)
                    _instances.Clear();
            }
        }

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
            // 辞書から自分を削除
            var toRemove = new List<NetworkRunner>();
            foreach (var kvp in _instances)
            {
                if (kvp.Value == this)
                    toRemove.Add(kvp.Key);
            }
            foreach (var key in toRemove)
                _instances.Remove(key);
        }
    }
}
