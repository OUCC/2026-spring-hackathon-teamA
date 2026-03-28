# FLOOR BREAKER — 4P対応・ステージギミック・演出改善 設計書

## 概要

通信対戦実装の前段階として、以下の3軸を整備する。

1. **4P対応**: 2プレイヤー固定の構造を最大4人まで柔軟に対応
2. **ステージギミック**: ステージバリエーションを支える新タイル種の追加
3. **演出改善**: 炎の残り時間・崩落予告など、プレイヤーに情報を伝える視覚フィードバック強化

---

## 1. 4P対応

### 1.1 現状の問題

2プレイヤーが以下の6層にわたってハードコードされている。

| 層 | 主要ファイル | 固定内容 |
|---|---|---|
| Domain | `PlayerId.cs` | `Player1`/`Player2` の2値、`Opponent` が反転前提 |
| Data | `MatchPlayers.cs` | P1/P2 の明示フィールド |
| DI | `MatchLifetimeScope.cs` | Stats/Build/Cooldown/Draft を2つずつ個別生成 |
| Input | `InputMapSwitcher`, `UpgradeUIInputBridge` | `Gameplay_P1`/`P2` 等のハードコード |
| Camera | `SplitScreenCameraSetup.cs` | 2カメラ、Viewport 50%/50% |
| UI | UXML, View, Presenter 全般 | Left/Right の対構造 |

### 1.2 PlayerId の拡張

```csharp
public readonly struct PlayerId : IEquatable<PlayerId>
{
    // 既存の互換性を維持
    public static readonly PlayerId Player1 = new(0);
    public static readonly PlayerId Player2 = new(1);
    public static readonly PlayerId Player3 = new(2);
    public static readonly PlayerId Player4 = new(3);

    public static PlayerId FromIndex(int index) => new((byte)index);

    private readonly byte _value;
    public int Index => _value;

    // Opponent は 1v1 専用。N人モードでは使わない
    [Obsolete("1v1専用。N人モードでは使わないこと")]
    public PlayerId Opponent => _value == 0 ? Player2 : Player1;
}
```

### 1.3 MatchPlayers のコレクション化

```csharp
public sealed class MatchPlayers
{
    public IReadOnlyList<PlayerModel> All { get; }
    public IReadOnlyList<BombCooldownState> Cooldowns { get; }
    public IReadOnlyList<UpgradeDraftService> Drafts { get; }

    public int PlayerCount => All.Count;

    public PlayerModel Get(PlayerId id) => All[id.Index];
    public BombCooldownState GetCooldown(PlayerId id) => Cooldowns[id.Index];
    public UpgradeDraftService GetDraft(PlayerId id) => Drafts[id.Index];

    // 後方互換（段階的に削除）
    public PlayerModel Player1 => All[0];
    public PlayerModel Player2 => All[1];
}
```

### 1.4 スポーン位置（四隅配置）

3人の場合も四隅のうち3つを使用する。三角形配置は条件分岐が複雑になるため不採用。

```text
P1 ──────── P2        P1 ──────── P2        P1 ──────── P2
│            │        │            │        │            │
│   30x30    │        │   30x30    │        │   30x30    │
│            │        │            │        │            │
P3 ──────── P4        P3 ────────（空）      （空）────────（空）
  4人                   3人                   2人（現行）
```

| 人数 | 使用コーナー |
|------|-------------|
| 2 | 左上(P1), 右下(P2) — 現行の対角配置を維持 |
| 3 | 左上(P1), 右上(P2), 左下(P3) |
| 4 | 四隅すべて |

```csharp
public static GridPos[] GetSpawnPositions(int playerCount, int width, int height, int margin)
{
    var corners = new[]
    {
        new GridPos(margin, margin),                     // 左下
        new GridPos(width - 1 - margin, height - 1 - margin), // 右上
        new GridPos(margin, height - 1 - margin),        // 左上
        new GridPos(width - 1 - margin, margin),         // 右下
    };

    // 2人: 対角（左下・右上）、3人: 左下・右上・左上、4人: 全部
    return corners[..playerCount];
}
```

### 1.5 カメラ分割

