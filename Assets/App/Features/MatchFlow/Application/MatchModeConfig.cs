namespace FloorBreaker.MatchFlow.Application
{
    /// <summary>
    /// タイトル画面で選択されたモードをシーン遷移をまたいで保持する。
    /// ProjectLifetimeScope に Singleton 登録され、DI 経由でアクセスする。
    /// </summary>
    public sealed class MatchModeConfig
    {
        public bool IsCpuPlayer { get; set; }
    }
}
