using UnityEngine;
using VContainer;
using VContainer.Unity;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Shared.Infrastructure.UnityTime;
using FloorBreaker.ScriptableObjects.Balance;
using FloorBreaker.Shared.Infrastructure.Audio;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// アプリケーションレベルの DI ルート。
    /// Title シーンに配置し、DontDestroyOnLoad でシーンをまたいで生存する。
    /// AudioService は子 GameObject として配置する。
    /// </summary>
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        [SerializeField] private BalanceConfig _balanceConfig;
        [SerializeField] private int _randomSeed = 0;

        protected override void Awake()
        {
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance<IBalanceParameters>(_balanceConfig);

            int seed = _randomSeed != 0 ? _randomSeed : System.Environment.TickCount;
            builder.Register<IRandomProvider>(c => new SeededRandomProvider(seed), Lifetime.Singleton);

            builder.Register<UnityTimeProvider>(Lifetime.Singleton).As<ITimeProvider>();

            // AudioService: 子 GameObject から取得 (DontDestroyOnLoad と一緒に生存)
            var audioService = GetComponentInChildren<AudioService>();
            if (audioService != null)
            {
                builder.RegisterInstance<IAudioService>(audioService);
            }
        }
    }
}
