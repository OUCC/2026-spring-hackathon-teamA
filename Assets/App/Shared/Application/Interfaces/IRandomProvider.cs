namespace FloorBreaker.Shared.Application.Interfaces
{
    public interface IRandomProvider
    {
        int Range(int minInclusive, int maxExclusive);
        float Value { get; }
        bool Chance(float probability);
    }
}
