using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using R3;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Network.Infrastructure;
using FloorBreaker.ScriptableObjects.Configs;
using FloorBreaker.UI.RuntimeUI.Documents;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// ロビー UI の制御を担当する Presenter。
    /// ホスト/クライアント両方のフローを管理する。
    /// ホスト: スロット・ステージの操作 + LobbyController 同期。
    /// クライアント: 読み取り専用表示 + LobbyController 変更検知。
    /// </summary>
    public sealed class NetworkLobbyPresenter : IDisposable
    {
        private readonly TitleUIDocument _doc;
        private readonly NetworkConnectionService _connectionService;
        private readonly MatchModeConfig _modeConfig;
        private readonly IAudioService _audio;
        private readonly ISceneTransitionService _sceneTransition;
        private readonly IRandomProvider _random;

        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<(VisualElement card, string assetName)> _stageCards = new();
        private readonly Dictionary<string, StageConfig> _stageConfigs = new();
        private string _selectedStageName;
        private bool _isHost;
        private bool _boundToLobby;
        private bool _slotsInitialized;

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

            _connectionService.ConnectedPlayerCount
                .Subscribe(_ => RefreshAllSlotUI())
                .AddTo(_subscriptions);

            _connectionService.ErrorOccurred
                .Subscribe(msg =>
                {
                    if (_doc.LobbyStatusLabel != null)
                        _doc.LobbyStatusLabel.text = msg;
                })
                .AddTo(_subscriptions);
        }

        // ═══════════════════════════════════════════
        //  Public API
        // ═══════════════════════════════════════════

        public void EnterAsHost()
        {
            _isHost = true;
            SetupHostUI();
            InitializeSlotsAndStage();
            CreateRoomAsync().Forget();
        }

        public void EnterAsClient()
        {
            _isHost = false;
            SetupClientUI();
        }

        public async UniTask LeaveAsync()
        {
            await _connectionService.ShutdownAsync();
            _modeConfig.ResetOnlineState();
            _boundToLobby = false;
        }

        public void StartMatch()
        {
            if (!_isHost) return;

            _modeConfig.IsOnline = true;
            _modeConfig.IsHost = true;

            var lobby = _connectionService.LobbyController;
            if (lobby != null)
            {
                lobby.SetLobbyConfig(
                    _modeConfig.PlayerCount,
                    _modeConfig.IsCpuSlot,
                    _modeConfig.SelectedStageName);
                lobby.StartMatch();
            }

            HostStartMatchAsync().Forget();
        }

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

        // ═══════════════════════════════════════════
        //  UI 初期化
        // ═══════════════════════════════════════════

        private void SetupHostUI()
        {
            _doc.LobbyRoomCodeDisplay.style.display = DisplayStyle.Flex;
            _doc.LobbyJoinSection.style.display = DisplayStyle.None;
            _doc.LobbyStartButton.style.display = DisplayStyle.Flex;
            _doc.LobbyStatusLabel.text = "接続中...";
            _doc.LobbySlots.style.display = DisplayStyle.Flex;
            _doc.LobbyStageSection.style.display = DisplayStyle.Flex;
        }

        private void SetupClientUI()
        {
            _doc.LobbyRoomCodeDisplay.style.display = DisplayStyle.None;
            _doc.LobbyJoinSection.style.display = DisplayStyle.Flex;
            _doc.LobbyStartButton.style.display = DisplayStyle.None;
            _doc.LobbyStatusLabel.text = "ルームコードを入力してください";
            _doc.LobbyRoomCodeInput.value = string.Empty;
            // スロット・ステージはクライアント接続後に表示
            _doc.LobbySlots.style.display = DisplayStyle.None;
            _doc.LobbyStageSection.style.display = DisplayStyle.None;
        }

        private void InitializeSlotsAndStage()
        {
            if (_slotsInitialized) return;
            _slotsInitialized = true;

            SetupSlotHandlers();
            PopulateStageList();
        }

        // ═══════════════════════════════════════════
        //  スロット管理
        // ═══════════════════════════════════════════

        private void SetupSlotHandlers()
        {
            for (int i = 0; i < 4; i++)
            {
                int slot = i;

                _doc.LobbySlotToggleButtons[i]?.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!_isHost) return;
                    _audio?.PlaySfx(SfxIds.UiNavigate);
                    OnToggleSlot(slot);
                });

                _doc.LobbySlotAddButtons[i]?.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!_isHost) return;
                    _audio?.PlaySfx(SfxIds.UiNavigate);
                    ExpandSlot(slot);
                });

                _doc.LobbySlotRemoveButtons[i]?.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!_isHost) return;
                    _audio?.PlaySfx(SfxIds.UiNavigate);
                    CollapseSlot(slot);
                });
            }

            // P1 はホスト固定なのでトグル非表示
            if (_doc.LobbySlotToggleButtons[0] != null)
                _doc.LobbySlotToggleButtons[0].style.display = DisplayStyle.None;

            RefreshAllSlotUI();
        }

        private void OnToggleSlot(int slot)
        {
            if (slot == 0) return; // P1 はホスト固定

            _modeConfig.IsCpuSlot[slot] = !_modeConfig.IsCpuSlot[slot];
            RefreshSlotUI(slot);
            SyncLobbyConfig();
        }

        private void ExpandSlot(int slot)
        {
            if (slot < 2) return; // P1/P2 は常に存在

            _modeConfig.IsCpuSlot[slot] = true; // デフォルト CPU
            var content = _doc.LobbySlotContents[slot];
            var addBtn = _doc.LobbySlotAddButtons[slot];
            var elem = _doc.LobbySlotElements[slot];

            if (content != null) content.style.display = DisplayStyle.Flex;
            if (addBtn != null) addBtn.style.display = DisplayStyle.None;
            if (elem != null)
            {
                elem.RemoveFromClassList("setup-slot--empty");
                elem.AddToClassList("setup-slot--active");
            }

            RecalcPlayerCount();
            RefreshSlotUI(slot);
            SyncLobbyConfig();
        }

        private void CollapseSlot(int slot)
        {
            if (slot < 2) return;

            _modeConfig.IsCpuSlot[slot] = false;
            var content = _doc.LobbySlotContents[slot];
            var addBtn = _doc.LobbySlotAddButtons[slot];
            var elem = _doc.LobbySlotElements[slot];

            if (content != null) content.style.display = DisplayStyle.None;
            if (addBtn != null) addBtn.style.display = DisplayStyle.Flex;
            if (elem != null)
            {
                elem.RemoveFromClassList("setup-slot--active");
                elem.AddToClassList("setup-slot--empty");
            }

            RecalcPlayerCount();
            SyncLobbyConfig();
        }

        private void RecalcPlayerCount()
        {
            int count = 2; // P1 + P2 は常に存在
            for (int i = 2; i < 4; i++)
            {
                var content = _doc.LobbySlotContents[i];
                if (content != null && content.style.display != DisplayStyle.None)
                    count++;
            }
            _modeConfig.PlayerCount = count;
        }

        private void RefreshAllSlotUI()
        {
            for (int i = 0; i < _modeConfig.PlayerCount; i++)
                RefreshSlotUI(i);
        }

        private void RefreshSlotUI(int slot)
        {
            var typeLabel = _doc.LobbySlotTypeLabels[slot];
            var toggleBtn = _doc.LobbySlotToggleButtons[slot];
            if (typeLabel == null) return;

            bool isCpu = _modeConfig.IsCpuAt(slot);
            int connectedCount = _connectionService.ConnectedPlayerCount.CurrentValue;

            if (slot == 0)
            {
                typeLabel.text = "ホスト";
            }
            else if (isCpu)
            {
                typeLabel.text = "CPU";
                if (toggleBtn != null) toggleBtn.text = "Human に変更";
            }
            else
            {
                bool isConnected = slot < connectedCount;
                typeLabel.text = isConnected ? $"プレイヤー {slot + 1}" : "(空席)";
                if (toggleBtn != null) toggleBtn.text = "CPU に変更";
            }

            // クライアントは操作ボタン非表示
            if (!_isHost)
            {
                if (toggleBtn != null) toggleBtn.style.display = DisplayStyle.None;
                var addBtn = _doc.LobbySlotAddButtons[slot];
                if (addBtn != null) addBtn.style.display = DisplayStyle.None;
                var removeBtn = _doc.LobbySlotRemoveButtons[slot];
                if (removeBtn != null) removeBtn.style.display = DisplayStyle.None;
            }
        }

        // ═══════════════════════════════════════════
        //  ステージ選択
        // ═══════════════════════════════════════════

        private void PopulateStageList()
        {
            var configs = Resources.LoadAll<StageConfig>("StageConfigs");
            if (configs == null || configs.Length == 0) return;

            var sorted = configs.OrderBy(c => c.DisplayName).ToArray();

            foreach (var cfg in sorted)
            {
                _stageConfigs[cfg.name] = cfg;

                var card = new VisualElement();
                card.AddToClassList("stage-card");

                var thumb = new VisualElement();
                thumb.AddToClassList("stage-card__thumbnail");
                if (cfg.Thumbnail != null)
                    thumb.style.backgroundImage = new StyleBackground(cfg.Thumbnail);
                card.Add(thumb);

                var nameLabel = new Label(cfg.DisplayName);
                nameLabel.AddToClassList("stage-card__name");
                card.Add(nameLabel);

                string assetName = cfg.name;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!_isHost) return; // クライアントは選択不可
                    SelectStage(assetName);
                });

                _doc.LobbyStageList.Add(card);
                _stageCards.Add((card, assetName));
            }

            // デフォルト選択
            string defaultName = !string.IsNullOrEmpty(_modeConfig.SelectedStageName)
                ? _modeConfig.SelectedStageName
                : sorted.FirstOrDefault(c => c.name == "Standard")?.name ?? sorted[0].name;
            SelectStage(defaultName);
        }

        private void SelectStage(string assetName)
        {
            _selectedStageName = assetName;
            _modeConfig.SelectedStageName = assetName;

            foreach (var (card, name) in _stageCards)
            {
                if (name == assetName)
                    card.AddToClassList("stage-card--selected");
                else
                    card.RemoveFromClassList("stage-card--selected");
            }

            if (_stageConfigs.TryGetValue(assetName, out var cfg))
                UpdateStagePreview(cfg);

            if (_isHost)
            {
                _audio?.PlaySfx(SfxIds.UiNavigate);
                SyncLobbyConfig();
            }
        }

        private void UpdateStagePreview(StageConfig cfg)
        {
            if (cfg.Thumbnail != null && _doc.LobbyStagePreviewThumb != null)
                _doc.LobbyStagePreviewThumb.style.backgroundImage = new StyleBackground(cfg.Thumbnail);

            if (_doc.LobbyStagePreviewName != null) _doc.LobbyStagePreviewName.text = cfg.DisplayName;
            if (_doc.LobbyStagePreviewSize != null) _doc.LobbyStagePreviewSize.text = $"{cfg.Width} x {cfg.Height}";
            if (_doc.LobbyStagePreviewDesc != null) _doc.LobbyStagePreviewDesc.text = cfg.Description;
        }

        // ═══════════════════════════════════════════
        //  ネットワーク同期
        // ═══════════════════════════════════════════

        private void SyncLobbyConfig()
        {
            if (!_isHost) return;
            var lobby = _connectionService.LobbyController;
            lobby?.SetLobbyConfig(
                _modeConfig.PlayerCount,
                _modeConfig.IsCpuSlot,
                _modeConfig.SelectedStageName);
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

                // 初期設定を LobbyController に反映
                SyncLobbyConfig();
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

                _doc.LobbyJoinSection.style.display = DisplayStyle.None;
                _doc.LobbyRoomCodeDisplay.style.display = DisplayStyle.Flex;
                _doc.LobbyRoomCodeDisplay.text = roomCode;
                _doc.LobbyStatusLabel.text = "接続完了。ホストの開始を待っています...";

                // スロット・ステージを読み取り専用で表示
                _doc.LobbySlots.style.display = DisplayStyle.Flex;
                _doc.LobbyStageSection.style.display = DisplayStyle.Flex;
                InitializeSlotsAndStage();

                SubscribeToMatchStart();
            }
            catch (Exception ex)
            {
                _doc.LobbyStatusLabel.text = ex.Message;
            }
        }

        private async UniTaskVoid HostStartMatchAsync()
        {
            await UniTask.Delay(500);
            _sceneTransition.LoadMatchAsync().Forget();
        }

        // ═══════════════════════════════════════════
        //  クライアント側: LobbyController 購読
        // ═══════════════════════════════════════════

        private void SubscribeToMatchStart()
        {
            LobbyController.OnLobbySpawned += OnLobbySpawned;
            var existing = _connectionService.LobbyController;
            if (existing != null) BindToLobby(existing);
        }

        private void OnLobbySpawned(LobbyController lobby)
        {
            BindToLobby(lobby);
        }

        private void BindToLobby(LobbyController lobby)
        {
            if (_boundToLobby || _isHost) return;
            _boundToLobby = true;
            lobby.OnMatchStartDetected += OnClientMatchStart;
            lobby.OnLobbyConfigChanged += OnClientConfigChanged;

            // 初回の設定を反映
            OnClientConfigChanged();
        }

        private void OnClientConfigChanged()
        {
            var lobby = _connectionService.LobbyController;
            if (lobby == null) return;

            _modeConfig.PlayerCount = lobby.PlayerCount;
            _modeConfig.IsCpuSlot = LobbyController.DecodeCpuSlots(lobby.CpuSlotMask, lobby.PlayerCount);
            var stageName = lobby.StageName.ToString();
            if (!string.IsNullOrEmpty(stageName))
            {
                _modeConfig.SelectedStageName = stageName;
                // ステージカードの選択状態を更新
                foreach (var (card, name) in _stageCards)
                {
                    if (name == stageName)
                        card.AddToClassList("stage-card--selected");
                    else
                        card.RemoveFromClassList("stage-card--selected");
                }
                if (_stageConfigs.TryGetValue(stageName, out var cfg))
                    UpdateStagePreview(cfg);
            }

            // スロット表示の更新（P3/P4 の展開/折りたたみ含む）
            for (int i = 2; i < 4; i++)
            {
                bool isActive = i < _modeConfig.PlayerCount;
                var content = _doc.LobbySlotContents[i];
                var addBtn = _doc.LobbySlotAddButtons[i];
                var elem = _doc.LobbySlotElements[i];

                if (content != null) content.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
                if (addBtn != null) addBtn.style.display = DisplayStyle.None; // クライアントは追加ボタン非表示
                if (elem != null)
                {
                    if (isActive)
                    {
                        elem.RemoveFromClassList("setup-slot--empty");
                        elem.AddToClassList("setup-slot--active");
                    }
                    else
                    {
                        elem.RemoveFromClassList("setup-slot--active");
                        elem.AddToClassList("setup-slot--empty");
                    }
                }
            }

            RefreshAllSlotUI();
        }

        private void OnClientMatchStart()
        {
            var lobby = _connectionService.LobbyController;
            if (lobby != null)
            {
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
