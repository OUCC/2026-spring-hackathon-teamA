using System;
using R3;

namespace FloorBreaker.Player.Domain
{
    public sealed class PlayerStats : IDisposable
    {
        private readonly ReactiveProperty<int> _currentHp;
        private readonly ReactiveProperty<int> _coins;

        public int MaxHp { get; }
        public ReadOnlyReactiveProperty<int> CurrentHp => _currentHp;
        public ReadOnlyReactiveProperty<int> Coins => _coins;
        public float MoveSpeed { get; set; }
        public float MaxMoveSpeed { get; }

        public bool IsDead => _currentHp.Value <= 0;

        // --- 一時効果 ---
        private readonly ReactiveProperty<bool> _fireShieldActive = new(false);
        private readonly ReactiveProperty<bool> _levitationActive = new(false);

        public ReadOnlyReactiveProperty<bool> FireShieldActive => _fireShieldActive;
        public ReadOnlyReactiveProperty<bool> LevitationActive => _levitationActive;

        public PlayerStats(int maxHp, float baseMoveSpeed, float maxMoveSpeed)
        {
            MaxHp = maxHp;
            _currentHp = new ReactiveProperty<int>(maxHp);
            _coins = new ReactiveProperty<int>(0);
            MoveSpeed = baseMoveSpeed;
            MaxMoveSpeed = maxMoveSpeed;
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            _currentHp.Value = Math.Max(0, _currentHp.Value - amount);
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            _currentHp.Value = Math.Min(MaxHp, _currentHp.Value + amount);
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            _coins.Value += amount;
        }

        public bool SpendCoins(int amount)
        {
            if (amount <= 0) return true;
            if (_coins.Value < amount) return false;
            _coins.Value -= amount;
            return true;
        }

        public int CurrentHpValue => _currentHp.Value;

        public void SetHp(int value)
        {
            _currentHp.Value = Math.Clamp(value, 0, MaxHp);
        }

        public void ActivateFireShield() => _fireShieldActive.Value = true;
        public void DeactivateFireShield() => _fireShieldActive.Value = false;
        public void ActivateLevitation() => _levitationActive.Value = true;
        public void DeactivateLevitation() => _levitationActive.Value = false;

        /// <summary>
        /// フェーズ開始時に全一時効果をリセットする。
        /// </summary>
        public void ClearTemporaryEffects()
        {
            _fireShieldActive.Value = false;
            _levitationActive.Value = false;
        }

        // --- ネットワーク同期用ミラーセッター ---
        // クライアント側で [Networked] 値を Domain に反映するために使用

        internal void SetHpDirect(int value) => _currentHp.Value = value;
        internal void SetCoinsDirect(int value) => _coins.Value = value;
        internal void SetFireShieldDirect(bool value) => _fireShieldActive.Value = value;
        internal void SetLevitationDirect(bool value) => _levitationActive.Value = value;

        public void Dispose()
        {
            _currentHp.Dispose();
            _coins.Dispose();
            _fireShieldActive.Dispose();
            _levitationActive.Dispose();
        }
    }
}
