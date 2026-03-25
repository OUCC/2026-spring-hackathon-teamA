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

        public ReadOnlyReactiveProperty<PlayerId?> Winner => _winner;

        /// <summary>
        /// いずれかのプレイヤーが死亡していれば勝者を返す。
        /// 両方死亡の場合は Player2 勝利（簡易実装）。
        /// </summary>
        public PlayerId? CheckEnd(IReadOnlyList<PlayerModel> players)
        {
            bool p1Dead = players[0].Stats.IsDead;
            bool p2Dead = players[1].Stats.IsDead;

            if (p1Dead && p2Dead)
                return PlayerId.Player2;
            if (p1Dead)
                return PlayerId.Player2;
            if (p2Dead)
                return PlayerId.Player1;

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