| 人数 | レイアウト | Viewport |
|------|-----------|----------|
| 1 | フル画面 | (0, 0, 1, 1) |
| 2 | 左右分割 | (0, 0, 0.5, 1) / (0.5, 0, 0.5, 1) |
| 3 | 上に2つ + 下に1つ(中央寄せ) | (0, 0.5, 0.5, 0.5) / (0.5, 0.5, 0.5, 0.5) / (0.25, 0, 0.5, 0.5) |
| 4 | 4象限 | (0, 0.5, 0.5, 0.5) / (0.5, 0.5, 0.5, 0.5) / (0, 0, 0.5, 0.5) / (0.5, 0, 0.5, 0.5) |

> **注意**: ネットワーク対戦では自分の画面1つでよいので、N分割カメラはローカルマルチ専用。

### 1.6 UI ペインの動的生成

UXML のテンプレート再利用方針は既に正しい。Left/Right 固定を N ペインの動的生成に変える。

```text
MatchRoot
  MatchLayer (flex-row, flex-wrap)
    PlayerPane[0] (.hud-root .hud-root--p1)
    PlayerPane[1] (.hud-root .hud-root--p2)
    PlayerPane[2] (.hud-root .hud-root--p3)  // 3-4人時のみ
    PlayerPane[3] (.hud-root .hud-root--p4)  // 4人時のみ
  OverlayLayer
    UpgradeOverlayRoot
      UpgradePanes (flex-row, flex-wrap)
        UpgradePane[0..N]  // プレイヤー数分
    ResultRoot
      ResultPanes (flex-row, flex-wrap)
        ResultPane[0..N]
```

### 1.7 Input の動的マップ

Action Map を `Gameplay_P{n}` / `UpgradeUI_P{n}` のパターンで4人分用意。
ローカルでは最大2人（キーボード + パッド）が現実的。3-4人はパッド複数、またはネットワーク経由。

### 1.8 UpgradeSelectionState のコレクション化

現在 `_p1Index` / `_p2Index` 等がハードコードされている。プレイヤーIDで引ける辞書ベースに変更。

```csharp
public sealed class UpgradeSelectionState
{
    private readonly Dictionary<PlayerId, ReactiveProperty<int>> _indices;
    private readonly Dictionary<PlayerId, ReactiveProperty<int>> _rows;
    private readonly Dictionary<PlayerId, HashSet<int>> _purchased;
    private readonly Dictionary<PlayerId, ReactiveProperty<int>> _purchaseCounts;

    public ReadOnlyReactiveProperty<int> GetIndex(PlayerId id) => _indices[id];
    // ...
}
```

### 1.9 勝利条件の拡張

- **2人**: 相手のHP 0 で勝利（現行）
- **3-4人**: 最後の1人が生存で勝利。`Opponent` プロパティは使わず、生存者リストで判定

```csharp
// MatchEndUseCase
public PlayerId? CheckWinner(IReadOnlyList<PlayerModel> players)
{
    var alive = players.Where(p => p.CurrentHp.CurrentValue > 0).ToList();
    return alive.Count == 1 ? alive[0].Id : null;
}
```

### 1.10 実装順序

| Phase | 内容 | 影響範囲 |
|-------|------|---------|
| A-1 | `PlayerId` 拡張 + `MatchPlayers` コレクション化 | Domain/Application |
| A-2 | `MatchLifetimeScope` ループ生成 + `StageConfig` SO 導入 | Bootstrap/DI |
| A-3 | `UpgradeSelectionState` コレクション化 | Upgrades Domain |
| A-4 | `SplitScreenCameraSetup` N分割対応 | Camera Presentation |
| A-5 | UI ペイン動的生成 (HUD/Upgrade/Result) | UI Presentation |
| A-6 | Input Map 動的割り当て | Input Infrastructure |
| A-7 | 勝利条件の拡張 | MatchFlow Application |

---

## 2. ステージギミック

### 2.1 StageConfig ScriptableObject

ステージごとの設定を data-driven にする。

```csharp
[CreateAssetMenu(menuName = "FloorBreaker/Stage Config")]
public class StageConfig : ScriptableObject
{
    [Header("Grid")]
    public int width = 30;
    public int height = 30;
    public int maxPlayers = 4;

    [Header("Wall Generation")]
    public float wallSeedPercent = 0.08f;
    public float wallGrowthChance = 0.4f;
    public float wallTargetPercent = 0.2f;
    public int spawnProtectionRadius = 2;

    [Header("Preset Tiles")]
    public PresetTile[] presetTiles;  // 固定配置タイル（岩盤・ガス・ワープ等）

    [Header("Shrink")]
    public ShrinkPattern shrinkPattern = ShrinkPattern.OuterRing;

    [Header("Spawn")]
    public SpawnOverride[] spawnOverrides;  // nullならデフォルト四隅配置
}

[System.Serializable]
public struct PresetTile
{
    public GridPos position;
    public TileState state;
    public int warpPairId;  // ワープマス用ペアID
}
```

