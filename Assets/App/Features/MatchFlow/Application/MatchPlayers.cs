using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Upgrades.Application;

namespace FloorBreaker.MatchFlow.Application
{
    /// <summary>
    /// N 人分のプレイヤー関連インスタンスを保持するホルダー。
    /// PlayerId.Index でインデックスアクセスする。
    /// </summary>
    public sealed class MatchPlayers : IDisposable
    {
        public IReadOnlyList<PlayerModel> All { get; }
        public IReadOnlyList<BombCooldownState> Cooldowns { get; }
        public IReadOnlyList<UpgradeDraftService> Drafts { get; }
        public int PlayerCount => All.Count;

        public MatchPlayers(
            IReadOnlyList<PlayerModel> players,
            IReadOnlyList<BombCooldownState> cooldowns,
            IReadOnlyList<UpgradeDraftService> drafts)
        {
            All = players;
            Cooldowns = cooldowns;
            Drafts = drafts;
        }

        public PlayerModel GetPlayer(PlayerId id) => All[id.Index];
        public BombCooldownState GetCooldown(PlayerId id) => Cooldowns[id.Index];
        public UpgradeDraftService GetDraft(PlayerId id) => Drafts[id.Index];

        public void Dispose()
        {
            foreach (var p in All) p?.Dispose();
            foreach (var c in Cooldowns) c?.Dispose();
            foreach (var d in Drafts) d?.Dispose();
        }
    }
}
