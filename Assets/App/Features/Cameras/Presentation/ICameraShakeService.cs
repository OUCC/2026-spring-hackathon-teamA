namespace FloorBreaker.Cameras.Presentation
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
