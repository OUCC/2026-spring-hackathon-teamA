using UnityEngine;
using UnityEngine.Tilemaps;

public class Enemy : MonoBehaviour
{
	public float moveSpeed = 5f;
	public Tilemap groundTilemap;
	private Vector3 targetPosition;
	private bool isMoving = false;
	public bool hasActed = false;
	GameObject player;

	void Start()
	{
		// 座標をグリッドに合わせる
		targetPosition = new Vector3(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y), 0);
		transform.position = targetPosition;
		player = GameObject.FindGameObjectWithTag("Player");
	}

	// 敵のターンになったらBattleManagerから呼ばれる関数
	public void StartEnemyTurn()
	{
		if (hasActed)
		{
			return;
		}

		if (groundTilemap == null)
		{
			FinishAction();
			return;
		}

		// 1. プレイヤー（ターゲット）を探す
		if (player == null)
		{
			FinishAction();
			return;
		}

		// 2. どの方向に進むべきか計算
		Vector3 direction = CalculateBestMove(player.transform.position);

		// 3. 移動先をセット
		if (direction != Vector3.zero)
		{
			targetPosition = transform.position + direction;
			GridEntityManager.Instance.UpdateEntityPosition(
				gameObject,
				new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y)),
				new Vector2Int(Mathf.RoundToInt(targetPosition.x), Mathf.RoundToInt(targetPosition.y))
			);
			isMoving = true;
		}
		else
		{
			// 動けない、または隣接している場合は行動終了
			FinishAction();
		}
	}

	void Update()
	{
		if (isMoving)
		{
			transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
			if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
			{
				transform.position = targetPosition;
				isMoving = false;
				FinishAction();
			}
		}
	}

	Vector3 CalculateBestMove(Vector3 playerPos)
	{
		Vector3 diff = playerPos - transform.position;
		Vector3 dir = Vector3.zero;

		// X軸かY軸、距離が遠い方を優先して1マス近づく
		if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y))
			dir.x = Mathf.Sign(diff.x);
		else
			dir.y = Mathf.Sign(diff.y);

		// 移動先に床があるかチェック
		Vector3Int cellPos = groundTilemap.WorldToCell(transform.position + dir);
		if (groundTilemap.HasTile(cellPos))
		{
			return dir;
		}

		return Vector3.zero; // 移動不可なら動かない
	}

	void FinishAction()
	{
		hasActed = true;
	}

	public void ResetTurn()
	{
		hasActed = false;
	}
}