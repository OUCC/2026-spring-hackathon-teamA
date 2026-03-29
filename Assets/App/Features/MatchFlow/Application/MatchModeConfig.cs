using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.MatchFlow.Application
{
    /// <summary>
    /// タイトル画面 / マッチセットアップで選択された設定をシーン遷移をまたいで保持する。
    /// ProjectLifetimeScope に Singleton 登録され、DI 経由でアクセスする。
    /// </summary>
    public sealed class MatchModeConfig
    {
        public int PlayerCount { get; set; } = 2;
        public bool[] IsCpuSlot { get; set; } = { false, false, false, false };

        /// <summary>各スロットのデバイス種別。シーン遷移後の InputInitializer で使用。</summary>
        public DeviceType[] DeviceTypes { get; set; } = new DeviceType[4];

        /// <summary>各スロットのゲームパッドインデックス (DeviceType.Gamepad の場合のみ有効, -1=未割当)。</summary>
        public int[] GamepadIndices { get; set; } = { -1, -1, -1, -1 };

        /// <summary>選択されたステージ名。null or "" ならデフォルト。</summary>
        public string SelectedStageName { get; set; }

        /// <summary>リザルトから「設定に戻る」で遷移した場合 true。</summary>
        public bool StartInSetupMode { get; set; }

        // --- オンラインモード ---

        /// <summary>オンライン対戦中かどうか。</summary>
        public bool IsOnline { get; set; }

        /// <summary>このクライアントがホストかどうか（IsOnline 時のみ有効）。</summary>
        public bool IsHost { get; set; }

        /// <summary>現在のルームコード。</summary>
        public string RoomCode { get; set; }

        /// <summary>オンライン関連の状態をリセットする。</summary>
        public void ResetOnlineState()
        {
            IsOnline = false;
            IsHost = false;
            RoomCode = null;
        }

        public bool IsCpuPlayer => System.Array.Exists(IsCpuSlot, x => x);
        public bool IsCpuAt(int index) => index >= 0 && index < IsCpuSlot.Length && IsCpuSlot[index];

        public bool IsAllCpu
        {
            get
            {
                for (int i = 0; i < PlayerCount; i++)
                    if (!IsCpuSlot[i]) return false;
                return true;
            }
        }

        /// <summary>スロットのデバイス割り当てをクリアする。</summary>
        public void ClearDevice(int slot)
        {
            DeviceTypes[slot] = DeviceType.None;
            GamepadIndices[slot] = -1;
        }

        /// <summary>特定の DeviceType が他スロットで使用中か判定。</summary>
        public bool IsDeviceTypeAssigned(DeviceType type, int excludeSlot = -1, int gamepadIndex = -1)
        {
            for (int i = 0; i < PlayerCount; i++)
            {
                if (i == excludeSlot) continue;
                if (IsCpuSlot[i]) continue;
                if (type == DeviceType.Gamepad)
                {
                    if (DeviceTypes[i] == DeviceType.Gamepad && GamepadIndices[i] == gamepadIndex) return true;
                }
                else
                {
                    if (DeviceTypes[i] == type) return true;
                }
            }
            return false;
        }
    }
}
