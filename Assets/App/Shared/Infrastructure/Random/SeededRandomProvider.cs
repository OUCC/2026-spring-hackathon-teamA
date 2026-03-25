using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Shared.Infrastructure.Random
{
    public sealed class SeededRandomProvider : IRandomProvider
    {
        private readonly System.Random _random;

        public SeededRandomProvider(int seed)
        {
            _random = new System.Random(seed);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }

        public float Value => (float)_random.NextDouble();

        public bool Chance(float probability)
        {
            return (float)_random.NextDouble() < probability;
        }
    }
}
