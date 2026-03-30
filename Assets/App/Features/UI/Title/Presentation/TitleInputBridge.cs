using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// 2D グリッドメニューの項目。
    /// </summary>
    public struct GridMenuItem
    {
        public VisualElement Element;
        public Action OnSubmit;
        public string FocusClass;
    }

    /// <summary>
    /// タイトル画面でゲームプレイ操作（Navigate/Submit/Cancel）によるメニュー操作を提供する。
    /// 全プレイヤーの Gameplay マップから入力を読み取り、フォーカス移動・決定・戻るを行う。
    /// ボタンのクリックコールバックに依存せず、各項目のアクションを直接呼び出す。
    /// </summary>
    public sealed class TitleInputBridge : IDisposable
    {
        private readonly List<(InputActionMap map, Action<InputAction.CallbackContext> moveCb, Action<InputAction.CallbackContext> submitCb, Action<InputAction.CallbackContext> cancelCb)> _bindings = new();

        // 1D メニュー用
        private Button[] _currentButtons = Array.Empty<Button>();
        private Action[] _currentActions = Array.Empty<Action>();

        // 2D グリッドメニュー用
        private GridMenuItem[][] _gridRows;
        private int _gridRow;
        private int _gridCol;
        private bool _isGridMode;

        private int _focusIndex;
        private Action _onCancel;
        private float _lastNavTime;
        private const float NavRepeatDelay = 0.2f;
        private bool _suspended;

        // ゲームパッド A/B 用のランタイムアクション
        private InputAction _gamepadSubmitAction;
        private InputAction _gamepadCancelAction;

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
                if (fireAction != null) fireAction.performed += submitCb;
                if (breakAction != null) breakAction.performed += cancelCb;

                if (!map.enabled) map.Enable();
                _bindings.Add((map, moveCb, submitCb, cancelCb));
            }

            // ゲームパッド A/B ボタン（全ゲームパッド共通）
            _gamepadSubmitAction = new InputAction("TitleSubmit", binding: "<Gamepad>/buttonSouth");
            _gamepadSubmitAction.performed += ctx => OnSubmit(ctx);
            _gamepadSubmitAction.Enable();

            _gamepadCancelAction = new InputAction("TitleCancel", binding: "<Gamepad>/buttonEast");
            _gamepadCancelAction.performed += ctx => OnCancel(ctx);
            _gamepadCancelAction.Enable();
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
        /// 現在の画面で操作可能なボタンとそれに対応するアクションを設定する（1D リスト）。
        /// buttons[i] を選択した時に actions[i] が呼ばれる。
        /// </summary>
        public void SetMenu(Button[] buttons, Action[] actions, Action onCancel)
        {
            ClearAllFocus();
            _isGridMode = false;
            _gridRows = null;
            _currentButtons = buttons ?? Array.Empty<Button>();
            _currentActions = actions ?? Array.Empty<Action>();
            _onCancel = onCancel;
            _focusIndex = 0;
            UpdateFocus();
        }

        /// <summary>
        /// 2D グリッドメニューを設定。rows[rowIndex][colIndex] で上下左右に移動可能。
        /// </summary>
        public void SetGridMenu(GridMenuItem[][] rows, Action onCancel)
        {
            ClearAllFocus();
            _isGridMode = true;
            _gridRows = rows ?? Array.Empty<GridMenuItem[]>();
            _currentButtons = Array.Empty<Button>();
            _currentActions = Array.Empty<Action>();
            _onCancel = onCancel;
            _gridRow = 0;
            _gridCol = 0;
            UpdateGridFocus();
        }

        private void OnMove(InputAction.CallbackContext ctx)
        {
            if (_suspended) return;

            float now = Time.realtimeSinceStartup;
            if (now - _lastNavTime < NavRepeatDelay) return;

            var v = ctx.ReadValue<Vector2>();

            if (_isGridMode)
            {
                if (_gridRows == null || _gridRows.Length == 0) return;
                HandleGridMove(v, now);
            }
            else
            {
                if (_currentButtons.Length == 0) return;
                HandleListMove(v, now);
            }
        }

        private void HandleListMove(Vector2 v, float now)
        {
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

        private void HandleGridMove(Vector2 v, float now)
        {
            int oldRow = _gridRow;
            int oldCol = _gridCol;

            if (v.y > 0.5f)
                _gridRow = Mathf.Max(0, _gridRow - 1);
            else if (v.y < -0.5f)
                _gridRow = Mathf.Min(_gridRows.Length - 1, _gridRow + 1);

            if (v.x > 0.5f)
                _gridCol++;
            else if (v.x < -0.5f)
                _gridCol = Mathf.Max(0, _gridCol - 1);

            // 行が変わった場合、列をクランプ
            if (_gridRow != oldRow)
            {
                var row = _gridRows[_gridRow];
                _gridCol = Mathf.Clamp(_gridCol, 0, row.Length - 1);
            }
            else
            {
                var row = _gridRows[_gridRow];
                _gridCol = Mathf.Clamp(_gridCol, 0, row.Length - 1);
            }

            if (_gridRow != oldRow || _gridCol != oldCol)
            {
                _lastNavTime = now;
                ClearGridFocus(oldRow, oldCol);
                UpdateGridFocus();
            }
        }

        private void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (_suspended) return;

            if (_isGridMode)
            {
                if (_gridRows == null || _gridRows.Length == 0) return;
                if (_gridRow < 0 || _gridRow >= _gridRows.Length) return;
                var row = _gridRows[_gridRow];
                if (_gridCol < 0 || _gridCol >= row.Length) return;
                row[_gridCol].OnSubmit?.Invoke();
            }
            else
            {
                if (_currentActions.Length == 0) return;
                if (_focusIndex < 0 || _focusIndex >= _currentActions.Length) return;

                var btn = _focusIndex < _currentButtons.Length ? _currentButtons[_focusIndex] : null;
                if (btn != null && !btn.enabledSelf) return;

                _currentActions[_focusIndex]?.Invoke();
            }
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

        // ── 1D フォーカス ──

        // フォーカス中の要素の元スタイルを保持
        private VisualElement _inlineFocusedElement;
        private StyleColor _savedBg;
        private StyleColor _savedBorderTop;
        private StyleColor _savedBorderBottom;
        private StyleColor _savedBorderLeft;
        private StyleColor _savedBorderRight;
        private StyleScale _savedScale;

        private void UpdateFocus()
        {
            ClearListFocusHighlight();
            if (_focusIndex >= 0 && _focusIndex < _currentButtons.Length)
            {
                var btn = _currentButtons[_focusIndex];
                if (btn == null) return;
                btn.AddToClassList("title-btn--focused");
                ApplyInlineFocus(btn);
            }
        }

        private void ClearListFocusHighlight()
        {
            ClearInlineFocus();
            foreach (var btn in _currentButtons)
                btn?.RemoveFromClassList("title-btn--focused");
        }

        private void ApplyInlineFocus(VisualElement el)
        {
            ClearInlineFocus();
            _inlineFocusedElement = el;
            // 元のスタイルを保存
            _savedBg = el.style.backgroundColor;
            _savedBorderTop = el.style.borderTopColor;
            _savedBorderBottom = el.style.borderBottomColor;
            _savedBorderLeft = el.style.borderLeftColor;
            _savedBorderRight = el.style.borderRightColor;
            _savedScale = el.style.scale;
            // hover と同じスタイルを inline で適用
            var coinColor = new Color(0.941f, 0.753f, 0.251f, 1f);
            el.style.backgroundColor = new Color(0.314f, 0.275f, 0.431f, 0.9f);
            el.style.borderTopColor = coinColor;
            el.style.borderBottomColor = coinColor;
            el.style.borderLeftColor = coinColor;
            el.style.borderRightColor = coinColor;
            el.style.scale = new Scale(new Vector3(1.03f, 1.03f, 1f));
        }

        private void ClearInlineFocus()
        {
            if (_inlineFocusedElement == null) return;
            _inlineFocusedElement.style.backgroundColor = _savedBg;
            _inlineFocusedElement.style.borderTopColor = _savedBorderTop;
            _inlineFocusedElement.style.borderBottomColor = _savedBorderBottom;
            _inlineFocusedElement.style.borderLeftColor = _savedBorderLeft;
            _inlineFocusedElement.style.borderRightColor = _savedBorderRight;
            _inlineFocusedElement.style.scale = _savedScale;
            _inlineFocusedElement = null;
        }

        // ── 2D グリッドフォーカス ──

        private void UpdateGridFocus()
        {
            if (_gridRows == null || _gridRow < 0 || _gridRow >= _gridRows.Length) return;
            var row = _gridRows[_gridRow];
            if (_gridCol < 0 || _gridCol >= row.Length) return;
            var item = row[_gridCol];
            var cls = !string.IsNullOrEmpty(item.FocusClass) ? item.FocusClass : "title-btn--focused";
            item.Element?.AddToClassList(cls);
            if (item.Element != null) ApplyInlineFocus(item.Element);
        }

        private void ClearGridFocus(int row, int col)
        {
            if (_gridRows == null || row < 0 || row >= _gridRows.Length) return;
            var r = _gridRows[row];
            if (col < 0 || col >= r.Length) return;
            var item = r[col];
            var cls = !string.IsNullOrEmpty(item.FocusClass) ? item.FocusClass : "title-btn--focused";
            item.Element?.RemoveFromClassList(cls);
        }

        private void ClearAllFocus()
        {
            // inline style をクリア
            ClearInlineFocus();

            // 1D classes
            foreach (var btn in _currentButtons)
                btn?.RemoveFromClassList("title-btn--focused");

            // 2D classes
            if (_gridRows != null)
            {
                for (int r = 0; r < _gridRows.Length; r++)
                {
                    if (_gridRows[r] == null) continue;
                    for (int c = 0; c < _gridRows[r].Length; c++)
                        ClearGridFocus(r, c);
                }
            }
        }

        public void Dispose()
        {
            foreach (var (map, moveCb, submitCb, cancelCb) in _bindings)
            {
                var moveAction = map.FindAction("Move");
                var breakAction = map.FindAction("BreakBombHold");
                var fireAction = map.FindAction("FireBombHold");

                if (moveAction != null) { moveAction.performed -= moveCb; moveAction.started -= moveCb; }
                if (fireAction != null) fireAction.performed -= submitCb;
                if (breakAction != null) breakAction.performed -= cancelCb;
            }
            _bindings.Clear();

            _gamepadSubmitAction?.Disable();
            _gamepadSubmitAction?.Dispose();
            _gamepadCancelAction?.Disable();
            _gamepadCancelAction?.Dispose();
        }
    }
}
