namespace FloorBreaker.Cameras.Presentation
{
    public sealed class NullCameraShakeService : ICameraShakeService
    {
        public void Shake(ShakeIntensity intensity) { }
    }
}
