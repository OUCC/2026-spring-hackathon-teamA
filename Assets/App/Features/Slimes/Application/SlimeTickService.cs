using System;
using R3;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Slimes.Application
{
    public sealed class SlimeTickService : IDisposable
    {
        private readonly SlimeAiService _aiService;
        private readonly SlimeSpawnService _spawnService;
        private readonly SlimeRegistry _registry;
        private readonly float _spawnCheckInterval;

        private float _spawnAccumulator;
        private IDisposable _timerSubscription;

        public SlimeTickService(
            SlimeAiService aiService,
            SlimeSpawnService spawnService,
            SlimeRegistry registry,
            TileTimerService tileTimerService,
            float spawnCheckInterval)
        {
            _aiService = aiService;
            _spawnService = spawnService;
            _registry = registry;
            _spawnCheckInterval = spawnCheckInterval;

            // 崩落完了時にスライム自動死亡を購読
            _timerSubscription = tileTimerService.TimerCompleted.Subscribe(evt => OnTimerCompleted(evt));
        }

        public void Tick(float deltaTime)
        {
            // 1. AI 処理
            _aiService.TickAll(deltaTime);

            // 2. スポーンチェック
            _spawnAccumulator += deltaTime;
            if (_spawnAccumulator >= _spawnCheckInterval)
            {
                _spawnAccumulator -= _spawnCheckInterval;
                _spawnService.SpawnIfNeeded();
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
            }
        }

        public void Dispose()
        {
            _timerSubscription?.Dispose();
        }
    }
}
