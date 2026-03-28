using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Stage.Presentation;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;
using FloorBreaker.Player.Presentation;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Bombs.Presentation;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.Slimes.Application;
using FloorBreaker.Slimes.Presentation;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Upgrades.Application;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Input.Application;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Cameras.Presentation;
using FloorBreaker.UI.RuntimeUI.Documents;
using FloorBreaker.CpuPlayer.Application;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Match シーンの DI ルート。全 Match-scoped サービスを登録する。
    /// </summary>
    public sealed class MatchLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register(c =>
            {
                var modeConfig = c.Resolve<MatchModeConfig>();
                return new MatchConfig(modeConfig.IsCpuPlayer);
            }, Lifetime.Scoped);

            RegisterStage(builder);
            RegisterUpgrades(builder);
            RegisterPlayers(builder);
            RegisterBombs(builder);
            RegisterSlimes(builder);
            RegisterMatchFlow(builder);
            RegisterInput(builder);
            RegisterCpuPlayer(builder); // 常に登録 (実際の resolve は MatchTickRunner でゲート)
            RegisterPresentation(builder);
            RegisterEntryPoints(builder);
        }

        private static void RegisterStage(IContainerBuilder builder)
        {
            builder.Register(c =>
            {
                var balance = c.Resolve<IBalanceParameters>();
                return new StageModel(TileCoordRange.FromSize(balance.StageSize));
            }, Lifetime.Scoped);

            builder.Register<TileTimerService>(Lifetime.Scoped);
            builder.Register<StageQueryService>(Lifetime.Scoped);

            builder.Register(c =>
            {
                var b = c.Resolve<IBalanceParameters>();
                return new WallGenerationService(
                    b.WallSeedPercent, b.WallGrowthChance,
                    b.WallTargetPercent, b.SpawnProtectionRadius);
            }, Lifetime.Scoped);

            builder.Register<StageShrinkService>(Lifetime.Scoped);
            builder.Register<SafeTileSearchService>(Lifetime.Scoped);
            builder.Register<WarpService>(Lifetime.Scoped);

            builder.Register(c =>
            {
                var b = c.Resolve<IBalanceParameters>();
                return new GasIgnitionService(
                    c.Resolve<StageModel>(),
                    c.Resolve<TileTimerService>(),
                    0.1f,           // chainDelayPerStep: 0.1秒/マス
                    b.FireBombDuration);
            }, Lifetime.Scoped);
        }

        private static void RegisterUpgrades(IContainerBuilder builder)
        {
            builder.Register<UpgradeCatalog>(Lifetime.Scoped);
            builder.Register<UpgradeAvailabilityRule>(Lifetime.Scoped);
            builder.Register<UpgradeRollRule>(Lifetime.Scoped);
            builder.Register<UpgradeApplyService>(Lifetime.Scoped);

            builder.Register(c =>
            {
                var modeConfig = c.Resolve<MatchModeConfig>();
                return new UpgradeSelectionState(modeConfig.PlayerCount);
            }, Lifetime.Scoped);
        }

        private static void RegisterPlayers(IContainerBuilder builder)
        {
            builder.Register<PlayerMoveService>(Lifetime.Scoped);

            builder.Register(c =>
            {
                var b = c.Resolve<IBalanceParameters>();
                return new PlayerDamageService(
                    b.InvulnerabilityDuration, b.ForcedMoveDuration,
                    c.Resolve<StageModel>(), c.Resolve<SafeTileSearchService>());
            }, Lifetime.Scoped);

            builder.Register(c =>
            {
                var b = c.Resolve<IBalanceParameters>();
                var modeConfig = c.Resolve<MatchModeConfig>();
                int playerCount = modeConfig.PlayerCount;
                int size = b.StageSize;

                var spawnPositions = GetSpawnPositions(playerCount, size, b.SpawnProtectionRadius);

                var players = new List<PlayerModel>(playerCount);
                var cooldowns = new List<BombCooldownState>(playerCount);
                var drafts = new List<UpgradeDraftService>(playerCount);

                var rollRule = c.Resolve<UpgradeRollRule>();
                var applyService = c.Resolve<UpgradeApplyService>();

                for (int i = 0; i < playerCount; i++)
                {
                    var id = PlayerId.FromIndex(i);
                    var stats = new PlayerStats(b.InitialHp, b.BaseMovementSpeed, b.MaxMovementSpeed);
                    var build = CreateDefaultBuild(b);

                    players.Add(new PlayerModel(id, spawnPositions[i], stats, build));
                    cooldowns.Add(new BombCooldownState());
                    drafts.Add(new UpgradeDraftService(rollRule, applyService, b));
                }

                return new MatchPlayers(players, cooldowns, drafts);
            }, Lifetime.Scoped);
        }

        private static void RegisterBombs(IContainerBuilder builder)
        {
            builder.Register<BombAreaResolver>(Lifetime.Scoped);
            builder.Register<BombLandingResolver>(Lifetime.Scoped);
            builder.Register<BreakBombResolver>(Lifetime.Scoped);
            builder.Register<FireBombResolver>(Lifetime.Scoped);
            builder.Register<BombLaunchUseCase>(Lifetime.Scoped);
            builder.Register<BombEffectSpreadService>(Lifetime.Scoped);
            builder.Register<FireDamageTickService>(Lifetime.Scoped);

            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                return new BombFlightTracker(
                    c.Resolve<BombLaunchUseCase>(),
                    mp.Cooldowns,
                    c.Resolve<StageModel>(),
                    c.Resolve<SlimeRegistry>(),
                    c.Resolve<IBalanceParameters>());
            }, Lifetime.Scoped);
        }

        private static void RegisterSlimes(IContainerBuilder builder)
        {
            builder.Register<SlimeRegistry>(Lifetime.Scoped);
            builder.Register<SlimeDropResolver>(Lifetime.Scoped);

            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                return new SlimeSpawnService(
                    c.Resolve<StageModel>(),
                    c.Resolve<SlimeRegistry>(),
                    mp.All,
                    c.Resolve<IRandomProvider>(),
                    c.Resolve<IBalanceParameters>());
            }, Lifetime.Scoped);

            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                return new SlimeAiService(
                    c.Resolve<PlayerDamageService>(),
                    c.Resolve<SafeTileSearchService>(),
                    c.Resolve<SlimeRegistry>(),
                    mp.All,
                    c.Resolve<StageModel>(),
                    c.Resolve<IBalanceParameters>());
            }, Lifetime.Scoped);

            builder.Register(c =>
                new SlimeTickService(
                    c.Resolve<SlimeAiService>(),
                    c.Resolve<SlimeSpawnService>(),
                    c.Resolve<SlimeRegistry>(),
                    c.Resolve<TileTimerService>(),
                    c.Resolve<IBalanceParameters>().SlimeSpawnCheckInterval),
                Lifetime.Scoped);
        }

        private static void RegisterMatchFlow(IContainerBuilder builder)
        {
            builder.Register(c =>
                new MatchClock(c.Resolve<IBalanceParameters>().PhaseDuration),
                Lifetime.Scoped);

            builder.Register<MatchEndUseCase>(Lifetime.Scoped);

            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                return new UpgradePhaseUseCase(
                    mp.Drafts,
                    c.Resolve<UpgradeSelectionState>(),
                    c.Resolve<IBalanceParameters>());
            }, Lifetime.Scoped);

            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                return new MatchPhaseScheduler(
                    c.Resolve<MatchClock>(),
                    c.Resolve<TileTimerService>(),
                    mp.Cooldowns,
                    c.Resolve<SlimeTickService>(),
                    c.Resolve<FireDamageTickService>(),
                    c.Resolve<BombFlightTracker>(),
                    c.Resolve<BombEffectSpreadService>(),
                    c.Resolve<GasIgnitionService>(),
                    c.Resolve<StageShrinkService>(),
                    c.Resolve<UpgradePhaseUseCase>(),
                    c.Resolve<MatchEndUseCase>(),
                    c.Resolve<PlayerDamageService>(),
                    mp.All,
                    c.Resolve<StageModel>(),
                    c.Resolve<SlimeRegistry>(),
                    c.Resolve<IBalanceParameters>(),
                    c.Resolve<IRandomProvider>());
            }, Lifetime.Scoped);
        }

        private static void RegisterInput(IContainerBuilder builder)
        {
            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                return new GameplayInputBridge(
                    c.Resolve<IBalanceParameters>(),
                    c.Resolve<PlayerMoveService>(),
                    c.Resolve<BombFlightTracker>(),
                    c.Resolve<BombLaunchUseCase>(),
                    c.Resolve<MatchClock>(),
                    mp.All,
                    c.Resolve<StageModel>());
            }, Lifetime.Scoped);

            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                return new UpgradeUIInputBridge(
                    mp.Drafts, mp.All,
                    c.Resolve<MatchClock>(),
                    c.Resolve<IRandomProvider>(),
                    c.Resolve<UpgradeSelectionState>());
            }, Lifetime.Scoped);
        }

        private static void RegisterCpuPlayer(IContainerBuilder builder)
        {
            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                // CPU は最後のプレイヤースロット
                int cpuIndex = mp.PlayerCount - 1;
                var cpuPlayer = mp.All[cpuIndex];
                var humanPlayer = mp.All[0];
                return new CpuPlayerBrain(
                    c.Resolve<IBalanceParameters>(),
                    cpuPlayer, humanPlayer,
                    c.Resolve<StageModel>(),
                    c.Resolve<PlayerMoveService>(),
                    c.Resolve<BombFlightTracker>(),
                    c.Resolve<BombLaunchUseCase>(),
                    mp.Cooldowns[cpuIndex],
                    c.Resolve<SlimeRegistry>(),
                    mp.All);
            }, Lifetime.Scoped);

            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                int cpuIndex = mp.PlayerCount - 1;
                return new CpuUpgradeSelector(
                    c.Resolve<IBalanceParameters>(),
                    mp.Drafts[cpuIndex],
                    mp.All[cpuIndex]);
            }, Lifetime.Scoped);

            builder.Register(c =>
                new CpuPlayerService(
                    c.Resolve<CpuPlayerBrain>(),
                    c.Resolve<CpuUpgradeSelector>(),
                    c.Resolve<MatchClock>()),
                Lifetime.Scoped);
        }

        private static void RegisterPresentation(IContainerBuilder builder)
        {
            // シーン上の MonoBehaviour
            builder.RegisterComponentInHierarchy<StageViewFactory>();
            builder.RegisterComponentInHierarchy<PlayerViewFactory>();
            builder.RegisterComponentInHierarchy<BombViewFactory>();
            builder.RegisterComponentInHierarchy<SlimeViewFactory>();
            builder.RegisterComponentInHierarchy<SplitScreenCameraSetup>();
            builder.RegisterComponentInHierarchy<MatchUIDocument>();

            // ランタイム生成ラッパー
            builder.Register<TileViewRegistry>(Lifetime.Scoped);
            builder.Register<MatchPresenters>(Lifetime.Scoped);

            // 初期化サブシステム
            builder.Register<PresentationInitializer>(Lifetime.Scoped);
            builder.Register<InputInitializer>(Lifetime.Scoped);

            // AnimationService (Config は各 Factory の SerializeField から取得)
            builder.Register(c =>
                new TileAnimationService(c.Resolve<StageViewFactory>().Config),
                Lifetime.Scoped);

            builder.Register(c =>
                new PlayerAnimationService(c.Resolve<PlayerViewFactory>().Config),
                Lifetime.Scoped);

            builder.Register(c =>
                new BombAnimationService(c.Resolve<BombViewFactory>().Config),
                Lifetime.Scoped);

            builder.Register(c =>
                new SlimeAnimationService(c.Resolve<SlimeViewFactory>().Config),
                Lifetime.Scoped);

            builder.Register<DOTweenCameraShakeService>(Lifetime.Scoped)
                .As<ICameraShakeService>();

            builder.Register<ImpactFreezeService>(Lifetime.Scoped)
                .AsSelf()
                .As<IImpactFreezeService>();
        }

        private static void RegisterEntryPoints(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<MatchInitializer>();

            // MatchTickRunner: CpuPlayerService は CPU モード時のみ注入
            builder.Register(c =>
            {
                CpuPlayerService cpuService = null;
                var config = c.Resolve<MatchConfig>();
                if (config.IsCpuPlayer)
                    cpuService = c.Resolve<CpuPlayerService>();

                return new MatchTickRunner(
                    c.Resolve<MatchPhaseScheduler>(),
                    c.Resolve<GameplayInputBridge>(),
                    c.Resolve<MatchPresenters>(),
                    c.Resolve<SplitScreenCameraSetup>(),
                    c.Resolve<ITimeProvider>(),
                    cpuService);
            }, Lifetime.Scoped).AsImplementedInterfaces();
        }

        /// <summary>
        /// N 人分のスポーン位置を四隅から計算する。
        /// 2人: 対角（左下・右上）、3人: 左下・右上・左上、4人: 四隅すべて
        /// </summary>
        internal static GridPos[] GetSpawnPositions(int playerCount, int stageSize, int margin)
        {
            var corners = new[]
            {
                new GridPos(margin, margin),                                     // 左下
                new GridPos(stageSize - 1 - margin, stageSize - 1 - margin),     // 右上
                new GridPos(margin, stageSize - 1 - margin),                     // 左上
                new GridPos(stageSize - 1 - margin, margin),                     // 右下
            };

            var result = new GridPos[playerCount];
            for (int i = 0; i < playerCount && i < corners.Length; i++)
                result[i] = corners[i];
            return result;
        }

        private static PlayerBuild CreateDefaultBuild(IBalanceParameters b)
        {
            return new PlayerBuild(
                b.FireBombMaxFlightDistance, b.FireBombEffectRange,
                b.FireBombContactDamage, b.FireBombCooldown,
                b.FireBombDuration, b.FireBombDefaultWallPenetration,
                b.FireBombCooldownMin,
                b.BreakBombMaxFlightDistance, b.BreakBombEffectRange,
                b.BreakBombDamage, b.BreakBombCooldown,
                b.BreakBombCollapseDuration, b.BreakBombCooldownMin);
        }
    }
}
