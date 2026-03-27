# FLOOR BREAKER — オンライン対戦 (Photon Fusion 2 Host Mode) 移行計画

## 1. 方針

### 1.1 目標

ローカル 2P 対戦のゲーム体験を維持したまま、オンライン 1v1 対戦に対応する。
Photon Fusion 2 の **Host Mode** を採用し、ホストが全ゲームロジックを実行する。

### 1.2 選定理由

| 方式 | 評価 |
|------|------|
| **Fusion 2 Host Mode** | 既存 Domain 層がホスト側でそのまま実行できる。専用サーバー不要 |
| Fusion 2 Server Mode | 専用サーバーが必要。1v1 には過剰 |
| Fusion 2 Shared Mode | 共有状態（タイル 900 マス、ボム判定）の権威が曖昧になる |
| Quantum | ECS への全面書き換えが必要。既存コード資産が活かせない |

### 1.3 設計原則

1. **Domain 層は変更しない** — pure C# のまま、ホスト側でそのまま実行する
2. **ネットワーク層は Infrastructure に閉じる** — `NetworkRunner`, `NetworkBehaviour`, 同期アダプターは全て Infrastructure
3. **固定 Tick で駆動する** — `deltaTime` ベースから Fusion の `FixedUpdateNetwork` (固定 Tick) に移行し、決定論性を保証する
4. **R3 は Presentation 専用** — ネットワーク同期には使わない。`[Networked]` → R3 への一方向フロー
5. **ローカル対戦との共存** — ネットワーク無しでも動作するよう、入力・Tick の抽象化を維持する

---

## 2. 権威モデル

### 2.1 ホスト権威の範囲

ホストが **全ゲーム状態の唯一の権威** を持つ。クライアントは状態を受信して表示する。

| 状態カテゴリ | 権威 | 同期方式 | 備考 |
|---|---|---|---|
| タイル状態 (900 マス) | ホスト | イベント (RPC) + 定期スナップショット | 差分送信を基本に、フォールバックとして全盤面同期 |
| プレイヤー位置 | ホスト確定 / クライアント予測 | `[Networked]` + 補間 | 0.2 秒間隔の離散移動のため予測なしも検討可 |
| プレイヤー HP・コイン | ホスト | `[Networked]` | チート防止 |
| プレイヤー向き | ホスト | `[Networked]` | |
| ボム飛行状態 | ホスト | `[Networked]` | 飛行開始/着弾をイベントで送信 |
| ボムクールダウン | ホスト | `[Networked]` | |
| ボム爆風スプレッド | ホスト | イベント (RPC) | タイル変更として送信 |
| スライム状態 | ホスト | `[Networked]` + イベント | スポーン/死亡はイベント、位置は `[Networked]` |
| フェーズ・タイマー | ホスト | `[Networked]` | 単一タイマー原則を維持 |
| 強化候補 | ホスト | RPC | ホスト側で RNG → クライアントに選択肢を送信 |
| 強化選択結果 | ホスト検証 | RPC (入力 → 結果) | クライアントの選択をホストが検証・適用 |
| PlayerBuild | ホスト | `[Networked]` or RPC | 強化フェーズ完了時にのみ変更 |
| 無敵状態 | ホスト | `[Networked]` | |
| 強制移動状態 | ホスト | `[Networked]` | |
| ステージ縮小範囲 | ホスト | `[Networked]` | |

### 2.2 クライアント予測の方針

**原則: 予測しない。** 理由:

- 移動間隔が 0.2 秒（5 Hz）であり、RTT 100ms 以下なら入力遅延が体感で許容範囲
- グリッド移動は離散的なため、ロールバック時のラバーバンドが目立ちやすい
- ボム発射・強化選択は予測不要（ホスト確定を待つ）

RTT が高い環境 (100ms+) での操作感が問題になった場合のみ、移動予測を段階的に導入する。

---

## 3. Tick アーキテクチャ

### 3.1 現状の問題

現在の `MatchTickRunner` は `ITickable` (VContainer) で毎フレーム呼ばれ、`ITimeProvider.DeltaTime` を使う。
フレームレートが異なるクライアント間で `deltaTime` が異なるため、float 演算の蓄積で状態が乖離する。

### 3.2 移行後の Tick 構造

