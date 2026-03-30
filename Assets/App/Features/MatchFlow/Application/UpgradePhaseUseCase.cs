using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Upgrades.Application;

namespace FloorBreaker.MatchFlow.Application
{
    public sealed class UpgradePhaseUseCase : IDisposable
    {
        private readonly IReadOnlyList<UpgradeDraftService> _drafts;
        private readonly UpgradeSelectionState _selectionState;
        private readonly float _timeout;

        private float _elapsed;
        private bool _isActive;

        private readonly ReactiveProperty<float> _remainingTime = new(0f);

        public bool IsActive => _isActive;
        public ReadOnlyReactiveProperty<float> RemainingTime => _remainingTime;

        public UpgradePhaseUseCase(
            IReadOnlyList<UpgradeDraftService> drafts,
            UpgradeSelectionState selectionState,
            IBalanceParameters balance)
        {
            _drafts = drafts;
            _selectionState = selectionState;
            _timeout = balance.UpgradeSelectionTimeout;
        }

        public void Start(IReadOnlyList<PlayerModel> players, IRandomProvider random)
        {
            _elapsed = 0f;
            _isActive = true;
            _remainingTime.Value = _timeout;

            // 前フェーズの選択状態をリセット
            _selectionState.Reset();

            foreach (var player in players)
            {
                var draft = GetDraft(player.Id);
                draft.Reset();
                if (player.Stats.IsDead)
                {
                    draft.Skip();
                    continue;
                }
                draft.GenerateChoices(player, random);
            }
        }

        public void Tick(float deltaTime)
        {
            if (!_isActive) return;

            _elapsed += deltaTime;
            _remainingTime.Value = MathF.Max(0f, _timeout - _elapsed);

            if (_elapsed >= _timeout)
            {
                // タイムアウト: 未完了プレイヤーを自動スキップ
                foreach (var draft in _drafts)
                {
                    if (draft.State.CurrentValue == DraftState.Choosing)
                        draft.TimeOut();
                }
            }

            // 全員完了チェック
            if (IsComplete)
                _isActive = false;
        }

        public bool IsComplete =>
            _drafts.All(d => d.State.CurrentValue != DraftState.Choosing);

        public void Reset()
        {
            _isActive = false;
            _elapsed = 0f;
            _remainingTime.Value = 0f;
            foreach (var draft in _drafts) draft.Reset();
        }

        public UpgradeDraftService GetDraft(PlayerId id) => _drafts[id.Index];

        public void Dispose()
        {
            _remainingTime.Dispose();
        }
    }
}
