using UnityEngine;
using R3;
using VContainer;
using VContainer.Unity;
using CustomTiles;

public class GameEventSource : LifetimeScope
{
    [SerializeField]
    GameManager gameManager;

    [SerializeField]
    GridData gridData;

    [SerializeField]
    TileGenerator tileGenerator;

    protected override void Configure(IContainerBuilder builder)
    {
        var OnNextTurnObservable = gameManager.OnNextTurn;

        builder.RegisterInstance(OnNextTurnObservable);
        builder.RegisterInstance(gameManager);
        builder.RegisterInstance(gridData);
        builder.RegisterInstance(tileGenerator);
    }
}
