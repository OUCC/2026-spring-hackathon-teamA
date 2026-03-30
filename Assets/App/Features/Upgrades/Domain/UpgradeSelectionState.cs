using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Upgrades.Domain
{
    /// <summary>
    /// 強化フェーズ中のカード選択状態を保持する共有状態。
    /// InputBridge が書き込み、UI Presenter が読み取る。
    /// Row 0 = カード行、Row 1 = アクション行（リロール/スキップ）。
    /// </summary>
    public sealed class UpgradeSelectionState : IDisposable
    {
        private readonly ReactiveProperty<int>[] _indices;
        private readonly ReactiveProperty<int>[] _rows;
        private readonly HashSet<int>[] _purchased;
        private readonly ReactiveProperty<int>[] _purchaseCounts;
        private readonly int _playerCount;

        public UpgradeSelectionState(int playerCount)
        {
            _playerCount = playerCount;
            _indices = new ReactiveProperty<int>[playerCount];
            _rows = new ReactiveProperty<int>[playerCount];
            _purchased = new HashSet<int>[playerCount];
            _purchaseCounts = new ReactiveProperty<int>[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                _indices[i] = new ReactiveProperty<int>(0);
                _rows[i] = new ReactiveProperty<int>(0);
                _purchased[i] = new HashSet<int>();
                _purchaseCounts[i] = new ReactiveProperty<int>(0);
            }
        }

        public ReadOnlyReactiveProperty<int> GetIndexObservable(PlayerId player) => _indices[player.Index];
        public ReadOnlyReactiveProperty<int> GetRowObservable(PlayerId player) => _rows[player.Index];
        public ReadOnlyReactiveProperty<int> GetPurchaseCountObservable(PlayerId player) => _purchaseCounts[player.Index];

        public void SetIndex(PlayerId player, int index) => _indices[player.Index].Value = index;
        public int GetIndex(PlayerId player) => _indices[player.Index].Value;

        public void SetRow(PlayerId player, int row) => _rows[player.Index].Value = row;
        public int GetRow(PlayerId player) => _rows[player.Index].Value;

        public void MarkPurchased(PlayerId player, int index)
        {
            _purchased[player.Index].Add(index);
            _purchaseCounts[player.Index].Value++;
        }

        public bool IsPurchased(PlayerId player, int index)
            => _purchased[player.Index].Contains(index);

        public void UnmarkPurchased(PlayerId player, int index)
        {
            if (_purchased[player.Index].Remove(index))
                _purchaseCounts[player.Index].Value = Math.Max(0, _purchaseCounts[player.Index].Value - 1);
        }

        public void ClearPurchased(PlayerId player)
            => _purchased[player.Index].Clear();

        public void Reset()
        {
            for (int i = 0; i < _playerCount; i++)
            {
                _indices[i].Value = 0;
                _rows[i].Value = 0;
                _purchased[i].Clear();
                _purchaseCounts[i].Value = 0;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _playerCount; i++)
            {
                _indices[i].Dispose();
                _rows[i].Dispose();
                _purchaseCounts[i].Dispose();
            }
        }
    }
}
