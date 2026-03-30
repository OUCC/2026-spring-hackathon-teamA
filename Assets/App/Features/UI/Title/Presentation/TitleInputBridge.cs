using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// タイトル画面でゲームプレイ操作（Navigate/Submit/Cancel）によるメニュー操作を提供する。
    /// 全プレイヤーの Gameplay マップから入力を読み取り、フォーカス移動・決定・戻るを行う。
    /// ボタンのクリックコールバックに依存せず、各項目のアクションを直接呼び出す。
    /// </summary>
    public sealed class TitleInputBridge : IDisposable
    {
        private readonly List<(InputActionMap map, Action<InputAction.CallbackContext> moveCb, Action<InputAction.CallbackContext> submitCb, Action<InputAction.CallbackContext> cancelCb)> _bindings = new();

        private Button[] _currentButtons = Array.Empty<Button>();
        private Action[] _currentActions = Array.Empty<Action>();
        private int _focusIndex;
        private Action _onCancel;
        private float _lastNavTime;
        private const float NavRepeatDelay = 0.2f;
        private bool _suspended;

        public TitleInputBridge(InputActionAsset actions)
        {
            if (actions == null) return;

            for (int i = 1; i <= 4; i++)
            {
                var map = actions.FindActionMap($"Gameplay_P{i}");
                if (map == null) continue;

                var moveAction = map.FindAction("Move");
                var breakAction = map.FindAction("BreakBombHold");
                var fireAction = map.FindAction("FireBombHold");

                if (moveAction == null) continue;

                Action<InputAction.CallbackContext> moveCb = ctx => OnMove(ctx);
                Action<InputAction.CallbackContext> submitCb = ctx => OnSubmit(ctx);
                Action<InputAction.CallbackContext> cancelCb = ctx => OnCancel(ctx);

                moveAction.performed += moveCb;
                moveAction.started += moveCb;
                if (breakAction != null) breakAction.performed += submitCb;
                if (fireAction != null) fireAction.performed += cancelCb;

                if (!map.enabled) map.Enable();
                _bindings.Add((map, moveCb, submitCb, cancelCb));
            }
        }

        private Action _suspendedCancelAction;

        /// <summary>
        /// 入力を一時停止する。cancelAction が指定されている場合、
        /// suspend 中でも Cancel キーでそのアクションを呼び出せる。
        /// </summary>
        public void Suspend(Action cancelAction = null)
        {
            _suspended = true;
            _suspendedCancelAction = cancelAction;
        }

        public void Resume()
        {
            _suspended = false;
            _suspendedCancelAction = null;
        }

        /// <summary>
        /// 現在の画面で操作可能なボタンとそれに対応するアクションを設定する。
        /// buttons[i] を選択した時に actions[i] が呼ばれる。
        /// </summary>
        public void SetMenu(Button[] buttons, Action[] actions, Action onCancel)
        {
            ClearFocusHighlight();
            _currentButtons = buttons ?? Array.Empty<Button>();
            _currentActions = actions ?? Array.Empty<Action>();
            _onCancel = onCancel;
            _focusIndex = 0;
            UpdateFocus();
        }

        public void ClearMenu()
        {
            ClearFocusHighlight();
            _currentButtons = Array.Empty<Button>();
            _currentActions = Array.Empty<Action>();
            _onCancel = null;
        }

        private void OnMove(InputAction.CallbackContext ctx)
        {
            if (_suspended || _currentButtons.Length == 0) return;

            float now = Time.realtimeSinceStartup;
            if (now - _lastNavTime < NavRepeatDelay) return;

            var v = ctx.ReadValue<Vector2>();
            if (v.y > 0.5f)
            {
                _focusIndex = Mathf.Max(0, _focusIndex - 1);
                _lastNavTime = now;
                UpdateFocus();
            }
            else if (v.y < -0.5f)
            {
                _focusIndex = Mathf.Min(_currentButtons.Length - 1, _focusIndex + 1);
                _lastNavTime = now;
                UpdateFocus();
            }
        }

        private void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (_suspended || _currentActions.Length == 0) return;
            if (_focusIndex < 0 || _focusIndex >= _currentActions.Length) return;

            var btn = _focusIndex < _currentButtons.Length ? _currentButtons[_focusIndex] : null;
            if (btn != null && !btn.enabledSelf) return;

            _currentActions[_focusIndex]?.Invoke();
        }

        private void OnCancel(InputAction.CallbackContext ctx)
        {
            if (_suspended)
            {
                _suspendedCancelAction?.Invoke();
                return;
            }
            _onCancel?.Invoke();
        }

        private void UpdateFocus()
        {
            ClearFocusHighlight();
            if (_focusIndex >= 0 && _focusIndex < _currentButtons.Length)
            {
                _currentButtons[_focusIndex]?.AddToClassList("title-btn--focused");
            }
        }

        private void ClearFocusHighlight()
        {
            foreach (var btn in _currentButtons)
                btn?.RemoveFromClassList("title-btn--focused");
        }

        public void Dispose()
        {
            foreach (var (map, moveCb, submitCb, cancelCb) in _bindings)
            {
                var moveAction = map.FindAction("Move");
                var breakAction = map.FindAction("BreakBombHold");
                var fireAction = map.FindAction("FireBombHold");

                if (moveAction != null) { moveAction.performed -= moveCb; moveAction.started -= moveCb; }
                if (breakAction != null) breakAction.performed -= submitCb;
                if (fireAction != null) fireAction.performed -= cancelCb;
            }
            _bindings.Clear();
        }
    }
}
