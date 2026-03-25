using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;

namespace FloorBreaker.MatchFlow.Application
{
    public sealed class UpgradePhaseUseCase
    {
        private readonly UpgradeDraftService _draftP1;
        private readonly UpgradeDraftService _draftP2;
        private readonly float _timeout;

        private float _elapsed;
        private bool _isActive;

        public UpgradeDraftService DraftP1 => _draftP1;
        public UpgradeDraftService DraftP2 => _draftP2;
        public bool IsActive => _isActive;
        public float RemainingTime => _timeout - _elapsed;

        public UpgradePhaseUseCase(
            UpgradeDraftService draftP1,
            UpgradeDraftService draftP2,
            IBalanceParameters balance)
        {
            _draftP1 = draftP1;
            _draftP2 = draftP2;
            _timeout = balance.UpgradeSelectionTimeout;
        }

        public void Start(IReadOnlyList<PlayerModel> players, IRandomProvider random)
        {
            _elapsed = 0f;
            _isActive = true;

            foreach (var player in players)
            {
                var draft = GetDraft(player.Id);
                draft.Reset();
                draft.GenerateChoices(player, random);
            }
        }

        public void Tick(float deltaTime)
        {
            if (!_isActive) return;

            _elapsed += deltaTime;

            if (_elapsed >= _timeout)
            {
                // タイムアウト: 未完了プレイヤーを自動スキップ
                if (_draftP1.State.CurrentValue == DraftState.Choosing)
                    _draftP1.TimeOut();
                if (_draftP2.State.CurrentValue == DraftState.Choosing)
                    _draftP2.TimeOut();
            }

            // 両者完了チェック
            if (IsComplete)
                _isActive = false;
        }

        public bool IsComplete =>
            _draftP1.State.CurrentValue != DraftState.Choosing
            && _draftP2.State.CurrentValue != DraftState.Choosing;

        public void Reset()
        {
            _isActive = false;
            _elapsed = 0f;
            _draftP1.Reset();
            _draftP2.Reset();
        }

        public UpgradeDraftService GetDraft(PlayerId id)
        {
            return id == PlayerId.Player1 ? _draftP1 : _draftP2;
        }
    }
}
