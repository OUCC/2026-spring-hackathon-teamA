using UnityEngine.InputSystem;

namespace FloorBreaker.Input.Application
{
    /// <summary>
    /// InputActionAsset から実際のバインドを読み取ってキーラベルを返す。
    /// </summary>
    public static class KeyLabelResolver
    {
        /// <summary>
        /// 指定プレイヤーの Gameplay マップからボム・移動キーのラベルを取得する。
        /// </summary>
        public static (string fireKey, string breakKey, string moveKeys) GetBombKeyLabels(
            InputActionAsset actions, int playerIndex)
        {
            var mapName = $"Gameplay_P{playerIndex + 1}";
            var map = actions?.FindActionMap(mapName);
            if (map == null) return ("", "", "");

            string fireKey = GetActionDisplayString(map, "FireBombHold");
            string breakKey = GetActionDisplayString(map, "BreakBombHold");
            string moveKeys = GetMoveDisplayString(map);

            return (fireKey, breakKey, moveKeys);
        }

        /// <summary>
        /// 指定プレイヤーの AimLock キーラベルを取得する。
        /// </summary>
        public static string GetAimKeyLabel(InputActionAsset actions, int playerIndex)
        {
            var mapName = $"Gameplay_P{playerIndex + 1}";
            var map = actions?.FindActionMap(mapName);
            if (map == null) return "";
            return GetActionDisplayString(map, "AimLock");
        }

        private static string GetActionDisplayString(InputActionMap map, string actionName)
        {
            var action = map.FindAction(actionName);
            if (action == null) return "";

            // 非コンポジット・非ゲームパッドの最初のバインドを探す
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                if (IsGamepadPath(b.effectivePath)) continue;
                return action.GetBindingDisplayString(i,
                    InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
            }
            // フォールバック: 最初のバインド
            return bindings.Count > 0
                ? action.GetBindingDisplayString(0,
                    InputBinding.DisplayStringOptions.DontUseShortDisplayNames)
                : "";
        }

        private static string GetMoveDisplayString(InputActionMap map)
        {
            var action = map.FindAction("Move");
            if (action == null) return "";

            // コンポジットの各パーツ (Up/Down/Left/Right) を探す
            var bindings = action.bindings;
            string up = "", down = "", left = "", right = "";
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (!b.isPartOfComposite) continue;
                if (IsGamepadPath(b.effectivePath)) continue;

                var display = action.GetBindingDisplayString(i,
                    InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
                var partName = b.name?.ToLowerInvariant() ?? "";
                if (partName == "up") up = display;
                else if (partName == "down") down = display;
                else if (partName == "left") left = display;
                else if (partName == "right") right = display;
            }

            if (!string.IsNullOrEmpty(up))
                return $"{up}{left}{down}{right}";
            return "";
        }

        private static bool IsGamepadPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (path.StartsWith("<Gamepad>") || path.StartsWith("<XInputController>"));
        }
    }
}
