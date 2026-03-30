using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.MatchFlow.Application;

namespace FloorBreaker.UI.Result.Presentation
{
    /// <summary>
    /// リザルト画面を駆動する Presenter。Human プレイヤーのみ表示対応。
    /// Gameplay マップの Move/Fire/Break でキーボードナビゲーションを行う。
    /// </summary>
    public sealed class ResultPresenter : IDisposable
    {
        private readonly IDisposable _phaseSub;
        private readonly IDisposable _winnerSub;
        private readonly ResultView _view;
        private readonly Action[] _buttonActions;
        private readonly Button[] _buttons;
        private int _focusIndex;
        private float _lastNavTime;
        private const float NavRepeatDelay = 0.2f;
        private bool _isResultPhase;

        // Gameplay コールバック参照（Dispose 用）
        private readonly List<(InputActionMap map,
            Action<InputAction.CallbackContext> moveCb,
            Action<InputAction.CallbackContext> submitCb)> _inputBindings = new();

        // ゲームパッド A/B
        private InputAction _gamepadSubmitAction;

        // inline focus
        private VisualElement _focusedElement;
        private StyleColor _savedBg, _savedBorderTop, _savedBorderBottom, _savedBorderLeft, _savedBorderRight;
        private StyleScale _savedScale;

        public ResultPresenter(
            ResultView view,
            MatchClock clock,
            MatchEndUseCase matchEnd,
            int playerCount,
            ISceneTransitionService sceneTransition,
            MatchModeConfig modeConfig,
            InputActionAsset inputActions,
            int[] humanIndices = null)
        {
            _view = view;

            // ボタンとアクションを構築（ペインがある場合のみ）
            _buttonActions = new Action[]
            {
                () => sceneTransition.LoadMatchAsync().Forget(e => Debug.LogException(e)),
                () => { modeConfig.StartInSetupMode = true; sceneTransition.LoadTitleAsync().Forget(e => Debug.LogException(e)); },
                () => sceneTransition.LoadTitleAsync().Forget(e => Debug.LogException(e)),
            };
            _buttons = view.PaneCount > 0
                ? new[] { view.GetRematchButton(0), view.GetSetupButton(0), view.GetTitleButton(0) }
                : System.Array.Empty<Button>();

            _phaseSub = clock.CurrentPhase.Subscribe(phase =>
            {
                _isResultPhase = phase == GamePhase.Result;
                if (_isResultPhase)
                {
                    view.Show();
                    _focusIndex = 0;
                    UpdateFocus();
                }
                else
                {
                    view.Hide();
                    ClearFocus();
                }
            });

            _winnerSub = matchEnd.Winner.Subscribe(winner =>
            {
                if (!winner.HasValue) return;
                view.SetResult(winner, playerCount, humanIndices);
            });

            // 全ペインのボタンにクリックコールバックを接続
            for (int i = 0; i < view.PaneCount; i++)
            {
                view.GetRematchButton(i).clicked += _buttonActions[0];
                view.GetTitleButton(i).clicked += _buttonActions[2];
                int pane = i;
                view.GetSetupButton(i).clicked += _buttonActions[1];
            }

            // Gameplay マップからキーボードナビを接続
            if (inputActions != null)
            {
                for (int p = 1; p <= 4; p++)
                {
                    var map = inputActions.FindActionMap($"Gameplay_P{p}");
                    if (map == null) continue;

                    var moveAction = map.FindAction("Move");
                    var fireAction = map.FindAction("FireBombHold");

                    if (moveAction == null) continue;

                    Action<InputAction.CallbackContext> moveCb = ctx => OnMove(ctx);
                    Action<InputAction.CallbackContext> submitCb = ctx => OnSubmit();

                    moveAction.performed += moveCb;
                    moveAction.started += moveCb;
                    if (fireAction != null) fireAction.performed += submitCb;

                    _inputBindings.Add((map, moveCb, submitCb));
                }

                // ゲームパッド A ボタン
                _gamepadSubmitAction = new InputAction("ResultSubmit", binding: "<Gamepad>/buttonSouth");
                _gamepadSubmitAction.performed += ctx => OnSubmit();
                _gamepadSubmitAction.Enable();
            }
        }

        private void OnMove(InputAction.CallbackContext ctx)
        {
            if (!_isResultPhase) return;

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
                _focusIndex = Mathf.Min(_buttons.Length - 1, _focusIndex + 1);
                _lastNavTime = now;
                UpdateFocus();
            }
        }

        private void OnSubmit()
        {
            if (!_isResultPhase) return;
            if (_focusIndex < 0 || _focusIndex >= _buttonActions.Length) return;
            _buttonActions[_focusIndex]?.Invoke();
        }

        private void UpdateFocus()
        {
            ClearFocus();
            if (_focusIndex >= 0 && _focusIndex < _buttons.Length)
            {
                var btn = _buttons[_focusIndex];
                _focusedElement = btn;
                _savedBg = btn.style.backgroundColor;
                _savedBorderTop = btn.style.borderTopColor;
                _savedBorderBottom = btn.style.borderBottomColor;
                _savedBorderLeft = btn.style.borderLeftColor;
                _savedBorderRight = btn.style.borderRightColor;
                _savedScale = btn.style.scale;

                var gold = new Color(0.941f, 0.753f, 0.251f, 1f);
                btn.style.backgroundColor = new Color(0.314f, 0.275f, 0.431f, 0.9f);
                btn.style.borderTopColor = gold;
                btn.style.borderBottomColor = gold;
                btn.style.borderLeftColor = gold;
                btn.style.borderRightColor = gold;
                btn.style.scale = new Scale(new Vector3(1.03f, 1.03f, 1f));
            }
        }

        private void ClearFocus()
        {
            if (_focusedElement == null) return;
            _focusedElement.style.backgroundColor = _savedBg;
            _focusedElement.style.borderTopColor = _savedBorderTop;
            _focusedElement.style.borderBottomColor = _savedBorderBottom;
            _focusedElement.style.borderLeftColor = _savedBorderLeft;
            _focusedElement.style.borderRightColor = _savedBorderRight;
            _focusedElement.style.scale = _savedScale;
            _focusedElement = null;
        }

        public void Dispose()
        {
            _phaseSub.Dispose();
            _winnerSub.Dispose();

            foreach (var (map, moveCb, submitCb) in _inputBindings)
            {
                var moveAction = map.FindAction("Move");
                var fireAction = map.FindAction("FireBombHold");
                if (moveAction != null) { moveAction.performed -= moveCb; moveAction.started -= moveCb; }
                if (fireAction != null) fireAction.performed -= submitCb;
            }
            _inputBindings.Clear();

            _gamepadSubmitAction?.Disable();
            _gamepadSubmitAction?.Dispose();
        }
    }
}
