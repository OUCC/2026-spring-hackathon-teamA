using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Shared.Application.Interfaces
{
    public interface IAudioService
    {
        void PlaySfx(string sfxId);
        void PlaySfx(string sfxId, Float2 worldPosition);
    }
}