```
FixedUpdateNetwork() [ホスト側のみ実行]
│
├─ GetInput(out FloorBreakerInput input)  ← 両プレイヤーの入力を取得
│
├─ InputDispatcher.Dispatch(input)        ← 入力を Domain サービスに変換
│   ├─ PlayerMoveService.TryMove()
│   └─ BombFlightTracker.StartFlight() / ReleaseBomb()
│
├─ MatchPhaseScheduler.Tick(Runner.DeltaTime)  ← 固定 deltaTime
│   ├─ MatchClock.Tick()
│   ├─ TileTimerService.Tick()
│   ├─ BombCooldownState.Tick() x2
│   ├─ InvulnerabilityState.Tick() x2
│   ├─ ForcedMoveState.Tick() x2
│   ├─ SlimeTickService.Tick()
│   ├─ FireDamageTickService.Tick()
│   ├─ BombFlightTracker.Tick()
│   └─ BombEffectSpreadService.Tick()
│
└─ StateSynchronizer.Sync()               ← [Networked] プロパティを更新

Render() [全クライアント]
│
├─ Presentation 層の更新（補間含む）
├─ R3 ReactiveProperty の購読による UI 更新
└─ カメラ追従
```

### 3.3 固定 Tick レート

Fusion 2 のデフォルト Tick レート（60 Hz）を使用する。
`Runner.DeltaTime` は常に `1/60 ≈ 0.01667` 秒で固定されるため、全サービスの float 演算が決定論的になる。

### 3.4 ローカル対戦との共存

ローカル対戦時は `NetworkRunner` を起動せず、従来の `MatchTickRunner` (VContainer ITickable) で駆動する。
Domain 層は入力ソースと Tick ソースに依存しないため、両モードで共通。

```
オンライン: FixedUpdateNetwork() → MatchPhaseScheduler.Tick(fixedDt)
ローカル:   MatchTickRunner.Tick() → MatchPhaseScheduler.Tick(deltaTime)
```

---

## 4. 入力設計

### 4.1 NetworkInput 構造体

```csharp
public struct FloorBreakerInput : INetworkInput
{
    // 移動
    public Direction8 MoveDirection;    // 8方向 (3 bit)
    public NetworkBool MoveHeld;        // ホールド中か

    // ボム
    public NetworkBool BreakBombPressed;
    public NetworkBool BreakBombReleased;
    public NetworkBool FireBombPressed;
    public NetworkBool FireBombReleased;

    // ダッシュ
    public NetworkBool DashTriggered;
    public Direction8 DashDirection;

    // 強化フェーズ
    public UpgradeInputAction UpgradeAction;
    // None / SelectCard0 / SelectCard1 / SelectCard2 / Reroll / Skip
}
```

合計: 約 4 byte。グリッドゲームの強み。

### 4.2 入力フロー

```
クライアント:
  PlayerInputAdapter (InputSystem)
    → OnInput() コールバックで FloorBreakerInput に変換
    → Fusion が自動送信

ホスト:
  FixedUpdateNetwork()
    → GetInput(player, out FloorBreakerInput input)
    → NetworkInputDispatcher が Domain サービスを呼ぶ
```

### 4.3 入力と GameplayInputBridge の関係

現在の `GameplayInputBridge` はホールドリピート移動（`InputBufferTime`, `InitialRepeatDelay`）を実装している。
これはネットワーク環境でも**入力側（クライアント）で処理**し、「移動したい」という意思だけを `NetworkInput` に載せる。

```
クライアント:
  ホールドリピートロジック → MoveHeld = true, MoveDirection = dir
  (0.2秒ごとに MoveHeld がセットされる)

ホスト:
  MoveHeld && MoveDirection → PlayerMoveService.TryMove()
```

---

## 5. 状態同期の詳細設計

### 5.1 同期アダプター層

Domain 層の状態を Fusion の `[Networked]` プロパティに橋渡しする Infrastructure 層のアダプターを設計する。
Domain 層は一切変更しない。

```
[新規] Infrastructure/Network/
  ├─ NetworkMatchRunner.cs        ← NetworkBehaviour, FixedUpdateNetwork 駆動
  ├─ NetworkInputCollector.cs     ← OnInput() で InputSystem → NetworkInput 変換
  ├─ NetworkInputDispatcher.cs    ← ホスト側で NetworkInput → Domain サービス呼び出し
  ├─ NetworkPlayerState.cs        ← [Networked] プレイヤー状態 ↔ PlayerModel 同期
  ├─ NetworkStageState.cs         ← [Networked] タイル状態 ↔ StageModel 同期
  ├─ NetworkBombState.cs          ← [Networked] ボム状態 ↔ BombFlightTracker 同期
  ├─ NetworkSlimeState.cs         ← [Networked] スライム状態 ↔ SlimeRegistry 同期
  ├─ NetworkMatchState.cs         ← [Networked] フェーズ・タイマー ↔ MatchClock 同期
  └─ NetworkUpgradeBridge.cs      ← RPC で強化候補・選択を送受信
```

