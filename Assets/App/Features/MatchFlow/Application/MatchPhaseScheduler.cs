using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.Slimes.Application;

namespace FloorBreaker.MatchFlow.Application
{
    public enum SchedulerState : byte
    {
        Running,
        StageShrink,
        UpgradePhase,
        Result,
    }

    public sealed class MatchPhaseScheduler
    {
        private readonly MatchClock _clock;
        private readonly TileTimerService _tileTimerService;
        private readonly BombCooldownState _p1Cooldown;
        private readonly BombCooldownState _p2Cooldown;
        private readonly SlimeTickService _slimeTickService;
        private readonly FireDamageTickService _fireDamageTickService;
        private readonly BombFlightTracker _bombFlightTracker;
        private readonly BombEffectSpreadService _bombEffectSpreadService;
        private readonly StageShrinkService _stageShrinkService;
        private readonly UpgradePhaseUseCase _upgradePhaseUseCase;
        private readonly MatchEndUseCase _matchEndUseCase;
        private readonly PlayerDamageService _playerDamageService;
        private readonly SafeTileSearchService _safeTileSearch;
        private readonly IReadOnlyList<PlayerModel> _players;
        private readonly StageModel _stage;
        private readonly SlimeRegistry _slimeRegistry;
        private readonly IBalanceParameters _balance;
        private readonly IRandomProvider _random;

        private readonly float _shrinkAnimDuration;
        private float _shrinkTimer;

        public SchedulerState State { get; private set; }
        public MatchClock Clock => _clock;

        public MatchPhaseScheduler(
            MatchClock clock,
            TileTimerService tileTimerService,
            BombCooldownState p1Cooldown,
            BombCooldownState p2Cooldown,
            SlimeTickService slimeTickService,
            FireDamageTickService fireDamageTickService,
            BombFlightTracker bombFlightTracker,
            BombEffectSpreadService bombEffectSpreadService,
            StageShrinkService stageShrinkService,
            UpgradePhaseUseCase upgradePhaseUseCase,
            MatchEndUseCase matchEndUseCase,
            PlayerDamageService playerDamageService,
            SafeTileSearchService safeTileSearch,
            IReadOnlyList<PlayerModel> players,
            StageModel stage,
            SlimeRegistry slimeRegistry,
            IBalanceParameters balance,
            IRandomProvider random)
        {
            _clock = clock;
            _tileTimerService = tileTimerService;
            _p1Cooldown = p1Cooldown;
            _p2Cooldown = p2Cooldown;
            _slimeTickService = slimeTickService;
            _fireDamageTickService = fireDamageTickService;
            _bombFlightTracker = bombFlightTracker;
            _bombEffectSpreadService = bombEffectSpreadService;
            _stageShrinkService = stageShrinkService;
            _upgradePhaseUseCase = upgradePhaseUseCase;
            _matchEndUseCase = matchEndUseCase;
            _playerDamageService = playerDamageService;
            _safeTileSearch = safeTileSearch;
            _players = players;
            _stage = stage;
            _slimeRegistry = slimeRegistry;
            _balance = balance;
            _random = random;

            _shrinkAnimDuration = balance.StageShrinkAnimDuration;

            State = SchedulerState.Running;
            _clock.SetPhase(GamePhase.MatchRunning);
        }

        public void Tick(float deltaTime)
        {
            switch (State)
            {
                case SchedulerState.Running:
                    TickRunning(deltaTime);
                    break;
                case SchedulerState.StageShrink:
                    TickStageShrink(deltaTime);
                    break;
                case SchedulerState.UpgradePhase:
                    TickUpgradePhase(deltaTime);
                    break;
                case SchedulerState.Result:
                    // 何もしない
                    break;
            }
        }

        private void TickRunning(float deltaTime)
        {
            // 全サービスに Tick 配布
            _clock.Tick(deltaTime);
            _tileTimerService.Tick(deltaTime);
            _p1Cooldown.Tick(deltaTime);
            _p2Cooldown.Tick(deltaTime);

            foreach (var player in _players)
            {
                player.Invulnerability.Tick(deltaTime);
                player.ForcedMove.Tick(deltaTime);
            }

            _slimeTickService.Tick(deltaTime, _players, _stage, _random, _balance);
            _fireDamageTickService.Tick(deltaTime, _players, _stage);
            _bombFlightTracker?.Tick(deltaTime, _players);
            _bombEffectSpreadService?.Tick(deltaTime);

            // HP 0 チェック
            var winner = _matchEndUseCase.CheckEnd(_players);
            if (winner.HasValue)
            {
                TransitionToResult(winner.Value);
                return;
            }

            // 20秒到達 → ステージ縮小へ
            if (_clock.RemainingValue <= 0f)
            {
                TransitionToStageShrink();
            }
        }

        private void TransitionToStageShrink()
        {
            State = SchedulerState.StageShrink;
            _clock.SetPhase(GamePhase.StageShrink);
            _clock.Pause();
            _shrinkTimer = 0f;

            // 外周を永久消滅
            var destroyed = _stageShrinkService.ShrinkOuterRing(_stage);

            // 縮小後に通行不可マスにいるプレイヤーをダメージ + BFS 強制移動
            // (外周リング上 + リング外の既に PermanentlyDestroyed なマスの両方を処理)
            var occupied = new HashSet<GridPos>();
            foreach (var p in _players) occupied.Add(p.CurrentPosition);

            foreach (var player in _players)
            {
                if (!_stage.IsPassable(player.CurrentPosition)
                    || !_stage.IsInBounds(player.CurrentPosition))
                {
                    // 無敵でもダメージ + 強制移動 (縮小は回避不可)
                    player.Stats.TakeDamage(_balance.FallBombDamage);
                    var safeTile = _safeTileSearch.FindSafeTile(_stage, player.CurrentPosition, occupied);
                    if (safeTile.HasValue)
                    {
                        player.ForcedMove.Start(safeTile.Value, _balance.ForcedMoveDuration);
                        player.CurrentPosition = safeTile.Value;
                        occupied.Add(safeTile.Value);
                    }
                }
            }

            // 縮小マス上のスライム死亡（ドロップなし）
            var slimesOnRing = _slimeRegistry.GetSlimesAt(destroyed);
            foreach (var slime in slimesOnRing)
            {
                slime.Kill();
                _slimeRegistry.Remove(slime.Id);
            }
        }

        private void TickStageShrink(float deltaTime)
        {
            _shrinkTimer += deltaTime;
            if (_shrinkTimer >= _shrinkAnimDuration)
            {
                TransitionToUpgradePhase();
            }
        }

        private void TransitionToUpgradePhase()
        {
            State = SchedulerState.UpgradePhase;
            _clock.SetPhase(GamePhase.UpgradePhase);
            _upgradePhaseUseCase.Start(_players, _random);
        }

        private void TickUpgradePhase(float deltaTime)
        {
            _upgradePhaseUseCase.Tick(deltaTime);

            if (_upgradePhaseUseCase.IsComplete)
            {
                TransitionToRunning();
            }
        }

        private void TransitionToRunning()
        {
            State = SchedulerState.Running;
            _clock.SetPhase(GamePhase.MatchRunning);
            _clock.ResetTimer();
            _clock.Resume();
        }

        private void TransitionToResult(PlayerId winner)
        {
            State = SchedulerState.Result;
            _clock.SetPhase(GamePhase.Result);
            _clock.Pause();
            _matchEndUseCase.SetWinner(winner);
        }
    }
}