### 2.2 新タイル種

現在の `TileState` enum に追加するギミックタイル。

| タイル種 | 見た目 | 説明 |
|---------|--------|------|
| **Gas (ガス)** | 緑の霧 | 通行可能。炎ボムまたは炎タイルに隣接すると引火し、隣接ガスに連鎖延焼する |
| **Bedrock (岩盤)** | 黒い岩 | 通行不能。ボムで破壊できない。崩落もしない。ステージ縮小でも残る |
| **Warp (ワープマス)** | 紫の渦 | 通行可能。踏むと対応するワープマスに即座にテレポートする |
| **EternalFire (消えない炎)** | 青い炎 | 通常の炎と同じダメージだが自然消火しない。ステージ縮小でのみ消滅 |

#### TileState enum の拡張

```csharp
public enum TileState : byte
{
    Normal,
    OnFire,
    Collapsing,
    Collapsed,
    PermanentlyDestroyed,
    Wall,
    // --- 新ギミック ---
    Gas,
    Bedrock,
    Warp,
    EternalFire,
}
```

### 2.3 ボムとギミックの相互作用マトリクス

ギミック追加時に最もバグを引き起こしやすいのがボムとの相互作用。全パターンを明示する。

#### 炎ボム × ギミック

| 対象タイル | 飛行中の衝突 | 効果範囲に含まれた場合 |
|-----------|-------------|---------------------|
| Normal | — | OnFire に変化 |
| Wall | 着弾（停止） | 壁を破壊 → Normal → OnFire（壁貫通強化時のみ貫通） |
| **Gas** | 通過（通行可能なため） | **引火**: Gas → OnFire に変化、さらに隣接 Gas に連鎖 |
| **Bedrock** | 着弾（停止） | **効果なし**: 破壊されない、炎も載らない |
| **Warp** | 通過 | **ワープマスは炎上**: Warp → OnFire → 消火後 Warp に戻る |
| **EternalFire** | 通過 | **上書きしない**: 既に EternalFire なら変化なし |
| OnFire | 通過 | 持続時間リセット |
| Collapsing | 通過 | OnFire に上書き（炎 + 崩落の同時状態は持たない → 炎優先） |
| Collapsed | 通過 | 効果なし（床がない） |
| PermanentlyDestroyed | 通過 | 効果なし |

#### 滑落ボム × ギミック

| 対象タイル | 飛行中の衝突 | 効果範囲に含まれた場合 |
|-----------|-------------|---------------------|
| Normal | — | Collapsing に変化 |
| Wall | 着弾（停止） | 壁を破壊 → Collapsing（デフォルトで壁貫通） |
| **Gas** | 通過 | **ガスを吹き飛ばす**: Gas → Collapsing（引火はしない、物理的に崩落） |
| **Bedrock** | 着弾（停止） | **効果なし**: 崩落させられない |
| **Warp** | 通過 | **ワープマスを崩落**: Warp → Collapsing → 復帰後 Warp に戻る |
| **EternalFire** | 通過 | **崩落で消火**: EternalFire → Collapsing（復帰後は Normal） |
| OnFire | — | Collapsing に変化（炎は消える） |
| Collapsing | — | 効果なし（既に崩落中） |
| Collapsed | — | 効果なし |
| PermanentlyDestroyed | — | 効果なし |

### 2.4 ガスの連鎖延焼ルール

ガスは炎ボムとの組み合わせがステージギミックの核。

```text
引火トリガー:
  1. 炎ボムの効果範囲にガスマスが含まれた場合
  2. OnFire タイルに4方向隣接するガスマスがある場合

連鎖処理:
  1. 引火したガスマスを OnFire に変更
  2. そのマスに4方向隣接するガスマスを探索（BFS）
  3. 隣接ガスにも引火（0.1秒/マスの遅延で段階的に広がる）
  4. 引火後の炎は通常の炎と同じ持続時間（3.5秒）

重要:
  - 滑落ボムではガスに引火しない（崩落させるのみ）
  - ガスは通行可能（プレイヤー・スライムが通れる）
  - ガスマス上で炎ダメージを受けるのは引火後のみ
```

