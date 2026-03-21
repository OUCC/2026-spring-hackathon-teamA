using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI; // UI操作に必要
using Tiles;

public class Player : MonoBehaviour
{
	public static Player Instance;

	[Header("ステータス")]
	public int maxHp = 10;
	public TextMeshProUGUI hpText; // HP表示用のテキスト（インスペクターでセット）
	public int currentHp;
	public Slider hpSlider; // UnityエディタからSliderをドラッグ&ドロップ

	[Header("移動設定")]
	public float moveSpeed = 5f;
	public Tilemap groundTilemap;
	public Tilemap obstacleTilemap;

	[Header("特殊能力：タイル変更")]
	public Tiles.TileData fireTile;

	[Header("攻撃設定")]
	public int attackDamage = 25;

	[Header("状態管理")]
	public bool hasActed = false;
	private Vector3 targetPosition;
	private bool isMoving = false;

    [Header("タイル")]
    [SerializeField]
    private TileMapManager tileMapManager;
    [SerializeField]
    private GridData gridData;

	void Awake()
	{
		Instance = this;
		// ゲーム開始時に満タンにする
		currentHp = maxHp;
	}

	void Start()
	{
		// UIの初期設定
		UpdateHPUI();

		targetPosition = new Vector3(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y), 0);
		transform.position = targetPosition;
	}

	// --- HP関連のメソッド ---

	/// <summary>
	/// ダメージを受ける処理。外部（敵など）から呼ばれる想定
	/// </summary>
	public void TakeDamage(int damage)
	{
		currentHp -= damage;
		currentHp = Mathf.Clamp(currentHp, 0, maxHp); // 0以下、最大値以上にならないよう制限

		UpdateHPUI();

		if (currentHp <= 0)
		{
			Die();
		}
	}

	/// <summary>
	/// UIスライダーの値を更新
	/// </summary>
	private void UpdateHPUI()
	{
		if (hpSlider != null)
		{
			hpSlider.maxValue = maxHp;
			hpSlider.value = currentHp;
			hpText.text = $"HP: {currentHp}/{maxHp}";
		}
	}

	private void Die()
	{
		Debug.Log("Player has died.");
		// ここに死亡演出やリロード処理などを記述
	}

	// --- 以下、既存のUpdate / HandleInput / Move 処理 ---
	// (変更がないため、以前のコードをそのまま維持してください)

	void Update()
	{
		if (GameManager.Instance == null || GameManager.Instance.currentPhase != TurnPhase.Player || hasActed) return;
		if (isMoving) { MoveTowardsTarget(); return; }
		HandleInput();
	}

	private void HandleInput()
	{
		float h = Input.GetAxisRaw("Horizontal");
		float v = Input.GetAxisRaw("Vertical");

		if (h != 0) TrySetTarget(new Vector3(Mathf.Sign(h), 0, 0));
		else if (v != 0) TrySetTarget(new Vector3(0, Mathf.Sign(v), 0));

		if (Input.GetMouseButtonDown(1))
		{
			ChangeTileByClick();
		}

		if (Input.GetMouseButtonDown(0))
		{
			TryAttackByClick();
		}
	}

	public void SetTile()
	{
		Vector3Int cellPos = groundTilemap.WorldToCell(transform.position);
		if (groundTilemap.HasTile(cellPos))
		{
			groundTilemap.SetTile(cellPos, fireTile.TileBase);
			FinishAction();
		}
	}

	private void TryAttackByClick()
	{
		if (Camera.main == null) return;

		Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
		mousePos.z = 0;
		Vector2Int clickedPos = new Vector2Int(Mathf.RoundToInt(mousePos.x), Mathf.RoundToInt(mousePos.y));
		Vector2Int playerPos = new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y));

		// 上下左右1マス以内のみ（マンハッタン距離）
		int manhattan = Mathf.Abs(clickedPos.x - playerPos.x) + Mathf.Abs(clickedPos.y - playerPos.y);
		if (manhattan > 1) return;

		if (GridEntityManager.Instance == null) return;

		EntityData target = GridEntityManager.Instance.GetEntityAt(clickedPos);
		if (target == null || target.type != "Enemy" || target.obj == null) return;

		Enemy enemy = target.obj.GetComponent<Enemy>();
		if (enemy == null) return;

		enemy.TakeDamage(attackDamage);
		FinishAction();
	}

    //右クリックでタイルを変更する処理
    private void ChangeTileByClick()
    {
        if (Camera.main == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector3Int clickedPos = new Vector3Int(Mathf.RoundToInt(mousePos.x), Mathf.RoundToInt(mousePos.y), 0);
        Vector3Int playerPos = new Vector3Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y), 0);

        // // 上下左右1マス以内のみ（マンハッタン距離）
        // int manhattan = Mathf.Abs(clickedPos.x - playerPos.x) + Mathf.Abs(clickedPos.y - playerPos.y);
        // if (manhattan > 1) return;

        Vector3Int clickedGridPos = groundTilemap.WorldToCell(mousePos);

        Debug.Log($"Clicked grid position: {clickedGridPos}");

        tileMapManager.ChangeTile(clickedGridPos, fireTile);
        FinishAction();
    }

	private void TrySetTarget(Vector3 direction)
	{
		Vector3 nextPos = transform.position + direction;
		Vector3Int cellPos = groundTilemap.WorldToCell(nextPos);
		if (!groundTilemap.HasTile(cellPos)) return;
		if (obstacleTilemap != null && obstacleTilemap.HasTile(cellPos)) return;

		targetPosition = nextPos;
		isMoving = true;
	}

	private void MoveTowardsTarget()
	{
		transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
		GridEntityManager.Instance.UpdateEntityPosition(
			gameObject,
			new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y)),
			new Vector2Int(Mathf.RoundToInt(targetPosition.x), Mathf.RoundToInt(targetPosition.y))
		);
		if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
		{
			transform.position = targetPosition;
			isMoving = false;

            //炎のマスに入ったときにダメージ（簡易版)
            Vector3Int targetPositionGrid = groundTilemap.WorldToCell(targetPosition);
            gridData.GetTileData(targetPositionGrid).OnPlayerSteppedOnTile(targetPositionGrid, this);
            Debug.Log($"Player stepped on tile at {targetPositionGrid}");

			FinishAction();
		}
	}

	public void FinishAction()
	{
		hasActed = true;
		GameManager.Instance.NextTurn();
	}

	public void ResetTurn()
	{
		hasActed = false;
	}
}