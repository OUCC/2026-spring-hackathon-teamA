using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Slimes.Application
{
    public sealed class SlimeTickService : IDisposable
    {
        private readonly SlimeAiService _aiService;
        private readonly SlimeSpawnService _spawnService;
        private readonly SlimeRegistry _registry;
        private readonly TileTimerService _tileTimerService;

        private float _spawnAccumulator;
        private IDisposable _timerSubscription;

        public SlimeTickService(
            SlimeAiService aiService,
            SlimeSpawnService spawnService,
            SlimeRegistry registry,
            TileTimerService tileTimerService)
        {
            _aiService = aiService;
            _spawnService = spawnService;
            _registry = registry;
            _tileTimerService = tileTimerService;

            // 崩落完了時にスライム自動死亡を購読
            _timerSubscription = _tileTimerService.TimerCompleted.Subscribe(evt => OnTimerCompleted(evt));
        }

        public void Tick(
            float deltaTime,
            IReadOnlyList<PlayerModel> players,
            StageModel stage,
            IRandomProvider random,
            IBalanceParameters balance)
        {
            // 1. AI 処理
            _aiService.TickAll(_registry, players, stage, deltaTime, balance);

            // 2. スポーンチェック
            _spawnAccumulator += deltaTime;
            if (_spawnAccumulator >= balance.SlimeSpawnCheckInterval)
            {
                _spawnAccumulator -= balance.SlimeSpawnCheckInterval;
                _spawnService.SpawnIfNeeded(stage, _registry, players, random, balance);
            }
        }

        private void OnTimerCompleted(TileTimerCompletedEvent evt)
        {
            if (evt.Type != TileTimerType.Collapse) return;

            var slime = _registry.GetAt(evt.Pos);
            if (slime != null && slime.IsAlive)
            {
                slime.Kill();
                _registry.Remove(slime.Id);
                // 崩落死亡はドロップなし（ステージ縮小と同扱い）
            }
        }

        public void Dispose()
        {
            _timerSubscription?.Dispose();
        }
    }
}