### 5.2 プレイヤー状態の同期

```csharp
// NetworkPlayerState.cs (NetworkBehaviour)
[Networked] public int Hp { get; set; }
[Networked] public int Coins { get; set; }
[Networked] public GridPos Position { get; set; }
[Networked] public Direction8 Facing { get; set; }
[Networked] public float MoveSpeed { get; set; }
[Networked] public NetworkBool IsInvulnerable { get; set; }
[Networked] public NetworkBool IsForced { get; set; }
[Networked] public GridPos ForcedTarget { get; set; }
[Networked] public float BreakCooldown { get; set; }
[Networked] public float FireCooldown { get; set; }
// Build は変更頻度が低いため RPC で送信
```

同期方向: **ホスト → 全クライアント（一方向）**

```
ホスト FixedUpdateNetwork() 後:
  NetworkPlayerState.Hp = playerModel.Stats.CurrentHp.CurrentValue;
  NetworkPlayerState.Position = playerModel.CurrentPosition;
  ...

クライアント Render():
  R3 ReactiveProperty を更新 → UI 自動反映
```

### 5.3 タイル状態の同期

タイル変更は頻度が高い（ボム爆風で一度に複数タイル変更）が、全盤面を毎 Tick 送るのは非効率。

**方式: イベント駆動 + 定期フルスナップショット**

```
通常時:
  StageModel.TileChanged イベント → RPC で (GridPos, TileState) を送信
  クライアント: 受信したら表示用 StageModel に反映

フォールバック:
  N Tick ごと（例: 300 Tick = 5 秒）に全盤面をスナップショット送信
  クライアント: 受信したら表示用 StageModel を上書き → ズレを修正
```

全盤面スナップショット = 30x30 × 1 byte = 900 byte（圧縮なしでも軽量）。

### 5.4 ボム飛行の同期

ボム飛行は毎 Tick の float 演算で距離を蓄積するため、Tick 同期が重要。

**方式: 発射・着弾のみ同期**

```
ホスト:
  StartFlight() → RPC: BombFlightStarted(Owner, Origin, Direction, Spec)
  Land()        → RPC: BombLanded(Owner, LandingPos, Type, EffectRange)

クライアント:
  BombFlightStarted → ローカルで飛行演出を開始（Presentation 層）
  BombLanded        → 飛行演出を停止、着弾 VFX を再生
```

飛行中のタイル単位の位置は同期しない。Presentation 層が速度と経過時間からローカル補間する。

### 5.5 スライムの同期

```
スポーン:  ホスト → RPC: SlimeSpawned(Id, Type, Position)
移動:      ホスト → [Networked] Position per slime (NetworkArray or StructArray)
死亡:      ホスト → RPC: SlimeKilled(Id, Position, KillerPlayerId)
攻撃:      ホスト → RPC: SlimeAttacked(AttackerId, TargetPosition)
```

スライム数の上限: `900 × 3% = 27` 体。`NetworkArray<GridPos>` で十分。

### 5.6 強化フェーズの同期

```
ホスト:
  UpgradePhaseUseCase.Start()
    → RNG で候補生成
    → RPC: UpgradeChoicesGenerated(PlayerId, UpgradeDefinition[3])

クライアント:
  選択 UI を表示
  入力 → NetworkInput.UpgradeAction = SelectCard0 / Reroll / Skip

ホスト:
  UpgradeAction を受信
    → UpgradeDraftService.SelectChoice() / Reroll() / Skip()
    → 結果を RPC で通知: UpgradePurchased(PlayerId, UpgradeId)
    → 両者完了 → Phase 遷移
```

---

## 6. R3 ReactiveProperty とネットワークの統合

### 6.1 問題

Domain 層の `ReactiveProperty` (R3) と Fusion の `[Networked]` プロパティが二重管理になる。

### 6.2 解決: 一方向フロー

```
[Networked] int Hp (Fusion が同期)
  ↓ OnChanged コールバック
PlayerStats._currentHp.Value = newValue (R3 ReactiveProperty)
  ↓ Subscribe
HUD 表示更新
```

