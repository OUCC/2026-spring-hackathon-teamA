using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.MatchFlow.Application
{
    public sealed class MatchEndUseCase : IDisposable
    {
        private readonly ReactiveProperty<PlayerId?> _winner = new(null);
        private PlayerId? _lastAliveDied;

        public ReadOnlyReactiveProperty<PlayerId?> Winner => _winner;

        /// <summary>
        /// 生存者が1人以下になったら勝者を返す。
        /// 全員同時死亡の場合は最後に死亡したプレイヤーが勝利。
        /// </summary>
        public PlayerId? CheckEnd(IReadOnlyList<PlayerModel> players)
        {
            PlayerId? lastAlive = null;
            int aliveCount = 0;

            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].Stats.IsDead)
                {
                    aliveCount++;
                    lastAlive = players[i].Id;
                }
            }

            if (aliveCount == 1)
                return lastAlive;

            if (aliveCount == 0)
            {
                // 全員死亡: 最後の生存者（前フレームで記録）を勝者に
                // 記録がなければ最後のプレイヤーを勝者とする
                return _lastAliveDied ?? players[players.Count - 1].Id;
            }

            // 次フレームの全員死亡に備えて最後の生存者を記録
            _lastAliveDied = lastAlive;
            return null;
        }

        public void SetWinner(PlayerId winner)
        {
            _winner.Value = winner;
        }

        public void Dispose()
        {
            _winner.Dispose();
        }
    }
}
