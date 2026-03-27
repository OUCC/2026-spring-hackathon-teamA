using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Upgrades.Application;

namespace FloorBreaker.MatchFlow.Application
{
    /// <summary>
    /// P1/P2 のペアインスタンスを保持するホルダー。
    /// VContainer にキー付き登録がないため、このクラスで P1/P2 を区別する。
    /// </summary>
    public sealed class MatchPlayers : IDisposable
    {
        public PlayerModel Player1 { get; }
        public PlayerModel Player2 { get; }
        public BombCooldownState Cooldown1 { get; }
        public BombCooldownState Cooldown2 { get; }
        public UpgradeDraftService Draft1 { get; }
        public UpgradeDraftService Draft2 { get; }
        public IReadOnlyList<PlayerModel> All { get; }

        public MatchPlayers(
            PlayerModel player1,
            PlayerModel player2,
            BombCooldownState cooldown1,
            BombCooldownState cooldown2,
            UpgradeDraftService draft1,
            UpgradeDraftService draft2)
        {
            Player1 = player1;
            Player2 = player2;
            Cooldown1 = cooldown1;
            Cooldown2 = cooldown2;
            Draft1 = draft1;
            Draft2 = draft2;
            All = new List<PlayerModel> { player1, player2 };
        }

        public PlayerModel GetPlayer(PlayerId id) =>
            id == PlayerId.Player1 ? Player1 : Player2;

        public BombCooldownState GetCooldown(PlayerId id) =>
            id == PlayerId.Player1 ? Cooldown1 : Cooldown2;

        public UpgradeDraftService GetDraft(PlayerId id) =>
            id == PlayerId.Player1 ? Draft1 : Draft2;

        public void Dispose()
        {
            Player1?.Dispose();
            Player2?.Dispose();
            Cooldown1?.Dispose();
            Cooldown2?.Dispose();
            Draft1?.Dispose();
            Draft2?.Dispose();
        }
    }
}