**ルール:**
- `[Networked]` が正（ネットワーク権威）
- `ReactiveProperty` は UI 通知のためのローカルミラー
- 書き込みは `[Networked]` → `ReactiveProperty` の一方向のみ
- Domain 層は `ReactiveProperty` を直接変更し続ける（ホスト側では従来通り）
- 同期アダプターが Domain の値を `[Networked]` に転写する

### 6.3 同期アダプターのパターン

```csharp
// NetworkPlayerStateAdapter.cs
public class NetworkPlayerStateAdapter : NetworkBehaviour
{
    [Networked] public int Hp { get; set; }

    // ホスト: Domain → [Networked]
    public void SyncFromDomain(PlayerModel model)
    {
        Hp = model.Stats.CurrentHp.CurrentValue;
    }

    // クライアント: [Networked] → Domain (表示用ミラー)
    public override void Render()
    {
        if (!Object.HasStateAuthority)
        {
            _localPlayerStats.SetHpMirror(Hp);
        }
    }
}
```

---

## 7. 接続・マッチメイキングフロー

### 7.1 フロー

```
タイトル画面
  ├─ 「2P ローカル対戦」→ ネットワーク無し、従来フロー
  ├─ 「vs CPU」→ ネットワーク無し、CPU AI フロー
  └─ 「オンライン対戦」
       ├─ 「部屋を作る」→ ホストとして NetworkRunner 起動
       │    → ルーム作成 → ルームコード表示 → 相手待ち
       └─ 「部屋に入る」→ クライアントとして接続
            → ルームコード入力 → 接続 → マッチ開始
```

### 7.2 ロビー設計

初期実装ではマッチメイキングは**ルームコード方式**のみ。
自動マッチメイキングは将来対応。

### 7.3 タイトル UI 拡張

```
TitleRoot.uxml:
  既存: 「2P 対戦」「vs CPU」「観戦モード (Coming Soon)」
  追加: 「オンライン対戦」
         → 「部屋を作る」ボタン
         → 「部屋に入る」ボタン + ルームコード入力欄
```

---

## 8. ディレクトリ構成

```
Assets/App/
  Features/
    Network/                              ← 新規 Feature
      Infrastructure/
        NetworkMatchRunner.cs             ← NetworkBehaviour, FixedUpdateNetwork
        NetworkInputCollector.cs          ← InputSystem → NetworkInput
        NetworkInputDispatcher.cs         ← NetworkInput → Domain
        NetworkPlayerStateAdapter.cs      ← Player 状態同期
        NetworkStageStateAdapter.cs       ← タイル状態同期
        NetworkBombStateAdapter.cs        ← ボム状態同期
        NetworkSlimeStateAdapter.cs       ← スライム状態同期
        NetworkMatchStateAdapter.cs       ← フェーズ・タイマー同期
        NetworkUpgradeBridge.cs           ← 強化フェーズ RPC
        NetworkConnectionService.cs       ← 接続管理
        RoomCodeService.cs                ← ルームコード生成・入力
      Application/
        MatchMode.cs                      ← Local / Cpu / Online enum
        NetworkMatchConfig.cs             ← 接続設定
      Presentation/
        NetworkLobbyPresenter.cs          ← ロビー UI
        NetworkStatusPresenter.cs         ← 接続状態表示

    Input/
      Application/
        IInputSource.cs                   ← 新規: 入力ソースの抽象化
        LocalInputSource.cs              ← 既存 GameplayInputBridge をラップ
        NetworkInputSource.cs            ← NetworkInput から変換

  Bootstrap/
    MatchLifetimeScope.cs                 ← MatchMode に応じた DI 切り替え
```

### 8.1 asmdef

```
App.Network (新規)
  参照: App.Shared.Domain, App.Shared.Application, App.Player, App.Stage,
        App.Bombs, App.Slimes, App.Upgrades, App.MatchFlow, Fusion
```

---

## 9. 想定される問題と対策

### 9.1 タイル状態同期ズレ (難易度: 高)

**問題:** `BombEffectSpreadService` が複数 Tick にわたってタイルを段階的に変更する。RPC のロスや順序乱れでクライアントの盤面が壊れる。

**対策:**
- タイル変更 RPC は **Reliable (信頼性保証)** で送信
- 定期フルスナップショット（5 秒ごと）で累積ズレを修正
- クライアントは自前でスプレッドシミュレーションを行わない

### 9.2 R3 と [Networked] の二重管理 (難易度: 高)

**問題:** 値の書き込み先が 2 箇所になり、不整合が発生する。

