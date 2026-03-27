namespace FloorBreaker.Shared.Presentation.Common
{
    public sealed class NullCameraShakeService : ICameraShakeService
    {
        public void Shake(ShakeIntensity intensity) { }
    }
}
