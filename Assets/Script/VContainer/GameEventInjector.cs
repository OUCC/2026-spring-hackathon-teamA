using UnityEngine;
using VContainer;
using VContainer.Unity;
using R3;

public class GameEventInjector : LifetimeScope
{
    [SerializeField]
    GameManager gameManager;
    
    protected override void Configure(IContainerBuilder builder)
    {
        Observable<Unit> OnNextTurnObservable = gameManager.OnNextTurn;

        //イベントの注入
        builder.RegisterInstance(OnNextTurnObservable);
    }
}
