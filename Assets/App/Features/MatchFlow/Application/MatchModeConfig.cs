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

        /// <summary>選択されたステージ名。null or "" ならデフォルト。</summary>
        public string SelectedStageName { get; set; }

        /// <summary>リザルトから「設定に戻る」で遷移した場合 true。</summary>
        public bool StartInSetupMode { get; set; }

        public bool IsCpuPlayer => System.Array.Exists(IsCpuSlot, x => x);
        public bool IsCpuAt(int index) => index >= 0 && index < IsCpuSlot.Length && IsCpuSlot[index];
    }
}
