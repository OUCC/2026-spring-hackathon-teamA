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
using FloorBreaker.Player.Presentation;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Bombs.Presentation;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.Slimes.Application;
using FloorBreaker.Slimes.Presentation;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Input.Application;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Cameras.Presentation;
using FloorBreaker.UI.RuntimeUI.Documents;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Match シーンの DI ルート。全 Match-scoped サービスを登録する。
    /// </summary>
    public sealed class MatchLifetimeScope : LifetimeScope
    {
        [Header("Fallback (Match 単体起動用)")]
        [SerializeField] private FloorBreaker.ScriptableObjects.Balance.BalanceConfig _fallbackBalance;

        protected override void Configure(IContainerBuilder builder)
        {
            // 親 (ProjectLifetimeScope) が無い場合のフォールバック登録
            if (Parent == null && _fallbackBalance != null)
            {
                Debug.Log("[MatchLifetimeScope] No parent scope — registering fallback globals");
                builder.RegisterInstance<IBalanceParameters>(_fallbackBalance);
                int seed = System.Environment.TickCount;
                builder.Register<IRandomProvider>(
                    c => new Shared.Infrastructure.Random.SeededRandomProvider(seed),
                    Lifetime.Singleton);
                builder.Register<Shared.Infrastructure.UnityTime.UnityTimeProvider>(Lifetime.Singleton)
                    .As<ITimeProvider>();

                // AudioService フォールバック: シーン内に AudioService があれば使う、なければ null 許容
                var audioService = FindAnyObjectByType<Shared.Infrastructure.Audio.AudioService>();
                if (audioService != null)
                    builder.RegisterInstance<IAudioService>(audioService);
                else
                    builder.Register<IAudioService>(
                        c => new Shared.Infrastructure.Audio.NullAudioService(), Lifetime.Singleton);
            }

            RegisterStage(builder);
            RegisterUpgrades(builder);
            RegisterPlayers(builder);
            RegisterBombs(builder);
            RegisterSlimes(builder);
            RegisterMatchFlow(builder);
            RegisterInput(builder);
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
        }

        private static void RegisterUpgrades(IContainerBuilder builder)
        {
            builder.Register<UpgradeCatalog>(Lifetime.Scoped);
            builder.Register<UpgradeAvailabilityRule>(Lifetime.Scoped);
            builder.Register<UpgradeRollRule>(Lifetime.Scoped);
            builder.Register<UpgradeApplyService>(Lifetime.Scoped);
            builder.Register<UpgradeSelectionState>(Lifetime.Scoped);
        }

        private static void RegisterPlayers(IContainerBuilder builder)
        {
            builder.Register<PlayerMoveService>(Lifetime.Scoped);

            builder.Register(c =>
            {
                var b = c.Resolve<IBalanceParameters>();
                return new PlayerDamageService(b.InvulnerabilityDuration, b.ForcedMoveDuration);
            }, Lifetime.Scoped);

            builder.Register(c =>
            {
                var b = c.Resolve<IBalanceParameters>();
                int size = b.StageSize;

                var stats1 = new PlayerStats(b.InitialHp, b.BaseMovementSpeed, b.MaxMovementSpeed);
                var stats2 = new PlayerStats(b.InitialHp, b.BaseMovementSpeed, b.MaxMovementSpeed);

                var build1 = CreateDefaultBuild(b);
                var build2 = CreateDefaultBuild(b);

                var p1Spawn = new GridPos(b.SpawnProtectionRadius, b.SpawnProtectionRadius);
                var p2Spawn = new GridPos(
                    size - 1 - b.SpawnProtectionRadius,
                    size - 1 - b.SpawnProtectionRadius);

                var p1 = new PlayerModel(PlayerId.Player1, p1Spawn, stats1, build1);
                var p2 = new PlayerModel(PlayerId.Player2, p2Spawn, stats2, build2);

                var cd1 = new BombCooldownState();
                var cd2 = new BombCooldownState();

                var rollRule = c.Resolve<UpgradeRollRule>();
                var applyService = c.Resolve<UpgradeApplyService>();
                var draft1 = new UpgradeDraftService(rollRule, applyService, b);
                var draft2 = new UpgradeDraftService(rollRule, applyService, b);

                return new MatchPlayers(p1, p2, cd1, cd2, draft1, draft2);
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

            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                return new BombFlightTracker(
                    c.Resolve<BombLaunchUseCase>(),
                    mp.Cooldown1, mp.Cooldown2,
                    c.Resolve<StageModel>(),
                    c.Resolve<SlimeRegistry>(),
                    c.Resolve<IBalanceParameters>());
            }, Lifetime.Scoped);
        }

        private static void RegisterSlimes(IContainerBuilder builder)
        {
            builder.Register<SlimeRegistry>(Lifetime.Scoped);
            builder.Register<SlimeSpawnService>(Lifetime.Scoped);
            builder.Register<SlimeAiService>(Lifetime.Scoped);
            builder.Register<SlimeDropResolver>(Lifetime.Scoped);
            builder.Register<SlimeTickService>(Lifetime.Scoped);
        }

        private static void RegisterMatchFlow(IContainerBuilder builder)
        {
            builder.Register(c =>
                new MatchClock(c.Resolve<IBalanceParameters>().PhaseDuration),
                Lifetime.Scoped);

            builder.Register<MatchEndUseCase>(Lifetime.Scoped);
            builder.Register<FireDamageTickService>(Lifetime.Scoped);

            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                return new UpgradePhaseUseCase(mp.Draft1, mp.Draft2, c.Resolve<UpgradeSelectionState>(), c.Resolve<IBalanceParameters>());
            }, Lifetime.Scoped);

            builder.Register(c =>
            {
                var mp = c.Resolve<MatchPlayers>();
                return new MatchPhaseScheduler(
                    c.Resolve<MatchClock>(),
                    c.Resolve<TileTimerService>(),
                    mp.Cooldown1, mp.Cooldown2,
                    c.Resolve<SlimeTickService>(),
                    c.Resolve<FireDamageTickService>(),
                    c.Resolve<BombFlightTracker>(),
                    c.Resolve<BombEffectSpreadService>(),
                    c.Resolve<StageShrinkService>(),
                    c.Resolve<UpgradePhaseUseCase>(),
                    c.Resolve<MatchEndUseCase>(),
                    c.Resolve<PlayerDamageService>(),
                    c.Resolve<SafeTileSearchService>(),
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
                    mp.Draft1, mp.Draft2,
                    mp.Player1, mp.Player2,
                    c.Resolve<MatchClock>(),
                    c.Resolve<IRandomProvider>(),
                    c.Resolve<UpgradeSelectionState>());
            }, Lifetime.Scoped);
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
            builder.RegisterEntryPoint<MatchTickRunner>();
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
