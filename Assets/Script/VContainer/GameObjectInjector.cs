using UnityEngine;
using R3;
using VContainer;
using VContainer.Unity;
using CustomTiles;
using System;

public class GameObjectInjector : LifetimeScope
{
    [SerializeField]
    GameManager _gameManager;

    [SerializeField]
    GridData _gridData;

    [SerializeField]
    TileGenerator _tileGenerator;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_gameManager);
        builder.RegisterInstance(_gridData);
        builder.RegisterInstance(_tileGenerator);
    }
}
