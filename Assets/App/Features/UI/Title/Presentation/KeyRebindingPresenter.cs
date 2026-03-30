using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using FloorBreaker.Input.Infrastructure;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// キーコンフィグオーバーレイの表示・リバインド操作を管理する。
    /// </summary>
    public sealed class KeyRebindingPresenter
    {
        private readonly KeyRebindingService _service;
        private readonly VisualElement _overlay;
        private readonly VisualElement _p1Container;
        private readonly VisualElement _p2Container;
        private readonly Button _resetButton;
        private readonly Button _closeButton;
        private readonly Action _onBindingsChanged;

        private readonly List<(Button keyButton, InputAction action, int bindingIndex)> _bindingRows = new();
        private InputActionRebindingExtensions.RebindingOperation _activeRebind;

        /// <summary>リバインド開始時に呼ばれる（TitlePresenter が Suspend するため）。</summary>
        public Action OnRebindStarted { get; set; }
        /// <summary>リバインド完了/キャンセル時に呼ばれる（TitlePresenter が Resume するため）。</summary>
        public Action OnRebindEnded { get; set; }

        public KeyRebindingPresenter(
            KeyRebindingService service,
            VisualElement overlay,
            VisualElement p1Container,
            VisualElement p2Container,
            Button resetButton,
            Button closeButton,
            Action onBindingsChanged)
        {
            _service = service;
            _overlay = overlay;
            _p1Container = p1Container;
            _p2Container = p2Container;
            _resetButton = resetButton;
            _closeButton = closeButton;
            _onBindingsChanged = onBindingsChanged;

            _resetButton?.RegisterCallback<ClickEvent>(_ => OnResetAll());
            _closeButton?.RegisterCallback<ClickEvent>(_ => Hide());
        }

        public void Show()
        {
            PopulateBindings();
            _overlay.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _activeRebind?.Cancel();
            _activeRebind = null;
            _overlay.style.display = DisplayStyle.None;
        }

        private void PopulateBindings()
        {
            _bindingRows.Clear();
            ClearContainer(_p1Container);
            ClearContainer(_p2Container);

            PopulateMap("Gameplay_P1", _p1Container);
            PopulateMap("Gameplay_P2", _p2Container);
        }

        private void PopulateMap(string mapName, VisualElement container)
        {
            var bindings = _service.GetKeyboardBindings(mapName);
            foreach (var info in bindings)
            {
                var row = new VisualElement();
                row.AddToClassList("keyconfig-row");

                var label = new Label(info.Label);
                label.AddToClassList("keyconfig-row__label");

                var keyButton = new Button();
                keyButton.text = info.DisplayString;
                keyButton.AddToClassList("keyconfig-row__key");

                var action = info.Action;
                int bindingIndex = info.BindingIndex;

                keyButton.RegisterCallback<ClickEvent>(_ =>
                    OnBindingClicked(keyButton, action, bindingIndex));

                row.Add(label);
                row.Add(keyButton);
                container.Add(row);

                _bindingRows.Add((keyButton, action, bindingIndex));
            }
        }

        /// <summary>
        /// リバインド行のボタン一覧 + Reset + Close を返す。
        /// SetMenu で使うための Button[] と Action[] のペアを生成する。
        /// </summary>
        public (Button[] buttons, Action[] actions) GetAllButtons()
        {
            var buttons = new List<Button>();
            var actions = new List<Action>();

            foreach (var (keyButton, action, bindingIndex) in _bindingRows)
            {
                var kb = keyButton;
                var act = action;
                var bi = bindingIndex;
                buttons.Add(kb);
                actions.Add(() => OnBindingClicked(kb, act, bi));
            }

            if (_resetButton != null)
            {
                buttons.Add(_resetButton);
                actions.Add(() => OnResetAll());
            }

            if (_closeButton != null)
            {
                buttons.Add(_closeButton);
                // Close はアクション側で制御するため null
                actions.Add(null);
            }

            return (buttons.ToArray(), actions.ToArray());
        }

        private void OnBindingClicked(Button keyButton, InputAction action, int bindingIndex)
        {
            // 既にリバインド中なら無視
            if (_activeRebind != null) return;

            keyButton.text = "...";
            keyButton.AddToClassList("keyconfig-row__key--listening");
            OnRebindStarted?.Invoke();

            _activeRebind = _service.StartRebinding(action, bindingIndex,
                onComplete: () =>
                {
                    keyButton.RemoveFromClassList("keyconfig-row__key--listening");
                    keyButton.text = _service.GetBindingDisplayString(action, bindingIndex);
                    _activeRebind = null;
                    RefreshAllDisplayStrings();
                    _onBindingsChanged?.Invoke();
                    OnRebindEnded?.Invoke();
                },
                onCancel: () =>
                {
                    keyButton.RemoveFromClassList("keyconfig-row__key--listening");
                    keyButton.text = _service.GetBindingDisplayString(action, bindingIndex);
                    _activeRebind = null;
                    OnRebindEnded?.Invoke();
                });
        }

        private void OnResetAll()
        {
            _activeRebind?.Cancel();
            _activeRebind = null;
            _service.ResetAllBindings();
            RefreshAllDisplayStrings();
            _onBindingsChanged?.Invoke();
        }

        private void RefreshAllDisplayStrings()
        {
            foreach (var (keyButton, action, bindingIndex) in _bindingRows)
            {
                keyButton.text = _service.GetBindingDisplayString(action, bindingIndex);
            }
        }

        private static void ClearContainer(VisualElement container)
        {
            if (container == null) return;
            // プレイヤーラベル（最初の子）は残す
            while (container.childCount > 1)
                container.RemoveAt(container.childCount - 1);
        }
    }
}