**対策:**
- Domain 層は変更しない（ホスト側で従来通り `ReactiveProperty` を更新）
- 同期アダプターが `[Networked]` ← Domain を毎 Tick 転写
- クライアント側は `[Networked]` → `ReactiveProperty` を Render() で転写
- **書き込みの方向を厳密に一方向に制限**する

### 9.3 VContainer と Fusion の DI 競合 (難易度: 高)

**問題:** Fusion の `NetworkObject` は `Runner.Spawn()` で生成され、VContainer のコンテナに登録されない。

**対策:**
- `NetworkBehaviour` は Presentation / Infrastructure 層に限定
- Domain サービスは VContainer 管理のまま
- `NetworkBehaviour.Spawned()` で VContainer のコンテナから手動で参照を取得

```csharp
public override void Spawned()
{
    // LifetimeScope から参照取得
    var scope = LifetimeScope.Find<MatchLifetimeScope>();
    _scheduler = scope.Container.Resolve<MatchPhaseScheduler>();
}
```

### 9.4 ボムのホールド/リリースタイミング (難易度: 中)

**問題:** クライアントの「pressed」「released」がネットワーク遅延で歪む。

**対策:**
- `NetworkInput` に pressed / released を bool で含める
- ホストは「pressed を受けた Tick の状態」で `StartFlight()` を実行
- 飛距離は `MinFlightDistance` で担保される（即リリースでも最低 3 マス飛ぶ）

### 9.5 強化フェーズのデッドロック (難易度: 中)

**問題:** 片方の選択入力がロストし、タイムアウトまで進行しない。

**対策:**
- タイムアウトは**ホストの Tick カウント基準のみ**
- クライアントのタイマー表示はホストの `[Networked] RemainingTime` を表示
- 選択入力は `NetworkInput` で毎 Tick 送信（RPC ではない）
- ホストが `UpgradeAction != None` を受けたら即処理

### 9.6 シーン遷移 (難易度: 中)

**問題:** `SceneManager.LoadScene()` と Fusion の `Runner.LoadScene()` の競合。

**対策:**
- オンラインモード時は全て `Runner.LoadScene()` を使用
- ローカルモード時は従来通り `SceneManager.LoadScene()`
- シーン遷移サービスを抽象化: `ISceneTransitionService`

### 9.7 切断・再接続 (難易度: 中)

**問題:** 試合中にクライアントが切断した場合の処理。

**対策 (初期実装):**
- 切断検知 → 相手の勝利として試合終了
- 再接続は初期実装では非対応（将来対応）
- ホスト切断 → 試合無効（ホストマイグレーションは将来対応）

### 9.8 クライアント予測のラバーバンド (難易度: 低、予測しない方針なら発生しない)

**問題:** 予測移動がホスト確定と食い違い、プレイヤーが瞬間移動する。

**対策:**
- 初期実装では**予測しない**方針（前述）
- RTT が問題になった場合のみ、移動予測 + スムーズ補正を導入

---

## 10. 実装フェーズ

### Phase 0: 基盤準備 (ネットワーク無し)

既存コードのリファクタリング。ネットワーク移行を容易にするための準備。

1. **Tick ソースの抽象化**
   - `MatchTickRunner` が `deltaTime` を外部から受け取れるようにする
   - `Time.deltaTime` への直接依存を排除（既にほぼ達成済み）

2. **入力ソースの抽象化**
   - `IInputSource` インターフェースを導入
   - `GameplayInputBridge` を `LocalInputSource` としてラップ
   - CPU AI も `IInputSource` 実装にリファクタ

3. **MatchMode enum の導入**
   - `Local` / `Cpu` / `Online` を明確に分離
   - `MatchLifetimeScope` が MatchMode に応じて DI を切り替え

4. **シーン遷移の抽象化**
   - `ISceneTransitionService` を導入
   - ローカル実装: `SceneManager.LoadScene()`
   - ネットワーク実装: `Runner.LoadScene()`（Phase 1 で実装）

### Phase 1: 接続とロビー

1. **Photon Fusion 2 パッケージ導入**
2. **NetworkRunner のセットアップ**
   - `NetworkConnectionService` 実装
   - ルーム作成 / 参加の基本フロー
3. **ルームコード方式のロビー**
   - `RoomCodeService` 実装
   - タイトル UI にオンラインボタン追加
4. **接続テスト**
   - 2 クライアントが同一ルームに接続できることを確認

### Phase 2: 入力同期

1. **`FloorBreakerInput` 構造体の定義**
2. **`NetworkInputCollector`** 実装
   - `INetworkRunnerCallbacks.OnInput()` で InputSystem → NetworkInput 変換
