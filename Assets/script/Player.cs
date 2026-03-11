using UnityEngine;
using UnityEngine.Tilemaps;

public class Player : MonoBehaviour
{
	public static Player Instance;

	[Header("移動設定")]
	public float moveSpeed = 5f;
	public Tilemap groundTilemap;    // 床（移動可能範囲）
	public Tilemap obstacleTilemap;  // 障害物（壁など）
	public Color actedColor = new Color(0.3f, 0.3f, 0.3f);

	[Header("特殊能力：タイル変更")]
	public TileBase fireTile;        // 変化後の炎タイル

	[Header("状態管理")]
	public bool hasActed = false;
	private Vector3 targetPosition;
	private bool isMoving = false;
	private Color originalColor;
	private SpriteRenderer sr;

	void Awake()
	{
		Instance = this;
	}

	void Start()
	{
		sr = GetComponent<SpriteRenderer>();
		originalColor = sr.color;

		// 座標をグリッドの整数値にスナップさせる
		targetPosition = new Vector3(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y), 0);
		transform.position = targetPosition;
	}

	void Update()
	{
		// 1. 自分のターンでない、または行動済みなら何もしない
		if (GameManager.Instance == null || GameManager.Instance.currentPhase != TurnPhase.Player || hasActed) return;

		// 2. 移動中なら移動処理のみ
		if (isMoving)
		{
			MoveTowardsTarget();
			return;
		}

		// 3. 入力処理
		HandleInput();
	}

	private void HandleInput()
	{
		// --- WASD移動 ---
		float h = Input.GetAxisRaw("Horizontal");
		float v = Input.GetAxisRaw("Vertical");

		if (h != 0)
		{
			TrySetTarget(new Vector3(Mathf.Sign(h), 0, 0));
		}
		else if (v != 0)
		{
			TrySetTarget(new Vector3(0, Mathf.Sign(v), 0));
		}

		// --- 右クリックでタイルを炎に変える（1ターン消費） ---
		if (Input.GetMouseButtonDown(1))
		{
			// マウス位置をワールド座標に変換
			Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			mousePos.z = 0;

			// セル座標に変換
			Vector3Int cellPos = groundTilemap.WorldToCell(mousePos);

			// 床がある場所ならタイルを張り替える
			if (groundTilemap.HasTile(cellPos))
			{
				groundTilemap.SetTile(cellPos, fireTile);
				Debug.Log($"タイルを炎に変更: {cellPos}");
				FinishAction();
			}
		}
	}

	// 移動可能かチェックしてターゲットをセット
	private void TrySetTarget(Vector3 direction)
	{
		Vector3 nextPos = transform.position + direction;
		Vector3Int cellPos = groundTilemap.WorldToCell(nextPos);

		// 床がない、または障害物がある場合は移動不可
		if (!groundTilemap.HasTile(cellPos)) return;
		if (obstacleTilemap != null && obstacleTilemap.HasTile(cellPos)) return;

		targetPosition = nextPos;
		isMoving = true;
	}

	private void MoveTowardsTarget()
	{
		transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

		if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
		{
			transform.position = targetPosition;
			isMoving = false;

			// 移動完了で行動終了（FE風：1回移動したら終わり）
			FinishAction();
		}
	}

	public void FinishAction()
	{
		hasActed = true;
		sr.color = actedColor;

		// ターン終了をマネージャーに通知
		GameManager.Instance.NextTurn();
	}

	public void ResetTurn()
	{
		hasActed = false;
		if (sr != null) sr.color = originalColor;
	}
}