namespace FloorBreaker.MatchFlow.Application
{
    /// <summary>
    /// タイトル画面で選択されたモードをシーン遷移をまたいで保持する。
    /// static だが書き込みはタイトル画面のボタン押下時のみ。
    /// </summary>
    public static class MatchModeSelection
    {
        public static bool IsCpuPlayer { get; set; }
    }
}