### 2.5 ワープマスのルール

```text
テレポート条件:
  - プレイヤーまたはスライムがワープマスに移動した瞬間に発動
  - 対応するペア先のワープマスに即座に移動
  - ペア先が通行不能（崩落中等）の場合はワープしない
  - 占有チェックは行わない（プレイヤー・スライムの重なりは既に許可されているため）

ボムとの関係:
  - ボムの飛行はワープマスの影響を受けない（通過する）
  - 効果範囲にワープマスが含まれた場合、ワープマス自体にも効果が適用される

状態遷移:
  - 炎上後は消火で Warp に戻る（TileTimerService にワープ復帰を追加）
  - 崩落後は復帰で Warp に戻る
  - 永久消滅では消える（ステージ縮小）
  - ペアの片方が永久消滅したら、もう片方は Normal に変化（ワープ不能）
```

### 2.6 岩盤のルール

```text
特性:
  - 通行不能（壁と同じ）
  - ボム飛行を停止させる（壁と同じ）
  - ボム効果範囲で破壊されない（壁と異なる）
  - ステージ縮小でも消滅しない（PermanentlyDestroyed にならない）
  - 壁貫通強化の影響を受けない（効果が岩盤で止まる）

用途:
  - ステージのランドマーク
  - 安全な遮蔽物
  - 通路を制限する地形デザイン
```

### 2.7 消えない炎のルール

```text
特性:
  - Normal タイルに対してステージ設計時に配置、またはギミックで生成
  - 通常の炎と同じダメージ（接触1 + 滞在1/秒）
  - 自然消火しない
  - 滑落ボムで崩落させると消える（復帰後は Normal）
  - ステージ縮小で PermanentlyDestroyed になれば消える

用途:
  - 危険地帯の固定化
  - 通路の封鎖（滑落ボムでのみ解除可能）
  - 戦略的な地形ハザード
```

### 2.8 TileState の状態遷移図（ギミック追加後）

```text
              ┌─ Gas ← (ステージ設計で配置)
              │   │
              │   ├─ [炎ボム効果] → OnFire (→ 隣接Gas連鎖引火)
              │   └─ [滑落ボム効果] → Collapsing → Collapsed → Normal (ガスは復帰しない)
              │
Normal ──────┼─ [炎ボム] → OnFire → (3.5秒) → Normal
              │                │
              │                └─ [滑落ボム] → Collapsing
              │
              ├─ [滑落ボム] → Collapsing → Collapsed → (5秒) → Normal
              │
              ├─ [ステージ縮小] → PermanentlyDestroyed
              │
Wall ─────────┼─ [ボム効果] → Normal → (ボム効果適用)
              │
Bedrock ──────┤  (状態遷移なし。何をされても Bedrock のまま)
              │
Warp ─────────┼─ [炎ボム] → OnFire → (3.5秒) → Warp
              ├─ [滑落ボム] → Collapsing → Collapsed → (5秒) → Warp
              └─ [ステージ縮小] → PermanentlyDestroyed (ペア先も Normal に)

EternalFire ──┼─ [滑落ボム] → Collapsing → Collapsed → (5秒) → Normal
              └─ [ステージ縮小] → PermanentlyDestroyed
```

### 2.9 実装上の注意: 基底タイル種と上書きタイル状態の分離

現在の `TileState` は「基底のタイル種」と「一時的な状態」が混在している。ギミック追加で以下の問題が起きる:

- Gas マスが炎上 → 消火後、Gas に戻るべきか Normal に戻るべきか？
- Warp マスが崩落 → 復帰後、Warp に戻るべき

**解決案**: 基底タイルタイプ(`TileType`) と現在の状態(`TileCondition`) を分離する。

```csharp
// 基底のタイル種（ステージ設計時に決定、試合中は原則変わらない）
public enum TileType : byte
{
    Normal,
    Wall,
    Bedrock,
    Gas,
    Warp,
}

// 現在の状態（試合中にリアルタイムで変化する）
public enum TileCondition : byte
{
    Intact,              // 無事（通行可能）
    OnFire,              // 炎上中
    EternalFire,         // 消えない炎
    Collapsing,          // 崩落中
    Collapsed,           // 崩落済み（復帰待ち）
    PermanentlyDestroyed, // 永久消滅
}

// StageModel 内部
struct TileData
{
    public TileType Type;       // 基底種
    public TileCondition Condition; // 現在状態
    public int WarpPairId;      // Warp用ペアID（-1 = なし）
}
```

