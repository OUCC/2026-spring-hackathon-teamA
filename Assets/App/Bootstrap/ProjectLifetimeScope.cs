using UnityEngine;
using VContainer;
using VContainer.Unity;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Shared.Infrastructure.UnityTime;
using FloorBreaker.ScriptableObjects.Balance;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// アプリケーションレベルの DI ルート。
    /// Title シーンに配置し、isRoot: true でシーンをまたいで生存する。
    /// </summary>
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        [SerializeField] private BalanceConfig _balanceConfig;
        [SerializeField] private int _randomSeed = 0;

        protected override void Awake()
        {
            // シーン遷移後も生存する
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // バランス設定 (ScriptableObject → IBalanceParameters)
            builder.RegisterInstance<IBalanceParameters>(_balanceConfig);

            // 乱数 (シード 0 = 毎回ランダム)
            int seed = _randomSeed != 0 ? _randomSeed : System.Environment.TickCount;
            builder.Register<IRandomProvider>(c => new SeededRandomProvider(seed), Lifetime.Singleton);

            // 時間
            builder.Register<UnityTimeProvider>(Lifetime.Singleton).As<ITimeProvider>();
        }
    }
}
