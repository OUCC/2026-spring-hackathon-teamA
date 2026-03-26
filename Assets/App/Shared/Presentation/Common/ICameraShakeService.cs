namespace FloorBreaker.Shared.Presentation.Common
{
    public enum ShakeIntensity
    {
        Light,
        Medium,
        Heavy,
    }

    public interface ICameraShakeService
    {
        void Shake(ShakeIntensity intensity);
    }
}
