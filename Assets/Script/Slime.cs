using UnityEngine;

public class Slime : Enemy
{
	[Header("Slime Settings")]
	public int attackDamage = 1;

	// EnemyのStartを継承
	protected override void Start()
	{
		base.Start();
	}

	// ターン開始時の処理をオーバーライド
	public override void StartEnemyTurn()
	{
		if (hasActed || isDead) return;

		if (player == null)
		{
			FinishAction();
			return;
		}

		// 1. プレイヤーとの距離をチェック（隣接しているか）
		Vector2Int currentGridPos = new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y));
		Vector2Int playerGridPos = new Vector2Int(Mathf.RoundToInt(player.transform.position.x), Mathf.RoundToInt(player.transform.position.y));

		float distance = Vector2Int.Distance(currentGridPos, playerGridPos);

		// 隣接している場合 (距離がちょうど1)
		if (Mathf.Approximately(distance, 1.0f))
		{
			AttackPlayer();
		}
		else
		{
			// 2. 隣接していない場合は移動を試みる
			MoveTowardsPlayer();
		}
	}

	// 攻撃処理
	protected virtual void AttackPlayer()
	{
		Debug.Log($"{gameObject.name} の攻撃！ プレイヤーに {attackDamage} ダメージ！");

		var playerScript = player.GetComponent<Player>();
		if (playerScript != null)
		{
			playerScript.TakeDamage(attackDamage);
		}

		FinishAction();
	}

	// 移動処理
	protected virtual void MoveTowardsPlayer()
	{
		Vector3 direction = CalculateBestMove(player.transform.position);

		if (direction != Vector3.zero)
		{
			Vector3 nextPos = transform.position + direction;
			Vector2Int nextGridPos = new Vector2Int(Mathf.RoundToInt(nextPos.x), Mathf.RoundToInt(nextPos.y));

			// 移動先が空いているか GridEntityManager で確認
			if (GridEntityManager.Instance.GetEntityAt(nextGridPos) == null)
			{
				targetPosition = nextPos;

				// マネージャーの座標データを更新
				GridEntityManager.Instance.UpdateEntityPosition(
					gameObject,
					new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y)),
					nextGridPos
				);

				isMoving = true; // Updateで移動が開始される
			}
			else
			{
				// 移動先が塞がっている場合は何もしないで終了
				Debug.Log($"{gameObject.name} は道が塞がっていて動けない！");
				FinishAction();
			}
		}
		else
		{
			FinishAction();
		}
	}
}