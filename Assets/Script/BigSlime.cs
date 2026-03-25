using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;

public class BigSlime : Enemy
{
    private int ActionCount = 0;

    [Header("BigSlime Settings")]
	public int attackDamage = 1;

    [Header("Visual Effects")]
    public Color highlightColor = new Color(1f, 0.4f, 0.4f, 1f); // 攻撃時のタイルの色
    public float flashDuration = 0.15f; // 光っている時間

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

        StartCoroutine(FlashAttackRangeRoutine());

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
    IEnumerator FlashAttackRangeRoutine()
    {
        // 攻撃範囲となるタイルの座標リストを作成
        List<Vector3Int> cellsToHighlight = GetAttackRangeCells();

        // 範囲内のタイルをすべて光らせる
        foreach (Vector3Int cell in cellsToHighlight)
        {
            if (groundTilemap.HasTile(cell))
            {
                // タイルの色ロックを解除
                groundTilemap.SetTileFlags(cell, TileFlags.None);
                // 色を変更
                groundTilemap.SetColor(cell, highlightColor);
            }
        }

        // 指定時間待機
        yield return new WaitForSeconds(flashDuration);

        // タイルの色を元（白）に戻す
        foreach (Vector3Int cell in cellsToHighlight)
        {
            if (groundTilemap.HasTile(cell))
            {
                groundTilemap.SetColor(cell, Color.white);
            }
        }
    }
    private List<Vector3Int> GetAttackRangeCells()
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        Vector3Int myPos = groundTilemap.WorldToCell(transform.position);

        // ニマス十字の座標を追加（上・下・左・右）
        cells.Add(myPos + Vector3Int.up);
        cells.Add(myPos + Vector3Int.down);
        cells.Add(myPos + Vector3Int.left);
        cells.Add(myPos + Vector3Int.right);



        return cells;
    }

    protected override void FinishAction()
    {
        ActionCount = ActionCount + 1;

		if (ActionCount == 1)
		{
            StartCoroutine(Blank());
		}
		else
		{
			ActionCount = 0;
			base.FinishAction();
		}
    }


	IEnumerator Blank()
	{
        yield return new WaitForSeconds(0.3f);
        StartEnemyTurn();
    }




}