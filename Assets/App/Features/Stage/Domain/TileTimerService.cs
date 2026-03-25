using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    public enum TileTimerType : byte
    {
        Collapse,
        Recovery,
        Fire,
    }

    public readonly struct TileTimerCompletedEvent
    {
        public readonly GridPos Pos;
        public readonly TileTimerType Type;

        public TileTimerCompletedEvent(GridPos pos, TileTimerType type)
        {
            Pos = pos;
            Type = type;
        }
    }

    internal struct TileTimerEntry
    {
        public TileTimerType Type;
        public float Remaining;
        public float ChainDuration; // collapse → recovery の自動チェーン用
    }

    public sealed class TileTimerService : IDisposable
    {
        private readonly StageModel _model;
        private readonly Dictionary<GridPos, TileTimerEntry> _activeTimers = new();
        private readonly Subject<TileTimerCompletedEvent> _timerCompleted = new();

        public Observable<TileTimerCompletedEvent> TimerCompleted => _timerCompleted;

        public TileTimerService(StageModel model)
        {
            _model = model;
        }

        public void StartCollapseTimer(GridPos pos, float collapseDuration, float recoveryDuration)
        {
            _activeTimers[pos] = new TileTimerEntry
            {
                Type = TileTimerType.Collapse,
                Remaining = collapseDuration,
                ChainDuration = recoveryDuration,
            };
        }

        public void StartFireTimer(GridPos pos, float duration)
        {
            _activeTimers[pos] = new TileTimerEntry
            {
                Type = TileTimerType.Fire,
                Remaining = duration,
                ChainDuration = 0f,
            };
        }

        public void CancelTimer(GridPos pos)
        {
            _activeTimers.Remove(pos);
        }

        public bool HasActiveTimer(GridPos pos) => _activeTimers.ContainsKey(pos);

        public void Tick(float deltaTime)
        {
            // Pass 1: 残り時間を減算し、完了分を収集 (辞書を変更しない)
            List<(GridPos pos, TileTimerEntry entry)> completed = null;
            var keys = new List<GridPos>(_activeTimers.Keys);

            foreach (var key in keys)
            {
                var entry = _activeTimers[key];
                entry.Remaining -= deltaTime;
                _activeTimers[key] = entry;

                if (entry.Remaining <= 0f)
                {
                    completed ??= new List<(GridPos, TileTimerEntry)>();
                    completed.Add((key, entry));
                }
            }

            if (completed == null) return;

            foreach (var (pos, entry) in completed)
            {
                _activeTimers.Remove(pos);

                switch (entry.Type)
                {
                    case TileTimerType.Collapse:
                        _model.SetTileState(pos, TileState.Collapsed);
                        _timerCompleted.OnNext(new TileTimerCompletedEvent(pos, TileTimerType.Collapse));
                        // 自動で復帰タイマーを開始
                        if (entry.ChainDuration > 0f)
                        {
                            _activeTimers[pos] = new TileTimerEntry
                            {
                                Type = TileTimerType.Recovery,
                                Remaining = entry.ChainDuration,
                                ChainDuration = 0f,
                            };
                        }
                        break;

                    case TileTimerType.Recovery:
                        _model.SetTileState(pos, TileState.Normal);
                        _timerCompleted.OnNext(new TileTimerCompletedEvent(pos, TileTimerType.Recovery));
                        break;

                    case TileTimerType.Fire:
                        _model.SetTileState(pos, TileState.Normal);
                        _timerCompleted.OnNext(new TileTimerCompletedEvent(pos, TileTimerType.Fire));
                        break;
                }
            }
        }

        public void Dispose()
        {
            _timerCompleted.Dispose();
        }
    }
}
