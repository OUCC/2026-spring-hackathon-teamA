using System.Collections.Generic;
using Fusion;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// 全プレイヤーの状態を同期する。NetworkArray で最大4人分を管理。
    /// プレハブに事前配置し、Spawned() で Initialize を受ける。
    /// </summary>
    public class NetworkPlayerStateAdapter : NetworkBehaviour
    {
        private const int MaxPlayers = 4;

        [Networked, Capacity(MaxPlayers)] public NetworkArray<int> Hp => default;
        [Networked, Capacity(MaxPlayers)] public NetworkArray<int> Coins => default;
        [Networked, Capacity(MaxPlayers)] public NetworkArray<int> PosX => default;
        [Networked, Capacity(MaxPlayers)] public NetworkArray<int> PosY => default;
        [Networked, Capacity(MaxPlayers)] public NetworkArray<byte> Facing => default;
        [Networked, Capacity(MaxPlayers)] public NetworkArray<float> Speed => default;
        [Networked, Capacity(MaxPlayers)] public NetworkArray<NetworkBool> Invulnerable => default;
        [Networked, Capacity(MaxPlayers)] public NetworkArray<NetworkBool> Forced => default;
        [Networked, Capacity(MaxPlayers)] public NetworkArray<NetworkBool> FireShieldArr => default;
        [Networked, Capacity(MaxPlayers)] public NetworkArray<NetworkBool> LevitationArr => default;
        [Networked, Capacity(MaxPlayers)] public NetworkArray<NetworkBool> DashArr => default;
        [Networked, Capacity(MaxPlayers)] public NetworkArray<NetworkBool> DualShotArr => default;

        private IReadOnlyList<PlayerModel> _players;
        private IReadOnlyList<BombCooldownState> _cooldowns;
        private int _playerCount;
        private ChangeDetector _changeDetector;

        public void Initialize(IReadOnlyList<PlayerModel> players, IReadOnlyList<BombCooldownState> cooldowns)
        {
            _players = players;
            _cooldowns = cooldowns;
            _playerCount = players.Count;
        }

        public override void Spawned()
        {
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        }

        /// <summary>ホスト側: Domain → [Networked]</summary>
        public void SyncFromDomain()
        {
            if (_players == null) return;
            for (int i = 0; i < _playerCount; i++)
            {
                var p = _players[i];
                Hp.Set(i, p.Stats.CurrentHp.CurrentValue);
                Coins.Set(i, p.Stats.Coins.CurrentValue);
                PosX.Set(i, p.CurrentPosition.X);
                PosY.Set(i, p.CurrentPosition.Y);
                Facing.Set(i, (byte)p.CurrentFacing);
                Speed.Set(i, p.Stats.MoveSpeed);
                Invulnerable.Set(i, p.Invulnerability.IsInvulnerable);
                Forced.Set(i, p.ForcedMove.IsForced);
                FireShieldArr.Set(i, p.Stats.FireShieldActive.CurrentValue);
                LevitationArr.Set(i, p.Stats.LevitationActive.CurrentValue);
                DashArr.Set(i, p.Build.HasDash);
                DualShotArr.Set(i, p.Build.HasDualShot);
            }
        }

        /// <summary>クライアント側: [Networked] → Domain ミラー</summary>
        public void SyncToLocal()
        {
            if (_players == null || Object.HasStateAuthority) return;
            if (_changeDetector == null) return;

            foreach (var _ in _changeDetector.DetectChanges(this))
            {
                // 変更検知されたら全プレイヤーを一括反映（NetworkArray の個別変更検知は困難）
                for (int i = 0; i < _playerCount; i++)
                {
                    var p = _players[i];
                    p.Stats.SetHpDirect(Hp[i]);
                    p.Stats.SetCoinsDirect(Coins[i]);
                    p.CurrentPosition = new GridPos(PosX[i], PosY[i]);
                    p.CurrentFacing = (Direction8)Facing[i];
                    p.Stats.MoveSpeed = Speed[i];
                    p.Stats.SetFireShieldDirect(FireShieldArr[i]);
                    p.Stats.SetLevitationDirect(LevitationArr[i]);
                    p.Build.HasDash = DashArr[i];
                    p.Build.HasDualShot = DualShotArr[i];
                }
                break; // 1回の反映で十分
            }
        }
    }
}
