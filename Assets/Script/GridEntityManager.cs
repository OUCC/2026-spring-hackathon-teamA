using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GridEntityManager : MonoBehaviour
{
	public static GridEntityManager Instance;

	// 座標(Vector2Int)をキーにしてデータを高速検索できるようにする
	private Dictionary<Vector2Int, EntityData> entityMap = new Dictionary<Vector2Int, EntityData>();

	void Awake()
	{
		Instance = this;
	}

	void Start()
	{
		InitializeEntities();
	}

	private void Update()
	{
		// デバッグ用：全エンティティの位置をコンソールに表示
		if (Input.GetKeyDown(KeyCode.Tab))
		{
			foreach (var kvp in entityMap)
			{
				Debug.Log($"Position: {kvp.Key}, Type: {kvp.Value.type}, Object: {kvp.Value.obj.name}");
			}
		}
	}

	// 1. 初期化：全ユニットをスキャンして登録
	private void InitializeEntities()
	{
		entityMap.Clear();

		// PlayerとEnemyタグを持つものをすべて取得
		var players = GameObject.FindGameObjectsWithTag("Player");
		var enemies = GameObject.FindGameObjectsWithTag("Enemy");

		foreach (var p in players) RegisterEntity(p, "Player");
		foreach (var e in enemies) RegisterEntity(e, "Enemy");
	}

	private void RegisterEntity(GameObject obj, string type)
	{
		Vector2Int pos = WorldToGrid2D(obj.transform.position);
		entityMap[pos] = new EntityData(obj, pos, type);
	}

	private Vector2Int WorldToGrid2D(Vector3 worldPos)
	{
		return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));
	}

	// 2. 更新用関数：移動が完了した時にユニット側からこれを呼ぶ
	public void UpdateEntityPosition(GameObject obj, Vector2Int oldPos, Vector2Int newPos)
	{
		if (entityMap.ContainsKey(oldPos) && entityMap[oldPos].obj == obj)
		{
			EntityData data = entityMap[oldPos];
			entityMap.Remove(oldPos); // 古い位置を消す

			data.gridPosition = newPos;
			entityMap[newPos] = data; // 新しい位置に登録
		}
	}

	// 3. 指定のマスにいるエンティティを返す
	public EntityData GetEntityAt(Vector2Int pos)
	{
		return entityMap.ContainsKey(pos) ? entityMap[pos] : null;
	}

	// 4. プレイヤーの座標をすべて返す（複数いる可能性を考慮）
	public List<Vector2Int> GetPlayerPositions()
	{
		return entityMap.Values
			.Where(e => e.type == "Player")
			.Select(e => e.gridPosition)
			.ToList();
	}


	// 5. すべてのエンティティを返す（デバッグ用）
	public List<EntityData> GetAllEntities()
	{
		return entityMap.Values.ToList();
	}
}



[System.Serializable]
public class EntityData
{
	public GameObject obj;
	public Vector2Int gridPosition;
	public string type; // "Player" or "Enemy"

	public EntityData(GameObject obj, Vector2Int pos, string type)
	{
		this.obj = obj;
		this.gridPosition = pos;
		this.type = type;
	}
}

