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
        private readonly SpectatorInputBridge _spectatorBridge;
        private readonly MatchPresenters _presenters;
        private readonly SplitScreenCameraSetup _cameraSetup;
        private readonly ITimeProvider _timeProvider;
        private readonly CpuPlayerService _cpuPlayerService;
        private readonly bool _isOnline;
        private bool _disposed;

        public MatchTickRunner(
            MatchPhaseScheduler scheduler,
            GameplayInputBridge inputBridge,
            SpectatorInputBridge spectatorBridge,
            MatchPresenters presenters,
            SplitScreenCameraSetup cameraSetup,
            ITimeProvider timeProvider,
            CpuPlayerService cpuPlayerService = null,
            bool isOnline = false)
        {
            _scheduler = scheduler;
            _inputBridge = inputBridge;
            _spectatorBridge = spectatorBridge;
            _presenters = presenters;
            _cameraSetup = cameraSetup;
            _timeProvider = timeProvider;
            _cpuPlayerService = cpuPlayerService;
            _isOnline = isOnline;
        }

        public void Tick()
        {
            if (_disposed) return;

            float dt = _timeProvider.DeltaTime;

            // オンラインモードでは入力・CPU・Scheduler は NetworkMatchRunner が駆動
            if (!_isOnline)
            {
                _inputBridge.Tick(dt);
                _cpuPlayerService?.Tick(dt);
                _scheduler.Tick(dt);
            }

            // Presentation の Tick（全モードで実行）
            _presenters.TickPresenters(dt);

            // カメラ追従
            _cameraSetup.Tick(dt);

            // 観戦カメラ入力配送
            TickSpectatorCameras();
        }

        private void TickSpectatorCameras()
        {
            var spectators = _cameraSetup.Spectators;
            if (spectators == null) return;

            foreach (var spec in spectators)
            {
                if (spec == null) continue;
                var input = _spectatorBridge.ReadInput(spec.DeviceType, spec.GamepadIndex);
                spec.SetPanInput(input.Pan);
                spec.SetZoomInput(input.Zoom);
                if (input.CycleTarget) spec.CycleFollowTarget();
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
