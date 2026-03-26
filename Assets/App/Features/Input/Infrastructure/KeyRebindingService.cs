using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FloorBreaker.Input.Infrastructure
{
    /// <summary>
    /// Input System のリバインド API をラップし、PlayerPrefs で永続化する。
    /// </summary>
    public sealed class KeyRebindingService
    {
        private const string PlayerPrefsKey = "InputBindingOverrides";
        private readonly InputActionAsset _actions;

        public KeyRebindingService(InputActionAsset actions)
        {
            _actions = actions;
        }

        /// <summary>PlayerPrefs から保存済みオーバーライドを読み込んで適用する。</summary>
        public void LoadOverrides()
        {
            var json = PlayerPrefs.GetString(PlayerPrefsKey, "");
            if (!string.IsNullOrEmpty(json))
                _actions.LoadBindingOverridesFromJson(json);
        }

        /// <summary>現在のオーバーライドを PlayerPrefs に保存する。</summary>
        public void SaveOverrides()
        {
            var json = _actions.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 指定アクション・バインドのインタラクティブリバインドを開始する。
        /// 戻り値の RebindingOperation は呼び出し側で保持し、キャンセル時に Dispose すること。
        /// </summary>
        public InputActionRebindingExtensions.RebindingOperation StartRebinding(
            InputAction action, int bindingIndex, Action onComplete, Action onCancel)
        {
            // リバインド中は一時的にアクションを無効化
            action.Disable();

            var operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("Mouse")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op =>
                {
                    action.Enable();
                    SaveOverrides();
                    onComplete?.Invoke();
                    op.Dispose();
                })
                .OnCancel(op =>
                {
                    action.Enable();
                    onCancel?.Invoke();
                    op.Dispose();
                })
                .Start();

            return operation;
        }

        /// <summary>指定バインドをデフォルトにリセットする。</summary>
        public void ResetBinding(InputAction action, int bindingIndex)
        {
            action.RemoveBindingOverride(bindingIndex);
            SaveOverrides();
        }

        /// <summary>全バインドをデフォルトにリセットする。</summary>
        public void ResetAllBindings()
        {
            foreach (var map in _actions.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    action.RemoveAllBindingOverrides();
                }
            }
            SaveOverrides();
        }

        /// <summary>バインドの表示用文字列を取得する。</summary>
        public string GetBindingDisplayString(InputAction action, int bindingIndex)
        {
            return action.GetBindingDisplayString(bindingIndex,
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        }

        /// <summary>
        /// 指定アクションマップのキーボードバインド情報を列挙する。
        /// コンポジット（2DVector 等）の場合は各パーツを個別に返す。
        /// </summary>
        public List<BindingInfo> GetKeyboardBindings(string actionMapName)
        {
            var result = new List<BindingInfo>();
            var map = _actions.FindActionMap(actionMapName);
            if (map == null) return result;

            foreach (var action in map.actions)
            {
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];

                    // コンポジット親はスキップ
                    if (binding.isComposite) continue;

                    // ゲームパッドバインドはスキップ
                    if (IsGamepadBinding(binding)) continue;

                    string label;
                    if (binding.isPartOfComposite)
                    {
                        label = GetCompositePartLabel(action.name, binding.name);
                    }
                    else
                    {
                        label = GetActionLabel(action.name);
                    }

                    result.Add(new BindingInfo
                    {
                        Action = action,
                        BindingIndex = i,
                        Label = label,
                        DisplayString = GetBindingDisplayString(action, i)
                    });
                }
            }

            return result;
        }

        private static bool IsGamepadBinding(InputBinding binding)
        {
            var path = binding.effectivePath;
            if (string.IsNullOrEmpty(path)) return false;
            return path.StartsWith("<Gamepad>") || path.StartsWith("<XInputController>");
        }

        private static string GetActionLabel(string actionName)
        {
            return actionName switch
            {
                "Move" => "移動",
                "BreakBombHold" => "ブレークボム",
                "FireBombHold" => "炎ボム",
                "AimLock" => "照準",
                "Navigate" => "ナビゲート",
                "Submit" => "決定",
                "Skip" => "スキップ",
                "Reroll" => "リロール",
                _ => actionName,
            };
        }

        private static string GetCompositePartLabel(string actionName, string partName)
        {
            string actionLabel = GetActionLabel(actionName);
            string partLabel = partName?.ToLower() switch
            {
                "up" => "上",
                "down" => "下",
                "left" => "左",
                "right" => "右",
                _ => partName,
            };
            return $"{actionLabel} {partLabel}";
        }

        public struct BindingInfo
        {
            public InputAction Action;
            public int BindingIndex;
            public string Label;
            public string DisplayString;
        }
    }
}
