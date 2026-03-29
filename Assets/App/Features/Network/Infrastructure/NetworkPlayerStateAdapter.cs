using Fusion;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// プレイヤー状態の同期。プレイヤーごとに1インスタンス。
    /// ホスト: PlayerModel → [Networked]
    /// クライアント: [Networked] → PlayerModel (ミラー)
    /// </summary>
    public class NetworkPlayerStateAdapter : NetworkBehaviour
    {
        // --- 基本状態 ---
        [Networked] public int Hp { get; set; }
        [Networked] public int Coins { get; set; }
        [Networked] public int PosX { get; set; }
        [Networked] public int PosY { get; set; }
        [Networked] public byte Facing { get; set; }
        [Networked] public float MoveSpeed { get; set; }

        // --- 状態フラグ ---
        [Networked] public NetworkBool IsInvulnerable { get; set; }
        [Networked] public NetworkBool IsForced { get; set; }
        [Networked] public int ForcedTargetX { get; set; }
        [Networked] public int ForcedTargetY { get; set; }

        // --- クールダウン ---
        [Networked] public float BreakCooldown { get; set; }
        [Networked] public float FireCooldown { get; set; }

        // --- 一時効果・アビリティ ---
        [Networked] public NetworkBool FireShield { get; set; }
        [Networked] public NetworkBool Levitation { get; set; }
        [Networked] public NetworkBool HasDash { get; set; }
        [Networked] public NetworkBool HasDualShot { get; set; }

        private PlayerModel _player;
        private Bombs.Domain.BombCooldownState _cooldown;
        private ChangeDetector _changeDetector;

        public void Initialize(PlayerModel player, Bombs.Domain.BombCooldownState cooldown)
        {
            _player = player;
            _cooldown = cooldown;
        }

        public override void Spawned()
        {
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        }

        /// <summary>ホスト側: Domain → [Networked]</summary>
        public void SyncFromDomain()
        {
            if (_player == null) return;

            Hp = _player.Stats.CurrentHp.CurrentValue;
            Coins = _player.Stats.Coins.CurrentValue;
            PosX = _player.CurrentPosition.X;
            PosY = _player.CurrentPosition.Y;
            Facing = (byte)_player.CurrentFacing;
            MoveSpeed = _player.Stats.MoveSpeed;

            IsInvulnerable = _player.Invulnerability.IsInvulnerable;
            IsForced = _player.ForcedMove.IsForced;
            if (_player.ForcedMove.IsForced)
            {
                ForcedTargetX = _player.ForcedMove.Target.X;
                ForcedTargetY = _player.ForcedMove.Target.Y;
            }

            if (_cooldown != null)
            {
                BreakCooldown = _cooldown.BreakBombRemaining.CurrentValue;
                FireCooldown = _cooldown.FireBombRemaining.CurrentValue;
            }

            FireShield = _player.Stats.FireShieldActive.CurrentValue;
            Levitation = _player.Stats.LevitationActive.CurrentValue;
            HasDash = _player.Build.HasDash;
            HasDualShot = _player.Build.HasDualShot;
        }

        /// <summary>クライアント側: [Networked] → Domain ミラー</summary>
        public void SyncToLocal()
        {
            if (_player == null || Object.HasStateAuthority) return;

            foreach (var change in _changeDetector.DetectChanges(this))
            {
                switch (change)
                {
                    case nameof(Hp):
                        _player.Stats.SetHpDirect(Hp);
                        break;
                    case nameof(Coins):
                        _player.Stats.SetCoinsDirect(Coins);
                        break;
                    case nameof(PosX):
                    case nameof(PosY):
                        _player.CurrentPosition = new GridPos(PosX, PosY);
                        break;
                    case nameof(Facing):
                        _player.CurrentFacing = (Direction8)Facing;
                        break;
                    case nameof(MoveSpeed):
                        _player.Stats.MoveSpeed = MoveSpeed;
                        break;
                    case nameof(FireShield):
                        _player.Stats.SetFireShieldDirect(FireShield);
                        break;
                    case nameof(Levitation):
                        _player.Stats.SetLevitationDirect(Levitation);
                        break;
                    case nameof(HasDash):
                        _player.Build.HasDash = HasDash;
                        break;
                    case nameof(HasDualShot):
                        _player.Build.HasDualShot = HasDualShot;
                        break;
                }
            }
        }
    }
}
