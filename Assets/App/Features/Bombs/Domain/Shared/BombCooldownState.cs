using System;
using R3;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Bombs.Domain
{
    public sealed class BombCooldownState : IDisposable
    {
        private readonly ReactiveProperty<float> _fallRemaining = new(0f);
        private readonly ReactiveProperty<float> _fireRemaining = new(0f);

        public ReadOnlyReactiveProperty<float> FallBombRemaining => _fallRemaining;
        public ReadOnlyReactiveProperty<float> FireBombRemaining => _fireRemaining;

        public void StartCooldown(BombType type, float duration)
        {
            switch (type)
            {
                case BombType.Fall:
                    _fallRemaining.Value = duration;
                    break;
                case BombType.Fire:
                    _fireRemaining.Value = duration;
                    break;
            }
        }

        public bool CanFire(BombType type)
        {
            return type switch
            {
                BombType.Fall => _fallRemaining.Value <= 0f,
                BombType.Fire => _fireRemaining.Value <= 0f,
                _ => false,
            };
        }

        public void Tick(float deltaTime)
        {
            if (_fallRemaining.Value > 0f)
                _fallRemaining.Value = MathF.Max(0f, _fallRemaining.Value - deltaTime);

            if (_fireRemaining.Value > 0f)
                _fireRemaining.Value = MathF.Max(0f, _fireRemaining.Value - deltaTime);
        }

        public void Dispose()
        {
            _fallRemaining.Dispose();
            _fireRemaining.Dispose();
        }
    }
}
