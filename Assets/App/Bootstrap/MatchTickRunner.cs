using VContainer.Unity;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Cameras.Presentation;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// 毎フレームの Tick を一元駆動する ITickable。
    /// MatchPhaseScheduler (Domain/Application) → Presenter (Presentation) → Camera の順で更新。
    /// </summary>
    public sealed class MatchTickRunner : ITickable
    {
        private readonly MatchPhaseScheduler _scheduler;
        private readonly MatchPresenters _presenters;
        private readonly SplitScreenCameraSetup _cameraSetup;
        private readonly ITimeProvider _timeProvider;

        public MatchTickRunner(
            MatchPhaseScheduler scheduler,
            MatchPresenters presenters,
            SplitScreenCameraSetup cameraSetup,
            ITimeProvider timeProvider)
        {
            _scheduler = scheduler;
            _presenters = presenters;
            _cameraSetup = cameraSetup;
            _timeProvider = timeProvider;
        }

        public void Tick()
        {
            float dt = _timeProvider.DeltaTime;

            // Domain / Application の Tick
            _scheduler.Tick(dt);

            // Presentation の Tick
            _presenters.TickPresenters(dt);

            // カメラ追従
            _cameraSetup.Tick(dt);
        }
    }
}