3. **`NetworkInputDispatcher`** 実装
   - ホスト側で NetworkInput → `PlayerMoveService.TryMove()` / `BombFlightTracker` 呼び出し
4. **動作確認**
   - 2 クライアントの入力がホストに届き、移動・ボム発射が実行されることを確認

### Phase 3: 状態同期

1. **`NetworkPlayerStateAdapter`** 実装
   - プレイヤー位置・HP・コイン・向き・クールダウン・無敵・強制移動
2. **`NetworkStageStateAdapter`** 実装
   - タイル変更 RPC + 定期スナップショット
3. **`NetworkMatchStateAdapter`** 実装
   - フェーズ・残り時間
4. **`NetworkBombStateAdapter`** 実装
   - 発射・着弾イベント
5. **`NetworkSlimeStateAdapter`** 実装
   - スポーン・移動・死亡
6. **R3 同期アダプター**
   - `[Networked]` → `ReactiveProperty` の一方向フロー実装
7. **動作確認**
   - 2 クライアントで同一の盤面・プレイヤー状態・スライムが見えることを確認

### Phase 4: 強化フェーズ

1. **`NetworkUpgradeBridge`** 実装
   - ホスト: RNG で候補生成 → RPC で送信
   - クライアント: 選択 → NetworkInput で送信
   - ホスト: 選択を検証・適用 → 結果 RPC
2. **タイムアウト同期**
   - ホスト Tick 基準のタイマー
3. **動作確認**
   - 2 クライアントで強化選択 → 適用 → フェーズ復帰が正常に動作

### Phase 5: 仕上げ

1. **切断処理**
   - 切断検知 → 試合終了
2. **リマッチフロー**
   - `Runner.LoadScene("Match")` でのリマッチ
3. **UI の仕上げ**
   - 接続中表示、待機中表示、エラー表示
4. **遅延体験の確認**
   - 意図的に遅延を入れてプレイテスト
   - 問題があれば移動予測を Phase 6 で導入

### Phase 6 (将来): 品質向上

- 移動予測 + スムーズ補正
- 自動マッチメイキング
- 再接続対応
- ホストマイグレーション
- レイテンシ表示

---

## 11. 帯域見積もり

### 11.1 入力 (クライアント → ホスト)

- `FloorBreakerInput`: 約 4 byte × 60 Tick/s = **240 byte/s**

### 11.2 状態同期 (ホスト → クライアント)

| データ | サイズ | 頻度 | bps |
|--------|--------|------|-----|
| プレイヤー状態 ×2 | 約 40 byte | 60 Hz | 2,400 |
| ボム飛行イベント | 約 20 byte | 数回/秒 | 約 100 |
| タイル変更 RPC | 約 4 byte × N | イベント時 | 約 200 |
| スライム位置 ×27 | 約 220 byte | 60 Hz | 13,200 |
| フェーズ・タイマー | 約 8 byte | 60 Hz | 480 |
| 全盤面スナップショット | 900 byte | 0.2 Hz | 180 |

**合計: 約 17 KB/s** — 極めて軽量。

---

## 12. テスト戦略

### 12.1 各 Phase の検証項目

| Phase | 検証 |
|-------|------|
| 0 | 既存 EditMode テスト全件グリーン。ローカル対戦が壊れていないこと |
| 1 | 2 クライアントが同一ルームに接続・切断できること |
| 2 | 両者の移動・ボム発射がホスト側で正しく処理されること |
| 3 | クライアント側で正しい盤面・HP・コインが表示されること |
| 4 | 強化フェーズの選択・タイムアウトが正しく動作すること |
| 5 | 切断・リマッチが正常に処理されること |

### 12.2 遅延テスト

Fusion の `NetworkProjectConfig` で `Simulation > Latency` を設定し、意図的に遅延を入れてテストする。

- 50ms: 国内 LAN 相当 → 問題なし想定
- 100ms: 国内 Wi-Fi 相当 → 操作感確認
- 200ms: 国際通信相当 → ボム操作の体感確認

---

## 13. 制約と前提

- **Unity 6.3** + **Photon Fusion 2.0.12** を使用
- **Host Mode** 固定（Server Mode / Shared Mode は使わない）
- **初期実装は 1v1 のみ**（将来 N 人対応の余地は残す）
- **Time.timeScale は使用禁止**（既存方針を継続）
- **Domain 層は変更しない**（ネットワーク対応は Infrastructure に閉じる）
- **ローカル対戦・vs CPU との共存**を維持する
