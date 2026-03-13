using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI; // UI操作に必要

public class Player : MonoBehaviour
{
	public static Player Instance;

	[Header("ステータス")]
	public int maxHp = 100;
	public TextMeshProUGUI hpText; // HP表示用のテキスト（インスペクターでセット）
	public int currentHp;
	public Slider hpSlider; // UnityエディタからSliderをドラッグ&ドロップ

	[Header("移動設定")]
	public float moveSpeed = 5f;
	public Tilemap groundTilemap;
	public Tilemap obstacleTilemap;
	public Color actedColor = new Color(0.3f, 0.3f, 0.3f);

	[Header("特殊能力：タイル変更")]
	public TileBase fireTile;

	[Header("状態管理")]
	public bool hasActed = false;
	private Vector3 targetPosition;
	private bool isMoving = false;
	private Color originalColor;
	private SpriteRenderer sr;

	void Awake()
	{
		Instance = this;
		// ゲーム開始時に満タンにする
		currentHp = maxHp;
	}

	void Start()
	{
		sr = GetComponent<SpriteRenderer>();
		originalColor = sr.color;

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
			Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			mousePos.z = 0;
			Vector3Int cellPos = groundTilemap.WorldToCell(mousePos);

			if (groundTilemap.HasTile(cellPos))
			{
				groundTilemap.SetTile(cellPos, fireTile);
				FinishAction();
			}
		}
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
			FinishAction();
		}
	}

	public void FinishAction()
	{
		hasActed = true;
		sr.color = actedColor;
		GameManager.Instance.NextTurn();
	}

	public void ResetTurn()
	{
		hasActed = false;
		if (sr != null) sr.color = originalColor;
	}
}