この分離により:
- Gas マスが炎上 → 消火後、`Type=Gas` + `Condition=Intact` → ガスに戻る
- Warp マスが崩落 → 復帰後、`Type=Warp` + `Condition=Intact` → ワープに戻る
- Wall が破壊 → `Type` を `Normal` に変更（壁は基底種として消える）
- Bedrock は `Condition` がどう変わっても `Type=Bedrock` で通行不能を維持

復帰ルール:
- **Gas**: 炎上→消火後 Gas に戻る、崩落→復帰後も Gas に戻る（将来のガスボム等でGasマスを活用するため）
- **Warp**: 炎上→消火後 Warp に戻る、崩落→復帰後も Warp に戻る
- **その他（Normal, Wall破壊後など）**: 復帰後は Normal に戻る

**注意**: この分離は既存コードへの影響が大きい。`TileState` を参照している全箇所（Resolver, Query, Presentation 等）に変更が波及する。段階的に移行するか、一括で切り替えるかは実装時に判断する。

### 2.10 ステージ案

| ステージ名 | サイズ | 人数 | 特徴 |
|-----------|--------|------|------|
| **Standard** | 30x30 | 2-4 | 現行。ランダム壁、外周縮小 |
| **Arena** | 20x20 | 2-4 | 小型密戦。縮小が速く効く |
| **Grand Colosseum** | 40x40 | 2-4 | 広大。スライム多め |
| **Gas Works** | 30x30 | 2-4 | ガスマスが多い。炎ボムで大連鎖が狙える |
| **Fortress** | 30x30 | 2-4 | 岩盤で仕切られた4部屋構造。通路の奪い合い |
| **Warp Maze** | 30x30 | 2-4 | 複数ワープマスが散在。奇襲と逃走のステージ |
| **Volcano** | 30x30 | 2-4 | 中央に消えない炎の帯。滑落ボムでのみ道を開ける |
| **Pillars** | 30x30 | 2-4 | 岩盤ブロック(3x3)が等間隔配置。見通しが良い |

---

## 3. 演出改善

### 3.1 炎の残り時間の可視化

**現状の問題**: 炎がいつ消えるかわからず、回避判断ができない。

**解決策**: 炎の残り時間に応じてスプライト/VFXの見た目を段階的に変化させる。

```text
炎の残り時間フェーズ:
  100% - 50%  : 赤い炎（通常） — 明るく揺らめく
   50% - 20%  : オレンジの炎 — やや小さく
   20% -  0%  : 小さな火の粉 — 明滅する

視覚的手がかり:
  - 炎のスケールを残り時間に比例して縮小（1.0 → 0.3）
  - 明滅速度を残り時間が少ないほど速くする
  - 色を 赤 → オレンジ → 黄色 にグラデーション
```

#### 実装方針

```csharp
// TileTimerService が各 OnFire タイルの残り時間を管理（既存）
// → 残り時間比率を Presentation に公開する

// StageModel に追加
public float GetFireRemainingRatio(GridPos pos);  // 0.0〜1.0

// TileFireVfxPool / TileView で比率に応じた見た目更新
// → Tick ごとに更新（フレーム単位で色・スケール補間）
```

**Domain 層への影響**: `TileTimerService` は既に残り時間を管理している。比率の公開メソッド追加のみ。ロジックの変更はなし。

### 3.2 崩落予告演出（ステージ縮小）

**現状の問題**: 20秒ごとの外周崩落が突然起き、プレイヤーが巻き込まれやすい。

**解決策**: 崩落の数秒前からタイルに予告演出を出す。

```text
崩落予告のタイムライン:
  T-5秒 : 外周1列のタイルに「ヒビ」テクスチャが重なり始める
  T-3秒 : タイルが小刻みに震え始める（振幅増加）
  T-1秒 : タイルが赤く点滅
  T-0秒 : 崩落実行（既存の StageShrinkService）

視覚的手がかり:
  - 予告対象タイルに USS class `.tile--shrink-warning` を…ではなく
    SpriteRenderer ベースなので、Presentation 層でシェイク + 色変化
  - 予告タイルのスプライトにヒビ割れオーバーレイを追加
  - 縮小範囲を示す赤い枠線を表示（各プレイヤーのカメラビュー内）
```

#### 実装方針

