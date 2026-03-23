using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public enum TurnPhase { Player, Enemy }

[System.Serializable]
public class EnemySpawnData
{
	public Enemy enemyPrefab;
	public Vector2Int spawnGridPos;
	public int overrideMaxHp = -1;
}

[System.Serializable]
public class WaveData
{
	public List<EnemySpawnData> enemies = new List<EnemySpawnData>();
}

public class GameManager : MonoBehaviour
{
	public static GameManager Instance;

	[Header("UI設定")]
	public TextMeshProUGUI turnText; // インスペクターでTMPをセット
	public TextMeshProUGUI waveText;

	[Header("Wave設定")]
	public List<WaveData> waves = new List<WaveData>();
	public Tilemap enemyGroundTilemap;

	[Header("状態管理")]
	public TurnPhase currentPhase = TurnPhase.Player;
	private int currentWaveIndex = -1;
	private bool isGameFinished = false;

    [Header("グリッドのデータ")]
    [SerializeField]
    private GridData gridData;

	void Awake()
	{
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
	}

	void Start()
	{
		if (!SpawnNextWave())
		{
			HandleVictory();
			return;
		}

		// 最初のターン開始
		SetPlayerTurn();
	}

	// ターン切り替えのメインロジック
	public void NextTurn()
	{
		if (isGameFinished) return;

		if (currentPhase == TurnPhase.Player)
		{
            gridData.OnNextTurn(); // ターン開始時にタイルの変化を処理
			StartCoroutine(EnemyTurnRoutine());
            //ターン終了
		}
		else
		{
			SetPlayerTurn();
		}
	}

	// プレイヤーのターンをセットする

	void SetPlayerTurn()
	{
		if (isGameFinished) return;

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
		if (isGameFinished) yield break;

		currentPhase = TurnPhase.Enemy;
		UpdateTurnUI("Enemy Turn", Color.red);
		yield return null;

		// 生存中の敵のみ取得
		List<Enemy> enemies = GetAliveEnemies();
		if (enemies.Count == 0)
		{
			if (!SpawnNextWave())
			{
				HandleVictory();
				yield break;
			}

			SetPlayerTurn();
			yield break;
		}

		foreach (var e in enemies) e.ResetTurn();

		// 敵を1体ずつ動かす
		foreach (Enemy enemy in enemies)
		{
			if (enemy == null || enemy.IsDead) continue;

			enemy.StartEnemyTurn();

			while (enemy != null && !enemy.hasActed)
			{
				yield return null;
			}

			yield return new WaitForSeconds(0.1f);
		}

		// 行動後に敵が全滅していたら次Wave、なければプレイヤーへ
		enemies = GetAliveEnemies();
		if (enemies.Count == 0)
		{
			if (!SpawnNextWave())
			{
				HandleVictory();
				yield break;
			}
		}

		SetPlayerTurn();
	}

	private List<Enemy> GetAliveEnemies()
	{
		Enemy[] allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
		List<Enemy> aliveEnemies = new List<Enemy>();

		foreach (Enemy enemy in allEnemies)
		{
			if (enemy == null) continue;
			if (enemy.IsDead) continue;
			if (!enemy.gameObject.activeInHierarchy) continue;

			aliveEnemies.Add(enemy);
		}

		return aliveEnemies;
	}

	private bool SpawnNextWave()
	{
		currentWaveIndex++;

		if (currentWaveIndex >= waves.Count)
		{
			return false;
		}

		WaveData wave = waves[currentWaveIndex];
		if (wave == null || wave.enemies == null) return true;

		foreach (EnemySpawnData spawnData in wave.enemies)
		{
			if (spawnData == null || spawnData.enemyPrefab == null) continue;

			Vector3 spawnPos = new Vector3(spawnData.spawnGridPos.x, spawnData.spawnGridPos.y, 0f);
			Enemy spawnedEnemy = Instantiate(spawnData.enemyPrefab, spawnPos, Quaternion.identity);

			if (spawnedEnemy != null)
			{
				if (spawnedEnemy.groundTilemap == null && enemyGroundTilemap != null)
				{
					spawnedEnemy.groundTilemap = enemyGroundTilemap;
				}

                if (gridData != null)
                {
                    spawnedEnemy.gridData = gridData;
                } 
                else
                {
                    Debug.LogError("GridData reference is missing in GameManager. Enemy won't be able to interact with tiles.");
                }

				if (spawnData.overrideMaxHp > 0)
				{
					spawnedEnemy.maxHp = spawnData.overrideMaxHp;
					spawnedEnemy.currentHp = spawnData.overrideMaxHp;
				}

				if (GridEntityManager.Instance != null)
				{
					GridEntityManager.Instance.AddOrUpdateEntity(spawnedEnemy.gameObject, "Enemy");
				}
			}
		}

		UpdateWaveUI();
		return true;
	}

	private void HandleVictory()
	{
		isGameFinished = true;
		UpdateTurnUI("Victory!", Color.green);
		if (waveText != null)
		{
			waveText.text = "All Waves Cleared";
		}
	}

	private void UpdateWaveUI()
	{
		if (waveText != null)
		{
			waveText.text = $"Wave {currentWaveIndex + 1}/{waves.Count}";
		}
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