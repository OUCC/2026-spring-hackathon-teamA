namespace FloorBreaker.Shared.Application.Interfaces
{
    public interface ITimeProvider
    {
        float DeltaTime { get; }
        float UnscaledTime { get; }
    }
}