```csharp
// MatchPhaseScheduler の残り時間から駆動
// → 残り5秒で ShrinkWarningPresenter に通知

public sealed class ShrinkWarningPresenter
{
    // MatchClock.Remaining を購読
    // 残り時間が warningThreshold 以下になったら
    // → StageBounds.GetOuterRing() の次の崩落対象を取得
    // → TileView に警告エフェクトを適用

    private const float WarningStartSeconds = 5f;
}
```

**Domain 層への影響**: なし。`MatchClock.Remaining` と `StageBounds` は既に公開されている。Presentation 層のみの追加。

### 3.3 崩落タイルの復帰予告

**現状の問題**: 崩落したタイルがいつ復帰するかわからない。

**解決策**: 復帰が近いタイルに視覚的変化を出す。

```text
復帰予告:
  復帰2秒前 : 崩落穴から薄い光が漏れ始める
  復帰1秒前 : 光が強くなる + 地面がせり上がるモーション開始
  復帰時    : タイル完全復帰
```

### 3.4 消えない炎の視覚的区別

通常の炎（赤/オレンジ）と消えない炎（青）を明確に区別する。

```text
通常の炎: 赤〜オレンジ、時間で減衰
消えない炎: 青い炎、減衰しない、ゆっくり揺らめく
```

### 3.5 ボム効果範囲プレビュー（将来検討）

ボム飛行中に着弾予測地点の効果範囲を薄く表示する。実装コストが高いため将来課題。

---

## 4. 実装ロードマップ

### Phase A: 4P対応（Domain → DI → Presentation）

| Step | 内容 | 依存 |
|------|------|------|
| A-1 | `PlayerId` 拡張 | — |
| A-2 | `MatchPlayers` コレクション化 | A-1 |
| A-3 | `StageConfig` SO 導入 + スポーン位置外部化 | — |
| A-4 | `MatchLifetimeScope` ループ生成化 | A-2, A-3 |
| A-5 | `UpgradeSelectionState` コレクション化 | A-1 |
| A-6 | 勝利条件 N人対応 | A-2 |
| A-7 | Camera N分割 | A-4 |
| A-8 | UI ペイン動的生成 | A-4 |
| A-9 | Input Map 動的化 | A-4 |

### Phase B: ステージギミック（Domain → Infrastructure → Presentation）

| Step | 内容 | 依存 |
|------|------|------|
| B-1 | `TileType` / `TileCondition` 分離 | — |
| B-2 | `StageModel` 内部を `TileData` 構造に移行 | B-1 |
| B-3 | 既存 Resolver / Query / Presentation の `TileState` 参照を移行 | B-2 |
| B-4 | Bedrock 実装（最もシンプル） | B-3 |
| B-5 | EternalFire 実装 | B-3 |
| B-6 | Gas + 連鎖延焼実装 | B-3 |
| B-7 | Warp 実装 | B-3 |
| B-8 | `StageConfig` にプリセットタイル対応追加 | B-4〜B-7 |
| B-9 | ステージバリエーション作成 | B-8 |

### Phase C: 演出改善（Presentation のみ）

| Step | 内容 | 依存 |
|------|------|------|
| C-1 | 炎残り時間比率の公開 | — |
| C-2 | 炎の段階的減衰表現 | C-1 |
| C-3 | 崩落予告演出 (ShrinkWarningPresenter) | — |
| C-4 | 崩落復帰予告 | — |
| C-5 | 消えない炎の青エフェクト | B-5 |

### Phase 順序

```text
Phase C (演出改善) は Domain 変更なしなので並行可能
  ↓
Phase A (4P対応) — Domain 基盤
  ↓
Phase B (ギミック) — Domain 大改修 (TileState 分離)
  ↓
ネットワーク対戦
```

**推奨**: Phase C は既存コードへの影響が小さいので先に着手可能。Phase A と Phase B は Domain 層の変更が重なるため、A → B の順が安全。

---

## 5. リスクと注意点

### TileState 分離の影響範囲

`TileState` は現在 249 テスト中の多数で参照されている。`TileType` + `TileCondition` への分離は**全テストに波及する可能性がある**。一括変更のため、ブランチを分けて慎重に進めること。

### ワープのエッジケース

- ワープ先が炎上中 → ワープする（到着時にダメージ判定）
- ワープ中にボムが着弾 → ワープ完了後に判定
- 強制移動中のワープ → ワープしない（強制移動が優先）
- 占有チェックは不要（プレイヤー・スライムの重なりは既に許可済み）
