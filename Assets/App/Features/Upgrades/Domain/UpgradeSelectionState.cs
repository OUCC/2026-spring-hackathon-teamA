using System;
using R3;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Upgrades.Domain
{
    /// <summary>
    /// 強化フェーズ中のカード選択インデックスを保持する共有状態。
    /// InputBridge が書き込み、UI Presenter が読み取る。
    /// </summary>
    public sealed class UpgradeSelectionState : IDisposable
    {
        private readonly ReactiveProperty<int> _p1Index = new(0);
        private readonly ReactiveProperty<int> _p2Index = new(0);

        public ReadOnlyReactiveProperty<int> P1Index => _p1Index;
        public ReadOnlyReactiveProperty<int> P2Index => _p2Index;

        public void SetIndex(PlayerId player, int index)
        {
            if (player == PlayerId.Player1)
                _p1Index.Value = index;
            else
                _p2Index.Value = index;
        }

        public int GetIndex(PlayerId player)
        {
            return player == PlayerId.Player1 ? _p1Index.Value : _p2Index.Value;
        }

        public void Reset()
        {
            _p1Index.Value = 0;
            _p2Index.Value = 0;
        }

        public void Dispose()
        {
            _p1Index.Dispose();
            _p2Index.Dispose();
        }
    }
}
