using UnityEngine;
using UnityEngine.InputSystem;
using FloorBreaker.Shared.Application.Interfaces;
using DeviceType = FloorBreaker.Shared.Application.Interfaces.DeviceType;

namespace FloorBreaker.Input.Infrastructure
{
    /// <summary>
    /// 観戦カメラ用の生デバイス入力読み取り。
    /// DeviceType に基づいて適切なデバイスからパン・ズーム・追従切替を読み取る。
    /// </summary>
    public sealed class SpectatorInputReader
    {
        public struct InputState
        {
            public Vector2 Pan;
            public float Zoom;
            public bool CycleTarget;
        }

        public InputState Read(DeviceType deviceType, int gamepadIndex)
        {
            var state = new InputState();
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            switch (deviceType)
            {
                case DeviceType.KeyboardWasd:
                    if (kb != null)
                    {
                        if (kb.wKey.isPressed) state.Pan.y += 1;
                        if (kb.sKey.isPressed) state.Pan.y -= 1;
                        if (kb.aKey.isPressed) state.Pan.x -= 1;
                        if (kb.dKey.isPressed) state.Pan.x += 1;
                        state.CycleTarget = kb.tabKey.wasPressedThisFrame;
                    }
                    if (mouse != null)
                        state.Zoom -= mouse.scroll.ReadValue().y * 0.01f;
                    break;

                case DeviceType.KeyboardArrows:
                    if (kb != null)
                    {
                        if (kb.upArrowKey.isPressed) state.Pan.y += 1;
                        if (kb.downArrowKey.isPressed) state.Pan.y -= 1;
                        if (kb.leftArrowKey.isPressed) state.Pan.x -= 1;
                        if (kb.rightArrowKey.isPressed) state.Pan.x += 1;
                        state.CycleTarget = kb.numpad5Key.wasPressedThisFrame;
                    }
                    break;

                case DeviceType.Gamepad:
                    var gamepads = Gamepad.all;
                    if (gamepadIndex >= 0 && gamepadIndex < gamepads.Count)
                    {
                        var gp = gamepads[gamepadIndex];
                        var stick = gp.leftStick.ReadValue();
                        if (stick.sqrMagnitude > 0.1f) state.Pan = stick;
                        state.Zoom = gp.rightStick.ReadValue().y;
                        state.CycleTarget = gp.rightShoulder.wasPressedThisFrame;
                    }
                    break;

                default: // None: 全入力受付 (全員 CPU 観戦時)
                    if (kb != null)
                    {
                        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) state.Pan.y += 1;
                        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) state.Pan.y -= 1;
                        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) state.Pan.x -= 1;
                        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) state.Pan.x += 1;
                        state.CycleTarget = kb.tabKey.wasPressedThisFrame;
                    }
                    if (mouse != null)
                        state.Zoom -= mouse.scroll.ReadValue().y * 0.01f;
                    var gpCurrent = Gamepad.current;
                    if (gpCurrent != null)
                    {
                        var stick2 = gpCurrent.leftStick.ReadValue();
                        if (stick2.sqrMagnitude > 0.1f) state.Pan += stick2;
                        state.Zoom += gpCurrent.rightStick.ReadValue().y;
                        if (gpCurrent.rightShoulder.wasPressedThisFrame) state.CycleTarget = true;
                    }
                    break;
            }

            return state;
        }
    }
}
