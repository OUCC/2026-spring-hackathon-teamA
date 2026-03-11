using UnityEngine;
using TMPro;
using System.Collections;

public enum TurnPhase { Player, Enemy }

public class GameManager : MonoBehaviour
{
	public static GameManager Instance;

	[Header("UI設定")]
	public TextMeshProUGUI turnText; // インスペクターでTMPをセット

	[Header("状態管理")]
	public TurnPhase currentPhase = TurnPhase.Player;

	void Awake()
	{
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
	}

	void Start()
	{
		// 最初のターン開始
		SetPlayerTurn();
	}

	// ターン切り替えのメインロジック
	public void NextTurn()
	{
		if (currentPhase == TurnPhase.Player)
		{
			StartCoroutine(EnemyTurnRoutine());
		}
		else
		{
			SetPlayerTurn();
		}
	}

	// プレイヤーのターンをセットする

	void SetPlayerTurn()
	{
		currentPhase = TurnPhase.Player;
		UpdateTurnUI("Player Turn", Color.blue);

		// Player.Instance が null でないか確認してから実行する
		if (Player.Instance != null)
		{
			Player.Instance.ResetTurn();
		}
		else
		{
			// もしnullなら、少し遅れてリセットをかける（予備策）
			Debug.LogWarning("Player Instance is null. Waiting for Player to initialize...");
			Invoke(nameof(RetryReset), 0.1f);
		}
	}

	// 予備策用
	void RetryReset() { if (Player.Instance != null) Player.Instance.ResetTurn(); }

	// 敵のターン処理
	IEnumerator EnemyTurnRoutine()
	{
		currentPhase = TurnPhase.Enemy;
		UpdateTurnUI("Enemy Turn", Color.red);

		// 敵全員を未行動に戻す
		Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
		foreach (var e in enemies) e.ResetTurn();

		// 敵を1体ずつ動かす
		foreach (Enemy enemy in enemies)
		{
			enemy.StartEnemyTurn();

			while (!enemy.hasActed)
			{
				yield return null;
			}

			yield return new WaitForSeconds(0.1f);
		}

		// 全員の行動が終わったらプレイヤーへ
		NextTurn();
	}

	void UpdateTurnUI(string message, Color color)
	{
		if (turnText != null)
		{
			turnText.text = message;
			turnText.color = color;
		}
	}
}