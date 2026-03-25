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

        public void Dispose()
        {
            _currentHp.Dispose();
            _coins.Dispose();
        }
    }
}
