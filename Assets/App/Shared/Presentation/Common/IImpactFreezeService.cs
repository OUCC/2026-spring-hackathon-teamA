namespace FloorBreaker.Shared.Presentation.Common
{
    public enum ImpactLevel
    {
        Light,
        Medium,
        Heavy,
    }

    public interface IImpactFreezeService
    {
        void PlayImpact(ImpactLevel level);
    }
}
