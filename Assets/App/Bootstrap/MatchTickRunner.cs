using System;
using VContainer.Unity;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Input.Application;
using FloorBreaker.Cameras.Presentation;
using FloorBreaker.CpuPlayer.Application;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// 毎フレームの Tick を一元駆動する ITickable。
    /// Input → Domain/Application → Presenter → Camera の順で更新。
    /// </summary>
    public sealed class MatchTickRunner : ITickable, IDisposable
    {
        private readonly MatchPhaseScheduler _scheduler;
        private readonly GameplayInputBridge _inputBridge;
        private readonly MatchPresenters _presenters;
        private readonly SplitScreenCameraSetup _cameraSetup;
        private readonly ITimeProvider _timeProvider;
        private readonly CpuPlayerService _cpuPlayerService;
        private bool _disposed;

        public MatchTickRunner(
            MatchPhaseScheduler scheduler,
            GameplayInputBridge inputBridge,
            MatchPresenters presenters,
            SplitScreenCameraSetup cameraSetup,
            ITimeProvider timeProvider,
            CpuPlayerService cpuPlayerService = null)
        {
            _scheduler = scheduler;
            _inputBridge = inputBridge;
            _presenters = presenters;
            _cameraSetup = cameraSetup;
            _timeProvider = timeProvider;
            _cpuPlayerService = cpuPlayerService;
        }

        public void Tick()
        {
            if (_disposed) return;

            float dt = _timeProvider.DeltaTime;

            // 入力のリピート移動処理
            _inputBridge.Tick(dt);

            // CPU プレイヤーの思考・行動
            _cpuPlayerService?.Tick(dt);

            // Domain / Application の Tick
            _scheduler.Tick(dt);

            // Presentation の Tick
            _presenters.TickPresenters(dt);

            // カメラ追従
            _cameraSetup.Tick(dt);
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
