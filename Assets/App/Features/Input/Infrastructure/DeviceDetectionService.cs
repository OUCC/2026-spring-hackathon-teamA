using System;
using UnityEngine.InputSystem;
using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Input.Infrastructure
{
    /// <summary>
    /// 任意のデバイスボタン押下を監視し、デバイス種別を判定して通知する。
    /// TitlePresenter の Press to Join フローで使用。
    /// </summary>
    public sealed class DeviceDetectionService : IDisposable
    {
        private int _listeningSlot = -1;
        private IDisposable _subscription;

        /// <summary>デバイスが割り当てられた時に発火。(slot, deviceType, gamepadIndex)</summary>
        public event Action<int, DeviceType, int> OnDeviceAssigned;

        public bool IsListening => _listeningSlot >= 0;
        public int ListeningSlot => _listeningSlot;

        public void StartListening(int slot)
        {
            StopListening();
            _listeningSlot = slot;
            _subscription = InputSystem.onAnyButtonPress.Subscribe(new ButtonObserver(this));
        }

        public void StopListening()
        {
            _subscription?.Dispose();
            _subscription = null;
            _listeningSlot = -1;
        }

        private void OnButtonPressed(InputControl control)
        {
            if (_listeningSlot < 0) return;
            var device = control.device;

            if (device is Mouse) return;

            if (device is Keyboard)
            {
                var type = ClassifyKeyboardKey(control);
                if (type == DeviceType.None) return;
                int slot = _listeningSlot;
                StopListening();
                OnDeviceAssigned?.Invoke(slot, type, -1);
            }
            else if (device is Gamepad gp)
            {
                int gpIdx = -1;
                for (int i = 0; i < Gamepad.all.Count; i++)
                    if (Gamepad.all[i] == gp) { gpIdx = i; break; }
                int slot = _listeningSlot;
                StopListening();
                OnDeviceAssigned?.Invoke(slot, DeviceType.Gamepad, gpIdx);
            }
        }

        private static DeviceType ClassifyKeyboardKey(InputControl control)
        {
            string name = control.name.ToLowerInvariant();
            if (name is "w" or "a" or "s" or "d" or "space")
                return DeviceType.KeyboardWasd;
            if (name.Contains("arrow") || name.StartsWith("up") || name.StartsWith("down")
                || name.StartsWith("left") || name.StartsWith("right")
                || name.Contains("numpad"))
                return DeviceType.KeyboardArrows;
            return DeviceType.KeyboardWasd;
        }

        public void Dispose()
        {
            StopListening();
        }

        private sealed class ButtonObserver : IObserver<InputControl>
        {
            private readonly DeviceDetectionService _service;
            public ButtonObserver(DeviceDetectionService service) => _service = service;
            public void OnNext(InputControl value) => _service.OnButtonPressed(value);
            public void OnError(Exception error) { }
            public void OnCompleted() { }
        }
    }
}
