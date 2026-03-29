using UnityEngine;
using VContainer;
using VContainer.Unity;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Shared.Infrastructure.UnityTime;
using FloorBreaker.ScriptableObjects.Balance;
using FloorBreaker.Shared.Infrastructure.Audio;
using FloorBreaker.Shared.Infrastructure.SceneTransition;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Network.Infrastructure;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// アプリケーションレベルの DI ルート。
    /// Boot シーンに配置し、シーンをまたいで生存する（Boot は常時ロード）。
    /// AudioService は子 GameObject として配置する。
    /// </summary>
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        [SerializeField] private BalanceConfig _balanceConfig;
        [SerializeField] private int _randomSeed;
        [Header("Debug")]
        [SerializeField] private bool _debugMode;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance<IBalanceParameters>(_balanceConfig);

            int seed = _randomSeed != 0 ? _randomSeed : System.Environment.TickCount;
            builder.Register<IRandomProvider>(c => new SeededRandomProvider(seed), Lifetime.Singleton);

            builder.Register<UnityTimeProvider>(Lifetime.Singleton).As<ITimeProvider>();

            // シーン遷移サービス (ルートスコープを渡して EnqueueParent に使用)
            builder.Register<ISceneTransitionService>(
                _ => new UnitySceneTransitionService(this), Lifetime.Singleton);

            // シーン間モード選択状態
            builder.Register<MatchModeConfig>(Lifetime.Singleton);

            // ネットワーク接続サービス（シーンをまたいで生存）
            // BuildCallback でルートスコープを FusionSceneManager に渡す
            var rootScope = this;
            builder.Register<NetworkConnectionService>(Lifetime.Singleton);
            builder.RegisterBuildCallback(c =>
            {
                var conn = c.Resolve<NetworkConnectionService>();
                conn.SetRootScope(rootScope);
            });

            // AudioService: 子 GameObject から取得 (Boot シーンと一緒に生存)
            var audioService = GetComponentInChildren<AudioService>();
            if (audioService != null)
            {
                builder.RegisterInstance<IAudioService>(audioService);
            }

            // Boot EntryPoint
            builder.RegisterInstance(new BootConfig(_debugMode));
            builder.RegisterEntryPoint<BootInitializer>();
        }
    }
}
