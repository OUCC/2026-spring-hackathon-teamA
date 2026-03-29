namespace FloorBreaker.MatchFlow.Application
{
    /// <summary>
    /// ロビー設定を MatchModeConfig に適用する Application 層の UseCase。
    /// Presenter が MatchModeConfig を直接変更する代わりに、このクラスを経由する。
    /// </summary>
    public sealed class LobbyConfigUseCase
    {
        private readonly MatchModeConfig _config;

        public LobbyConfigUseCase(MatchModeConfig config)
        {
            _config = config;
        }

        /// <summary>ホストとしてルーム作成成功後に呼ぶ。</summary>
        public void ConfigureAsHost(string roomCode)
        {
            _config.IsOnline = true;
            _config.IsHost = true;
            _config.RoomCode = roomCode;
        }

        /// <summary>クライアントとしてルーム参加成功後に呼ぶ。</summary>
        public void ConfigureAsClient(string roomCode)
        {
            _config.IsOnline = true;
            _config.IsHost = false;
            _config.RoomCode = roomCode;
        }

        /// <summary>ホストからのロビー設定同期を適用する（クライアント側）。</summary>
        public void ApplyLobbySync(int playerCount, bool[] cpuSlots, string stageName)
        {
            _config.PlayerCount = playerCount;
            _config.IsCpuSlot = cpuSlots;
            if (!string.IsNullOrEmpty(stageName))
                _config.SelectedStageName = stageName;
        }

        /// <summary>マッチ開始時の最終設定を適用する（クライアント側）。</summary>
        public void ApplyMatchStart(int playerCount, bool[] cpuSlots, string stageName)
        {
            _config.PlayerCount = playerCount;
            _config.IsCpuSlot = cpuSlots;
            _config.SelectedStageName = stageName;
            _config.IsOnline = true;
            _config.IsHost = false;
        }

        /// <summary>オンライン状態をリセットする。</summary>
        public void ResetOnline()
        {
            _config.ResetOnlineState();
        }
    }
}
