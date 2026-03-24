using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using CustomTiles;
using R3;
using VContainer;
using VContainer.Unity;

public class GridData: MonoBehaviour
{
    [SerializeField]
    private Tilemap _tileMapGround;

    [SerializeField]
    private Tilemap _tileMapCustomTiles;

    [SerializeField]
    private TileMapUI _tileMapUI;

    private HashSet<Vector2Int> _existingCells;
    private Dictionary<Vector2Int, CustomTileData> _gridDataDict;

    public Dictionary<Vector2Int, CustomTileData> TilesChangeOnNextTurn { get; private set; } = new Dictionary<Vector2Int, CustomTileData>();
    private Observable<Unit> _onNextTurnObservable;

    void Start()
    {
        _gridDataDict = GenerateGridData();
        printData();
    }

    [Inject]
    void Construct(Observable<Unit> onNextTurnObservable)
    {
        _onNextTurnObservable = onNextTurnObservable;
    }

    /// <summary>
    /// Tilemap からタイルリストを生成する
    /// </summary>
    public Dictionary<Vector2Int, CustomTileData> GenerateGridData()
    {
        this._tileMapCustomTiles.CompressBounds();
        BoundsInt bounds = this._tileMapCustomTiles.cellBounds;

        _gridDataDict = new Dictionary<Vector2Int, CustomTileData>();

        // y 行ごとにリストを作成
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Vector2Int cellPos = new Vector2Int(x, y);
                TileBase tile = _tileMapCustomTiles.GetTile(ConvertVector.ToVector3Int(cellPos));
                
                if (tile == null)
                {
                    continue;
                }
                
                _existingCells.Add(cellPos);
            }
        }

        foreach(Vector2Int cellPos in _existingCells)
        {
            TileBase tile = _tileMapCustomTiles.GetTile(ConvertVector.ToVector3Int(cellPos));
                
            if (tile == null)
            {
                continue;
            }

            // タイルの名前に応じてタイルを生成しているがアカンコードなので後々置換する
            CustomTileData newTile;
            if (tile.name.Contains("Fire"))
            {
                newTile = new FireTile(tile);
            }
            else
            {
                newTile = new WaterTile(tile);
            }

            _gridDataDict[cellPos] = newTile;
        }
        return _gridDataDict;
    }

    // デバッグ用：タイルの種類をコンソールに出力
    public void printData()
    {
        BoundsInt bounds = this._tileMapCustomTiles.cellBounds;
        for(int y = bounds.yMin; y < bounds.yMax; y++)
        {

            string rowStr = "";
            for(int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cellPos2D = new Vector2Int(x, y);
                if (_gridDataDict.TryGetValue(cellPos2D, out CustomTiles.CustomTileData tileData))
                {
                    rowStr += tileData.TileName + " ";
                }
                else
                {
                    rowStr += "* ";
                }
            }
            Debug.Log(rowStr);
        }
    }

    public void ChangeTile(Vector2Int position, CustomTileData newTile)
    {
        if (newTile == null)
        {
            Debug.LogError("ChangeTile: newTile が null です。Inspector で TileData を設定してください。");
            return;
        }
        
        if (_gridDataDict.ContainsKey(position))
        {
            _gridDataDict[position] = newTile;
            newTile.OnSet(position);
            _tileMapUI.ChangeTileUI(ConvertVector.ToVector3Int(position), newTile);
        }
        else
        {
            Debug.LogError($"ChangeTile: 座標 ({position.x}, {position.y}) にタイルデータが見つかりません。");
        }
    }

    public void ChangeTiles(Dictionary<Vector2Int, CustomTileData> tilesToChange)
    {
        foreach (var kvp in tilesToChange)
        {
            Vector2Int position = kvp.Key;
            CustomTileData newTile = kvp.Value;
            if (_gridDataDict.ContainsKey(position))
            {
                _gridDataDict[position] = newTile;
                _tileMapUI.ChangeTileUI(ConvertVector.ToVector3Int(position), newTile);
                newTile.OnSet(position);
            }
            else
            {
                Debug.LogError($"ChangeTiles: 座標 ({position.x}, {position.y}) にタイルデータが見つかりません。");
            }
        }
    }
    
    public CustomTileData GetTileData(Vector2Int position)
    {
        if (_gridDataDict.TryGetValue(position, out CustomTileData tileData))
        {
            return tileData;
        }
        else
        {
            Debug.LogError($"GetTileData: tile not found at ({position.x}, {position.y})");
            return null;
        }
    }

    public bool HasTileData(Vector2Int position)
    {
        return _gridDataDict.ContainsKey(position);
    }

    public void AddTileDataNextTurn(Vector2Int position, CustomTileData tileData)
    {
        if (TilesChangeOnNextTurn.ContainsKey(position))
        {
            TilesChangeOnNextTurn[position] = tileData; // すでに変更予定のタイルがある場合は上書き
        }
    }

    public bool TryGetTileData(Vector2Int position, out CustomTileData tileData)
    {
        return _gridDataDict.TryGetValue(position, out tileData);
    }

    //ここから下のやつをイベントハンドラーで呼び出すように変更したい
   

    public void OnPlayerSteppedOnTile(Vector2Int position, Player player)
    {
        var tileData = GetTileData(position);
        tileData.OnPlayerSteppedOnTile(position, player);
    }

    public void OnEnemySteppedOnTile(Vector2Int position, Enemy enemy)
    {
        var tileData = GetTileData(position);
        tileData.OnEnemySteppedOnTile(position, this, enemy);
    }

    private void ChangeTilesOnNextTurn()
    {
        ChangeTiles(TilesChangeOnNextTurn);
        TilesChangeOnNextTurn.Clear(); // ターン開始時に辞書をクリアして次のターンに備える
    }
}
