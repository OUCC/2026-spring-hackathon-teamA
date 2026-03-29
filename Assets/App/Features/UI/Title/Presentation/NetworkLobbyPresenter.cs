using System;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using R3;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Network.Infrastructure;
using FloorBreaker.UI.RuntimeUI.Documents;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// ロビー UI の制御を担当する Presenter。
    /// ホスト/クライアント両方のフローを管理する。
    /// </summary>
    public sealed class NetworkLobbyPresenter : IDisposable
    {
        private static readonly string[] PlayerColors = { "p1", "p2", "p3", "p4" };
        private static readonly string[] PlayerLabels = { "P1", "P2", "P3", "P4" };

        private readonly TitleUIDocument _doc;
        private readonly NetworkConnectionService _connectionService;
        private readonly MatchModeConfig _modeConfig;
        private readonly IAudioService _audio;
        private readonly ISceneTransitionService _sceneTransition;
        private readonly IRandomProvider _random;

        private readonly CompositeDisposable _subscriptions = new();
        private bool _isHost;

        public NetworkLobbyPresenter(
            TitleUIDocument doc,
            NetworkConnectionService connectionService,
            MatchModeConfig modeConfig,
            IAudioService audio,
            ISceneTransitionService sceneTransition,
            IRandomProvider random)
        {
            _doc = doc;
            _connectionService = connectionService;
            _modeConfig = modeConfig;
            _audio = audio;
            _sceneTransition = sceneTransition;
            _random = random;

            // 接続状態の変更を購読
            _connectionService.ConnectedPlayerCount
                .Subscribe(count => UpdatePlayerList(count))
                .AddTo(_subscriptions);

            _connectionService.ErrorOccurred
                .Subscribe(msg =>
                {
                    if (_doc.LobbyStatusLabel != null)
                        _doc.LobbyStatusLabel.text = msg;
                })
                .AddTo(_subscriptions);
        }

        /// <summary>ホストとしてロビーに入る。</summary>
        public void EnterAsHost()
        {
            _isHost = true;
            SetupHostUI();
            CreateRoomAsync().Forget();
        }

        /// <summary>クライアントとしてロビーに入る。</summary>
        public void EnterAsClient()
        {
            _isHost = false;
            SetupClientUI();
        }

        /// <summary>ロビーから離脱する。</summary>
        public async UniTask LeaveAsync()
        {
            await _connectionService.ShutdownAsync();
            _modeConfig.ResetOnlineState();
        }

        /// <summary>ホストがマッチを開始する。</summary>
        public void StartMatch()
        {
            if (!_isHost) return;

            _modeConfig.IsOnline = true;
            _modeConfig.IsHost = true;

            // LobbyController 経由でクライアントに開始シグナルと設定を送信
            var lobby = _connectionService.LobbyController;
            if (lobby != null)
            {
                lobby.SetLobbyConfig(
                    _modeConfig.PlayerCount,
                    _modeConfig.IsCpuSlot,
                    _modeConfig.SelectedStageName);
                lobby.StartMatch();
            }

            // クライアントにシグナルが届くまで少し待ってからシーン遷移
            HostStartMatchAsync().Forget();
        }

        private async UniTaskVoid HostStartMatchAsync()
        {
            // Fusion が [Networked] 変更を送信する時間を確保（数 Tick 分）
            await UniTask.Delay(500);
            _sceneTransition.LoadMatchAsync().Forget();
        }

        /// <summary>クライアントがルームコード入力後に接続する。</summary>
        public void JoinWithCode()
        {
            var input = _doc.LobbyRoomCodeInput?.value ?? string.Empty;
            var code = RoomCodeService.NormalizeInput(input);

            if (!RoomCodeService.IsValid(code))
            {
                _doc.LobbyStatusLabel.text = "コードは5文字の英数字です";
                return;
            }

            JoinRoomAsync(code).Forget();
        }

        public void Dispose()
        {
            LobbyController.OnLobbySpawned -= OnLobbySpawned;
            _subscriptions.Dispose();
        }

        // --- Private ---

        private void SetupHostUI()
        {
            // ホスト: コード表示、START ボタン表示
            _doc.LobbyRoomCodeDisplay.style.display = DisplayStyle.Flex;
            _doc.LobbyJoinSection.style.display = DisplayStyle.None;
            _doc.LobbyStartButton.style.display = DisplayStyle.Flex;
            _doc.LobbyStatusLabel.text = "接続中...";
            ClearPlayerList();
        }

        private void SetupClientUI()
        {
            // クライアント: コード入力、START ボタン非表示
            _doc.LobbyRoomCodeDisplay.style.display = DisplayStyle.None;
            _doc.LobbyJoinSection.style.display = DisplayStyle.Flex;
            _doc.LobbyStartButton.style.display = DisplayStyle.None;
            _doc.LobbyStatusLabel.text = "ルームコードを入力してください";
            _doc.LobbyRoomCodeInput.value = string.Empty;
            ClearPlayerList();
        }

        private async UniTaskVoid CreateRoomAsync()
        {
            try
            {
                var roomCode = RoomCodeService.GenerateRoomCode(_random);
                _doc.LobbyRoomCodeDisplay.text = roomCode;
                _doc.LobbyStatusLabel.text = "部屋を作成中...";

                await _connectionService.CreateRoomAsync(roomCode, _modeConfig.PlayerCount);

                _modeConfig.IsOnline = true;
                _modeConfig.IsHost = true;
                _modeConfig.RoomCode = roomCode;

                _doc.LobbyStatusLabel.text = "相手を待っています...";
            }
            catch (Exception ex)
            {
                _doc.LobbyStatusLabel.text = ex.Message;
                _doc.LobbyRoomCodeDisplay.text = "-----";
            }
        }

        private async UniTaskVoid JoinRoomAsync(string roomCode)
        {
            try
            {
                _doc.LobbyStatusLabel.text = "接続中...";

                await _connectionService.JoinRoomAsync(roomCode);

                _modeConfig.IsOnline = true;
                _modeConfig.IsHost = false;
                _modeConfig.RoomCode = roomCode;

                // 接続成功: コード表示に切り替え
                _doc.LobbyJoinSection.style.display = DisplayStyle.None;
                _doc.LobbyRoomCodeDisplay.style.display = DisplayStyle.Flex;
                _doc.LobbyRoomCodeDisplay.text = roomCode;
                _doc.LobbyStatusLabel.text = "接続完了。ホストの開始を待っています...";

                // LobbyController のマッチ開始シグナルを購読
                SubscribeToMatchStart();
            }
            catch (Exception ex)
            {
                _doc.LobbyStatusLabel.text = ex.Message;
            }
        }

        private void UpdatePlayerList(int playerCount)
        {
            var container = _doc.LobbyPlayerList;
            if (container == null) return;

            container.Clear();

            for (int i = 0; i < _modeConfig.PlayerCount; i++)
            {
                bool isConnected = i < playerCount;
                bool isCpu = _modeConfig.IsCpuAt(i);

                var slot = new VisualElement();
                slot.AddToClassList("lobby-player-slot");
                if (!isConnected && !isCpu) slot.AddToClassList("lobby-player-slot--empty");

                var label = new Label(PlayerLabels[i]);
                label.AddToClassList("lobby-player-slot__label");
                label.AddToClassList($"lobby-player-slot__label--{PlayerColors[i]}");
                slot.Add(label);

                var name = new Label(isCpu ? "CPU" : isConnected ? (i == 0 ? "ホスト" : $"プレイヤー {i + 1}") : "(空席)");
                name.AddToClassList("lobby-player-slot__name");
                slot.Add(name);

                var status = new Label(isCpu ? "" : isConnected ? "接続済み" : "待機中");
                status.AddToClassList("lobby-player-slot__status");
                slot.Add(status);

                container.Add(slot);
            }
        }

        private void ClearPlayerList()
        {
            _doc.LobbyPlayerList?.Clear();
        }

        private void SubscribeToMatchStart()
        {
            // LobbyController が Spawned されたときに通知を受け取る
            LobbyController.OnLobbySpawned += OnLobbySpawned;

            // 既に存在する場合（接続が速かった場合のフォールバック）
            var existing = _connectionService.LobbyController;
            if (existing != null) BindToLobby(existing);
        }

        private bool _boundToLobby;

        private void OnLobbySpawned(LobbyController lobby)
        {
            BindToLobby(lobby);
        }

        private void BindToLobby(LobbyController lobby)
        {
            if (_boundToLobby || _isHost) return;
            _boundToLobby = true;
            lobby.OnMatchStartDetected += OnClientMatchStart;
        }

        private void OnClientMatchStart()
        {
            var lobby = _connectionService.LobbyController;
            if (lobby != null)
            {
                // ホストの設定をクライアント側の MatchModeConfig に反映
                _modeConfig.PlayerCount = lobby.PlayerCount;
                _modeConfig.IsCpuSlot = LobbyController.DecodeCpuSlots(lobby.CpuSlotMask, lobby.PlayerCount);
                _modeConfig.SelectedStageName = lobby.StageName.ToString();
            }

            _modeConfig.IsOnline = true;
            _modeConfig.IsHost = false;

            _sceneTransition.LoadMatchAsync().Forget();
        }
    }
}
