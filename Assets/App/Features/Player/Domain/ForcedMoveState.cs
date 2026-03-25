using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Player.Domain
{
    public sealed class ForcedMoveState
    {
        public bool IsForced { get; private set; }
        public GridPos Target { get; private set; }
        public float RemainingDuration { get; private set; }

        public void Start(GridPos target, float duration)
        {
            IsForced = true;
            Target = target;
            RemainingDuration = duration;
        }

        public void Tick(float deltaTime)
        {
            if (!IsForced) return;
            RemainingDuration -= deltaTime;
            if (RemainingDuration <= 0f)
            {
                Complete();
            }
        }

        public void Complete()
        {
            IsForced = false;
            RemainingDuration = 0f;
        }
    }
}
