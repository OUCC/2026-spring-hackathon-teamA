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
        private readonly ReactiveProperty<int> _p1Index = new(0);
        private readonly ReactiveProperty<int> _p2Index = new(0);
        private readonly ReactiveProperty<int> _p1Row = new(0);
        private readonly ReactiveProperty<int> _p2Row = new(0);

        // 購入済みカードインデックス
        private readonly HashSet<int> _p1Purchased = new();
        private readonly HashSet<int> _p2Purchased = new();

        // 購入発生を通知 (値は購入回数カウンター)
        private readonly ReactiveProperty<int> _p1PurchaseCount = new(0);
        private readonly ReactiveProperty<int> _p2PurchaseCount = new(0);
        public ReadOnlyReactiveProperty<int> P1PurchaseCount => _p1PurchaseCount;
        public ReadOnlyReactiveProperty<int> P2PurchaseCount => _p2PurchaseCount;

        public ReadOnlyReactiveProperty<int> P1Index => _p1Index;
        public ReadOnlyReactiveProperty<int> P2Index => _p2Index;
        public ReadOnlyReactiveProperty<int> P1Row => _p1Row;
        public ReadOnlyReactiveProperty<int> P2Row => _p2Row;

        public void SetIndex(PlayerId player, int index)
        {
            if (player == PlayerId.Player1) _p1Index.Value = index;
            else _p2Index.Value = index;
        }

        public int GetIndex(PlayerId player)
            => player == PlayerId.Player1 ? _p1Index.Value : _p2Index.Value;

        public void SetRow(PlayerId player, int row)
        {
            if (player == PlayerId.Player1) _p1Row.Value = row;
            else _p2Row.Value = row;
        }

        public int GetRow(PlayerId player)
            => player == PlayerId.Player1 ? _p1Row.Value : _p2Row.Value;

        public void MarkPurchased(PlayerId player, int index)
        {
            if (player == PlayerId.Player1)
            {
                _p1Purchased.Add(index);
                _p1PurchaseCount.Value++;
            }
            else
            {
                _p2Purchased.Add(index);
                _p2PurchaseCount.Value++;
            }
        }

        public bool IsPurchased(PlayerId player, int index)
            => player == PlayerId.Player1 ? _p1Purchased.Contains(index) : _p2Purchased.Contains(index);

        public void ClearPurchased(PlayerId player)
        {
            if (player == PlayerId.Player1) _p1Purchased.Clear();
            else _p2Purchased.Clear();
        }

        public void Reset()
        {
            _p1Index.Value = 0;
            _p2Index.Value = 0;
            _p1Row.Value = 0;
            _p2Row.Value = 0;
            _p1Purchased.Clear();
            _p2Purchased.Clear();
            _p1PurchaseCount.Value = 0;
            _p2PurchaseCount.Value = 0;
        }

        public void Dispose()
        {
            _p1Index.Dispose();
            _p2Index.Dispose();
            _p1Row.Dispose();
            _p2Row.Dispose();
            _p1PurchaseCount.Dispose();
            _p2PurchaseCount.Dispose();
        }
    }
}
