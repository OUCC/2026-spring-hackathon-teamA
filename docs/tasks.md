# FLOOR BREAKER — 実装タスク一覧

CLAUDE.md §21 の実装優先順に従い、全タスクをフェーズ／マイルストーン単位で整理する。
現在の `Assets/Script/` 以下はレガシーコード（Tilemap 依存・シングルトン型）であり、`Assets/App/` 以下に新アーキテクチャで全面書き直す。

凡例:
- **[Domain]** — pure C#、Unity API 非依存
- **[Application]** — ユースケース・オーケストレーション。Domain とインターフェースにのみ依存
- **[Infrastructure]** — Unity API の実装・アダプタ
- **[Presentation]** — MonoBehaviour、VFX、UI
- **[Bootstrap]** — VContainer LifetimeScope、Installer
- **[Test]** — EditMode / PlayMode テスト

---

## Phase 0: プロジェクト基盤 ✅

> 目標: ディレクトリ構造・アセンブリ定義・ビルド基盤を整え、以降の作業が個別にコンパイルできる状態にする。

### T-0.1 ディレクトリ構造の作成 [Bootstrap]
- CLAUDE.md §3 に従い `Assets/App/` 配下のツリーを作成
- ディレクトリ: `Bootstrap/`, `Shared/`, `Features/{MatchFlow,Stage,Player,Bombs,Slimes,Upgrades,Cameras,Input,UI}`, `Scenes/`, `ScriptableObjects/`, `Tests/`
- 各 feature ディレクトリには `Domain/`, `Application/`, `Infrastructure/`, `Presentation/` を配置
- **成果物**: 仕様通りの空ディレクトリツリー

### T-0.2 アセンブリ定義の作成 [Bootstrap]
- CLAUDE.md §16 に従い `.asmdef` ファイルを作成:
  - `App.Shared.asmdef` — 参照: R3, UniTask
  - `App.Stage.asmdef` — 参照: App.Shared, R3, UniTask
  - `App.Player.asmdef` — 参照: App.Shared, R3, UniTask
  - `App.Bombs.asmdef` — 参照: App.Shared, App.Stage, R3, UniTask
  - `App.Slimes.asmdef` — 参照: App.Shared, App.Stage, App.Player, R3, UniTask
  - `App.Upgrades.asmdef` — 参照: App.Shared, App.Player, App.Bombs, R3, UniTask
  - `App.MatchFlow.asmdef` — 参照: App.Shared, App.Stage, App.Player, App.Bombs, App.Slimes, App.Upgrades, R3, UniTask
  - `App.Input.asmdef` — 参照: App.Shared, App.Player, App.Bombs, Unity.InputSystem
  - `App.UI.asmdef` — 参照: App.Shared, App.MatchFlow, App.Player, App.Upgrades, R3, UniTask, UnityEngine.UIElementsModule
  - `App.Bootstrap.asmdef` — 参照: 全 feature asmdef, VContainer
  - `App.Tests.EditMode.asmdef` — 参照: 全 feature asmdef, UnityEngine.TestRunner
  - `App.Tests.PlayMode.asmdef` — 参照: 全 feature asmdef, UnityEngine.TestRunner
- **受入条件**: 全 asmdef がエラーなくコンパイルされ、依存方向が守られている（Domain が Infrastructure/Presentation を参照しない）

### T-0.3 初期シーンの作成 [Infrastructure]
- `Assets/App/Scenes/Title.unity`, `Match.unity`, `Result.unity` を作成
- `Match.unity` には 2 プレイヤー分割画面用のカメラ設定（左右ビューポート）を含める
- EditorBuildSettings にシーンを登録
- **成果物**: エディタで読み込み可能な 3 シーン

### T-0.4 Input System アクションアセットの設定 [Infrastructure]
- `Assets/App/ScriptableObjects/Configs/FloorBreakerActions.inputactions` を新規作成
- CLAUDE.md §12 に従ったアクションマップ:
  - `Gameplay`: Move (Vector2, 8方向), FallBombHold (Button, started/canceled), FireBombHold (Button, started/canceled)
  - `UpgradeUI_P1`: Navigate (Vector2), Submit (Button), Skip (Button), Reroll (Button)
  - `UpgradeUI_P2`: Navigate (Vector2), Submit (Button), Skip (Button), Reroll (Button)
  - `System`: Pause (Button), Confirm (Button), Cancel (Button)
- 両プレイヤーのゲームパッドにバインド（P1: gamepad 0, P2: gamepad 1）
- **受入条件**: アクションアセットがコンパイルされ、Input System デバッガーで全アクションマップが確認できる

### T-0.5 バランス設定 ScriptableObject の作成 [Domain/Infrastructure]
- 全仕様パラメータを持つ `BalanceConfig.cs` ScriptableObject を作成:
  - グリッドサイズ (30)、プレイヤー初期 HP (10)、初期速度 (1.0)
  - 滑落ボム: CD 4秒, ダメージ 2, 崩落時間 3秒, 復帰時間 5秒, 最大飛行距離 3, 効果範囲 1, 壁貫通 true
  - 炎ボム: CD 2秒, ダメージ 1, 継続ダメージ 1/秒, 持続時間 3.5秒, 最大飛行距離 3, 効果範囲 1, 壁貫通 false
  - 壁: シード率 8%, 成長確率 40%, 目標被覆率 20%, スポーン保護 5x5
  - スライム: 目標比率 3%, スポーン間隔 5秒, 索敵範囲 5, 速度比 0.5, 攻撃 CD 1秒, 出現比 10:1:1
  - 強化フェーズ: 間隔 20秒, 選択肢数 3, 制限時間 10秒, リロールコスト 1
  - ステージ縮小: 20秒ごとに外周 1列
  - 移動速度上限 2.0, 滑落ボム CD 下限 1.0秒, 炎ボム CD 下限 0.5秒
- `Assets/App/ScriptableObjects/Balance/BalanceConfig.asset` にアセットインスタンスを作成
- **成果物**: 全ゲームパラメータが一元管理され、コード中にマジックナンバーがない

---

## Phase 1: 共通プリミティブ ✅

> 目標: 全 feature で共有する基礎的な値型・インターフェースを確立する。

### T-1.1 GridPos 値型の実装 [Domain]
- ファイル: `Assets/App/Shared/Domain/Grid/GridPos.cs`
- `readonly struct GridPos : IEquatable<GridPos>`、`int X, Y`
- 算術演算子: `+`, `-`, `*`（スカラー）
- `ManhattanDistance`, `ChebyshevDistance` メソッド
- `Neighbors4`, `Neighbors8`（`ReadOnlySpan<GridPos>` または配列を返す）
- 静的メソッド `ToWorldPosition(GridPos) -> Vector2`（単純な算術、Grid コンポーネント不使用）
- 静的メソッド `FromWorldPosition(Vector2) -> GridPos`
- **受入条件**: 算術・距離・隣接のユニットテストが全て通る

### T-1.2 方向型の実装 [Domain]
- ファイル: `Assets/App/Shared/Domain/Grid/Direction8.cs`
- `enum Direction8 { N, NE, E, SE, S, SW, W, NW }`
- ファイル: `Assets/App/Shared/Domain/Grid/CardinalDirection4.cs`
- `enum CardinalDirection4 { N, E, S, W }`
- 拡張メソッド: `ToOffset() -> GridPos`, `Opposite()`, `IsCardinal()`
- **受入条件**: 全 8 方向で正しいオフセット生成、反対方向が正しい

### T-1.3 PlayerId の実装 [Domain]
- ファイル: `Assets/App/Shared/Domain/Primitives/PlayerId.cs`
- `readonly struct PlayerId`（`int` をラップ、1 または 2）
- 静的プロパティ `Player1`, `Player2`
- `Opponent` プロパティ
- **受入条件**: `PlayerId.Player1.Opponent == PlayerId.Player2`

### T-1.4 GamePhase 列挙型の実装 [Domain]
- ファイル: `Assets/App/Shared/Domain/Primitives/GamePhase.cs`
- `enum GamePhase { Title, MatchRunning, StageShrink, UpgradePhase, Result }`
- **成果物**: 列挙型ファイル 1 つ

### T-1.5 MatchClock の実装 [Domain]
- ファイル: `Assets/App/Shared/Domain/Timing/MatchClock.cs`
- 経過時間、現在フェーズの残り時間を追跡
- `ReactiveProperty<float>` で `Remaining` を公開（R3）
- `ReactiveProperty<GamePhase>` で `CurrentPhase` を公開
- メソッド: `Tick(float deltaTime)`, `Pause()`, `Resume()`, `Reset()`
- `PhaseInterval` はバランス設定から取得（20秒）
- **受入条件**: Tick が正しくカウントダウンし、フェーズ遷移時に Observable が発火する

### T-1.6 TileCoordRange の実装 [Domain]
- ファイル: `Assets/App/Shared/Domain/Grid/TileCoordRange.cs`
- グリッドの矩形領域（最小/最大境界）を表現
- `Contains(GridPos)`, `GetAllPositions()`, `Shrink(int amount)`
- **受入条件**: 30x30 に対する `Shrink(1)` で 28x28 の範囲が得られる

### T-1.7 共有インターフェースの実装 [Application]
- ファイル: `Assets/App/Shared/Application/Interfaces/ITimeProvider.cs` — `float DeltaTime { get; }`, `float ElapsedTime { get; }`
- ファイル: `Assets/App/Shared/Application/Interfaces/IRandomProvider.cs` — `int Range(int min, int maxExclusive)`, `float Value01()`
- ファイル: `Assets/App/Shared/Application/Interfaces/IAudioService.cs` — `void PlaySE(string id)`, `void PlaySEAtPosition(string id, Vector2 worldPos)`
- **成果物**: 実装詳細を持たないクリーンなインターフェース

### T-1.8 UnityTimeProvider / SeededRandomProvider の実装 [Infrastructure]
- ファイル: `Assets/App/Shared/Infrastructure/UnityTime/UnityTimeProvider.cs` — `Time.deltaTime`, `Time.time` をラップ
- ファイル: `Assets/App/Shared/Infrastructure/Random/SeededRandomProvider.cs` — `System.Random` をラップし決定論的テストに対応
- **受入条件**: 両方が各インターフェースを実装し、SeededRandomProvider は同一シードで決定論的に動作する

---

## Phase 2: ステージ Domain ✅

> 目標: グリッドワールドモデル、壁生成、タイル状態管理、ステージ縮小、安全タイル探索を構築する。

### T-2.1 TileState の実装 [Domain]
- ファイル: `Assets/App/Features/Stage/Domain/TileState.cs`
- `enum TileState { Normal, OnFire, Collapsing, Collapsed, PermanentlyDestroyed, Wall }`
- **成果物**: 列挙型ファイル 1 つ

### T-2.2 StageModel の実装 [Domain]
- ファイル: `Assets/App/Features/Stage/Domain/StageModel.cs`
- 30x30 の `TileState` を内部 2D 配列またはディクショナリで管理
- タイル単位の `ReactiveProperty` または `Subject<(GridPos, TileState)>` で変更通知
- メソッド:
  - `GetTileState(GridPos) -> TileState`
  - `SetTileState(GridPos, TileState)`
  - `IsPassable(GridPos) -> bool`（Normal または OnFire）
  - `IsInBounds(GridPos) -> bool`
  - `GetAliveTileCount() -> int`
  - `GetCurrentBounds() -> TileCoordRange`
- Observable: `TileChanged -> Observable<(GridPos pos, TileState oldState, TileState newState)>`
- コンストラクタは `TileCoordRange` で初期境界を受け取る
- **受入条件**: 全タイル状態クエリが正しく、状態変更のたびに Observable が発火する

### T-2.3 StageBounds の実装 [Domain]
- ファイル: `Assets/App/Features/Stage/Domain/StageBounds.cs`
- 現在の有効矩形境界を追跡
- `Shrink()` で各辺 1 行/列ずつ縮小
- `GetOuterRing() -> IReadOnlyList<GridPos>` で破壊対象タイルを返す
- **受入条件**: 30x30 から 1 回縮小後、境界が [1,1]〜[28,28]、外周リングは 116 タイル

### T-2.4 WallGenerationService の実装 [Domain]
- ファイル: `Assets/App/Features/Stage/Domain/WallGenerationService.cs`
- コンストラクタで `IRandomProvider`、バランス設定（壁パラメータ）を受け取る
- `Generate(TileCoordRange bounds, GridPos p1Spawn, GridPos p2Spawn) -> HashSet<GridPos>`
- シードパス: 全タイルの 8% をランダムに壁配置
- 成長パス: シード壁の隣接 4 方向を 40% の確率で壁化、全体が約 20% になるまで繰り返す
- 各スポーン地点の 5x5 範囲を除外
- **受入条件**: シード固定のユニットテストで壁数が約 20%（許容範囲内）、スポーンゾーンがクリア

### T-2.5 StageShrinkService の実装 [Domain]
- ファイル: `Assets/App/Features/Stage/Domain/StageShrinkService.cs`
- `ShrinkOuterRing(StageModel model) -> IReadOnlyList<GridPos>`（破壊された位置を返す）
- 対象タイルを `PermanentlyDestroyed` に設定
- `StageBounds` を更新
- **受入条件**: 縮小後、外周タイルが PermanentlyDestroyed になり、境界が縮小されている

### T-2.6 SafeTileSearchService の実装 [Domain]
- ファイル: `Assets/App/Features/Stage/Domain/SafeTileSearchService.cs`
- `FindSafeTile(StageModel model, GridPos from, HashSet<GridPos> occupied) -> GridPos?`
- まず 3x3 優先探索（8 近傍、最近を優先）
- 候補がなければ BFS フォールバック
- 「安全」の定義: Collapsing / Collapsed / PermanentlyDestroyed / Wall / occupied のいずれでもない
- **受入条件**: 周囲を囲まれたタイルで正しく BFS にフォールバック、完全に安全タイルがなければ null を返す

### T-2.7 StageQueryService の実装 [Domain]
- ファイル: `Assets/App/Features/Stage/Domain/StageQueryService.cs`
- StageModel に対する便利クエリ:
  - `GetPassableTiles() -> IReadOnlyList<GridPos>`
  - `GetTilesInCross(GridPos center, int range, bool penetrateWalls) -> IReadOnlyList<GridPos>`
  - `RaycastGrid(GridPos from, Direction8 dir, int maxDist) -> (GridPos hitPos, int distance, TileState hitTileState)`
- **受入条件**: range=1 の十字パターンで正しく 5 タイルを返す、レイキャストが壁で停止する

### T-2.8 タイル崩落/復帰タイマーロジックの実装 [Domain]
- ファイル: `Assets/App/Features/Stage/Domain/TileTimerService.cs`
- タイルごとのタイマーを追跡: 崩落時間 (3秒)、復帰遅延 (5秒)、炎持続時間 (3.5秒)
- `Tick(float dt)` で全アクティブタイマーを進行
- 崩落タイマー満了時: タイルを Collapsed に設定
- 復帰タイマー満了時: タイルを Normal に戻す
- 炎タイマー満了時: タイルを Normal に戻す
- タイマー完了の Observable
- **受入条件**: Normal → Collapsing (3秒) → Collapsed (5秒) → Normal の遷移が正しいタイミングで行われる

### T-2.9 ステージ Domain ユニットテスト [Test]
- ファイル: `Assets/App/Tests/EditMode/Stage/`
- テスト対象: WallGenerationService、StageShrinkService、SafeTileSearchService、StageQueryService の十字パターン、TileTimerService のタイミング
- **受入条件**: 全テストグリーン

---

## Phase 3: プレイヤー Domain ✅

> 目標: HP、コイン、位置、移動、無敵、強制移動、ボムビルドステータスを含むプレイヤー状態をモデル化する。

### T-3.1 PlayerStats の実装 [Domain]
- ファイル: `Assets/App/Features/Player/Domain/PlayerStats.cs`
- `ReactiveProperty<int> CurrentHp`, `ReactiveProperty<int> Coins`
- `float MoveSpeed`（デフォルト 1.0、上限 2.0）
- メソッド: `TakeDamage(int)`, `Heal(int)`, `AddCoins(int)`, `SpendCoins(int) -> bool`
- `IsDead -> ReadOnlyReactiveProperty<bool>`
- **受入条件**: ダメージは 0 にクランプ、回復は最大値にクランプ、コイン不足時は消費失敗

### T-3.2 PlayerBuild の実装 [Domain]
- ファイル: `Assets/App/Features/Player/Domain/PlayerBuild.cs`
- 全強化スタックを追跡: 炎ボム飛距離/範囲/ダメージ/CD/飛行時ダメージ(bool)/持続時間/壁貫通(bool)、滑落ボム飛距離/範囲/ダメージ/CD/崩落時間/飛行時ダメージ(bool)、移動速度ボーナス
- メソッド: `GetFallBombSpec() -> BombSpec`, `GetFireBombSpec() -> BombSpec`（基礎値＋強化から算出）
- `ApplyUpgrade(UpgradeId)` で対応するカウンタを変更
- **受入条件**: 強化の重ね掛けで算出スペックが正しく変化し、CD が下限を守る（炎 0.5秒、滑落 1.0秒）

### T-3.3 PlayerModel の実装 [Domain]
- ファイル: `Assets/App/Features/Player/Domain/PlayerModel.cs`
- 集約: `PlayerId`, `PlayerStats`, `PlayerBuild`
- `ReactiveProperty<GridPos> Position`
- `ReactiveProperty<Direction8> FacingDirection`（最後の入力方向）
- **成果物**: 他システムから参照される集約ルート

### T-3.4 InvulnerabilityState の実装 [Domain]
- ファイル: `Assets/App/Features/Player/Domain/InvulnerabilityState.cs`
- `bool IsInvulnerable`, `float RemainingDuration`
- `Activate(float duration)`, `Tick(float dt)`
- **受入条件**: 指定時間後に無敵が切れる

### T-3.5 ForcedMoveState の実装 [Domain]
- ファイル: `Assets/App/Features/Player/Domain/ForcedMoveState.cs`
- `bool IsForced`, `GridPos Target`, `float Duration`（仕様により約 1 秒）
- `Start(GridPos target, float duration)`, `Tick(float dt)`, `Complete()`
- 強制移動中はプレイヤー入力を受け付けない
- **受入条件**: 移動中は IsForced が true、完了後は false

### T-3.6 PlayerMoveService の実装 [Application]
- ファイル: `Assets/App/Features/Player/Application/PlayerMoveService.cs`
- 移動先の検証: 通行可能・未占有・範囲内であること
- PlayerBuild に基づく移動速度の適用
- PlayerModel.Position を更新
- 入力から FacingDirection を更新
- **受入条件**: 壁への移動は false を返す、8 方向の斜め移動が動作する

### T-3.7 PlayerDamageService の実装 [Application]
- ファイル: `Assets/App/Features/Player/Application/PlayerDamageService.cs`
- ダメージ適用前に無敵状態をチェック
- ダメージ時: 無敵を発動し、崩落タイル上であれば強制移動を発動
- 強制移動先の決定に SafeTileSearchService を使用
- **受入条件**: 無敵中はダメージなし、強制移動が正しく発動する

### T-3.8 プレイヤー Domain ユニットテスト [Test]
- ファイル: `Assets/App/Tests/EditMode/Player/`
- テスト対象: PlayerStats のダメージ/回復/コイン、PlayerBuild の強化スタックと CD 下限、InvulnerabilityState、ForcedMoveState
- **受入条件**: 全テストグリーン

---

## Phase 4: ボムリゾルバ ✅

> 目標: ボムの飛行、着弾、範囲解決、種類別効果を実装する。

### Phase 4 レビューメモ (2026-03-26)

- **前提タスク追加**: `BombType` enum を `Shared/Domain/Primitives/` に定義してから着手
- **BombSpec 組み立て**: PlayerBuild は生の値のみ保持済み。BombLaunchUseCase が PlayerBuild → BombSpec を構築する
- **BombAreaResolver**: StageQueryService.GetTilesInCross に委譲し、ロジック重複を避ける
- **スライム衝突**: BombLandingResolver はエンティティ位置の問い合わせを `Func<GridPos, bool>` で受け取り、Phase 5 で SlimeRegistry を注入
- **強制移動**: FallBombResolver の責務は「タイル崩落 + ダメージ値の返却」まで。強制移動は PlayerDamageService (Phase 3 実装済み) が担う
- **App.Bombs.asmdef**: Feature ルートに配置。参照: App.Shared.Domain, App.Shared.Application, App.Stage, App.Player

### T-4.1 BombSpec の実装 [Domain]
- ファイル: `Assets/App/Features/Bombs/Domain/Shared/BombSpec.cs`
- `readonly struct BombSpec`: MaxFlightDistance, EffectRange, Damage, Cooldown, HasFlightDamage, WallPenetration, Duration（炎用）, CollapseTime（滑落用）, RecoveryTime（滑落用）
- ファクトリメソッド: `CreateFallBombDefault()`, `CreateFireBombDefault()`
- **成果物**: イミュータブルなスペック値型

### T-4.2 BombFlightCommand の実装 [Domain]
- ファイル: `Assets/App/Features/Bombs/Domain/Shared/BombFlightCommand.cs`
- `GridPos Origin`, `Direction8 Direction`, `BombSpec Spec`, `PlayerId Owner`
- ボム発射の意図を表すコマンド
- **成果物**: コマンド用の値オブジェクト

### T-4.3 BombCooldownState の実装 [Domain]
- ファイル: `Assets/App/Features/Bombs/Domain/Shared/BombCooldownState.cs`
- プレイヤーごと・ボム種別ごとのクールダウン追跡
- `ReactiveProperty<float> FallBombRemaining`, `ReactiveProperty<float> FireBombRemaining`
- `StartCooldown(BombType, float duration)`, `Tick(float dt)`, `CanFire(BombType) -> bool`
- **受入条件**: クールダウンがカウントダウンし、クールダウン中は CanFire が false を返す

### T-4.4 BombAreaResolver の実装 [Domain]
- ファイル: `Assets/App/Features/Bombs/Domain/Shared/BombAreaResolver.cs`
- `Resolve(GridPos center, int range, bool penetrateWalls, StageModel model) -> IReadOnlyList<GridPos>`
- 中心から十字パターン（上下左右）を返す
- `penetrateWalls=false` の場合、各腕は壁で停止
- `penetrateWalls=true` の場合、壁を貫通して継続
- 常に中心タイルを含む
- **受入条件**: range=1 壁なし → 5 タイル、東方向 distance=1 に壁あり＋貫通なし → 4 タイル

### T-4.5 BombLandingResolver の実装 [Domain]
- ファイル: `Assets/App/Features/Bombs/Domain/Shared/BombLandingResolver.cs`
- `Resolve(BombFlightCommand cmd, StageModel model) -> GridPos landingPos`
- 発射元から方向に沿って各タイルをチェックしながら進行
- 停止条件: 壁・プレイヤー・スライムへの衝突、または最大飛行距離到達
- ボタンリリース時も停止（位置は外部から提供）
- **受入条件**: 2 タイル先に壁がある場合、最大距離 3 でもそこで着弾する

### T-4.6 FallBombResolver の実装 [Domain]
- ファイル: `Assets/App/Features/Bombs/Domain/FallBomb/FallBombResolver.cs`
- 着弾位置から BombAreaResolver で範囲算出（デフォルトで壁貫通 = true）
- 範囲内の各タイル: Collapsing に設定し、崩落タイマーを開始
- 範囲内のプレイヤー/スライム: ダメージを与え、強制移動を発動
- 範囲内の壁を破壊
- **受入条件**: 正しいタイルが崩落し、ダメージが適用され、壁が破壊される

### T-4.7 FireBombResolver の実装 [Domain]
- ファイル: `Assets/App/Features/Bombs/Domain/FireBomb/FireBombResolver.cs`
- 着弾位置から BombAreaResolver で範囲算出（デフォルトで壁貫通 = false、強化で変更可能）
- 範囲内の各タイル: OnFire に設定し、炎タイマーを開始
- 範囲内のエンティティに即時接触ダメージ
- 炎上タイルに滞在するエンティティへの継続ダメージは TileTimerService で追跡
- 範囲内の壁を破壊
- **受入条件**: デフォルトで炎が壁を貫通しない、壁が破壊される、接触ダメージが適用される

### T-4.8 BombLaunchUseCase の実装 [Application]
- ファイル: `Assets/App/Features/Bombs/Application/BombLaunchUseCase.cs`
- BombCooldownState でクールダウンを検証
- プレイヤー入力（位置、方向）から BombFlightCommand を生成
- ボタンリリースまたは最大距離到達時: BombLandingResolver で着弾を解決
- FallBombResolver または FireBombResolver にディスパッチ
- クールダウンを開始
- **受入条件**: クールダウン中は発射不可、正しいリゾルバにディスパッチされる

### T-4.9 ボム Domain ユニットテスト [Test]
- ファイル: `Assets/App/Tests/EditMode/Bombs/`
- テスト対象: BombAreaResolver のパターン、BombLandingResolver の壁衝突、FallBombResolver のダメージ＋崩落、FireBombResolver の炎＋ダメージ、BombCooldownState
- **受入条件**: 全テストグリーン

---

## Phase 5: スライム スポーン / AI

> 目標: スライムのスポーン、AI 行動、死亡/ドロップ解決を実装する。

### T-5.1 SlimeModel の実装 [Domain]
- ファイル: `Assets/App/Features/Slimes/Domain/SlimeModel.cs`
- `SlimeId`, `SlimeType`（Normal, Gold, Red）, `GridPos Position`, `bool IsAlive`
- `float AttackCooldownRemaining`
- **成果物**: 個別スライムのデータモデル

### T-5.2 SlimeRegistry の実装 [Domain]
- ファイル: `Assets/App/Features/Slimes/Domain/SlimeRegistry.cs`
- 全生存スライムを追跡し、位置によるルックアップを提供
- `Add(SlimeModel)`, `Remove(SlimeId)`, `GetAt(GridPos) -> SlimeModel?`, `GetAll()`, `AliveCount`
- **受入条件**: 追加/削除を正しく追跡する

### T-5.3 SlimeSpawnService の実装 [Domain]
- ファイル: `Assets/App/Features/Slimes/Domain/SlimeSpawnService.cs`
- 5 秒ごとに呼び出される
- 目標数 = `floor(生存タイル数 * 0.03)`
- 現在数 < 目標数の場合、差分をスポーン
- スポーン位置: 各プレイヤーから 5 マス以上離れたランダムな通行可能タイル
- 種類抽選: IRandomProvider で 10:1:1 比率（ノーマル:金色:赤色）
- **受入条件**: 900 タイルで目標数 27、距離制約を守る、多数回実行で比率がおおよそ正しい

### T-5.4 SlimeAiService の実装 [Domain]
- ファイル: `Assets/App/Features/Slimes/Domain/SlimeAiService.cs`
- スライムごと・ティックごとの処理:
  - プレイヤーが 5 マス以内: 最寄りプレイヤーに接近（半分の速度 → 2 ティックに 1 回移動、または端数アキュムレータ）
  - 隣接（上下左右 4 方向）: 攻撃（1 ダメージ、CD 1秒）
  - それ以外: 待機
- 移動経路探索: 優先軸に沿ったシンプルな貪欲法
- **受入条件**: プレイヤーに向かって移動、隣接時に攻撃、クールダウンを守る

### T-5.5 SlimeDropResolver の実装 [Domain]
- ファイル: `Assets/App/Features/Slimes/Domain/SlimeDropResolver.cs`
- スライム死亡時:
  - ノーマル: 撃破者にコイン 1 枚
  - 金色: 撃破者にコイン 5 枚
  - 赤色: 「無制限取得可能な強化」からランダムに 1 つを撃破者に即時付与
- ステージ縮小による死亡はドロップなし
- **受入条件**: 種類ごとに正しいドロップ、縮小死亡時はドロップなし

### T-5.6 スライム Domain ユニットテスト [Test]
- ファイル: `Assets/App/Tests/EditMode/Slimes/`
- テスト対象: SlimeSpawnService の目標数・スポーン距離、SlimeAiService の移動/攻撃、SlimeDropResolver のドロップ
- **受入条件**: 全テストグリーン

---

## Phase 6: 強化 Domain

> 目標: 強化カタログ、ドラフト/選択システム、適用サービス、出現ルールを実装する。

### T-6.1 UpgradeDefinition の実装 [Domain]
- ファイル: `Assets/App/Features/Upgrades/Domain/UpgradeDefinition.cs`
- `UpgradeId`（enum または string）, `string DisplayName`, `int Cost`, `StackRule`（Unlimited, OnceOnly）, `int MaxStack`（0=無制限）
- 仕様に一致する 15 の強化定義:
  - 炎ボム: 飛距離+1 (2c), 範囲+1 (3c), 威力+1 (3c), 飛行時ダメージ (5c,1回), 持続時間+2秒 (2c), 壁貫通 (6c,1回), CD-0.3秒 (2c)
  - 滑落ボム: 飛距離+1 (3c), 範囲+1 (4c), 威力+1 (4c), 飛行時ダメージ (6c,1回), 崩落時間+2秒 (3c), CD-0.5秒 (3c)
  - 汎用: 移動速度+0.2 (2c), HP回復3 (2c, HP≦5 のみ)
- **受入条件**: 全 15 定義が仕様テーブルと完全に一致する

### T-6.2 UpgradeCatalog の実装 [Domain]
- ファイル: `Assets/App/Features/Upgrades/Domain/UpgradeCatalog.cs`
- 全 `UpgradeDefinition` の静的またはインジェクタブルなレジストリ
- `GetById(UpgradeId) -> UpgradeDefinition`
- `GetAll() -> IReadOnlyList<UpgradeDefinition>`
- `GetUnlimitedStackables() -> IReadOnlyList<UpgradeDefinition>`（赤色スライムドロップ用）
- **成果物**: 一元管理カタログ

### T-6.3 UpgradeAvailabilityRule の実装 [Domain]
- ファイル: `Assets/App/Features/Upgrades/Domain/UpgradeAvailabilityRule.cs`
- プレイヤーに出現可能な強化を判定:
  - 取得済みの 1 回限り強化は除外
  - HP 回復は HP ≦ 5 のときのみ出現
  - 移動速度強化は上限 (2.0) 到達で除外
  - CD 強化は下限到達で除外
- `IsAvailable(UpgradeDefinition, PlayerModel) -> bool`
- **受入条件**: HP 6 で HP 回復が除外される、1 回限り強化が取得後に除外される

### T-6.4 UpgradeRollRule の実装 [Domain]
- ファイル: `Assets/App/Features/Upgrades/Domain/UpgradeRollRule.cs`
- UpgradeAvailabilityRule で候補プールを構築
- プールからランダムに 3 つの重複なし強化を選択
- IRandomProvider を使用
- **受入条件**: 常に 3 つの重複なし結果（プール < 3 の場合はそれ未満）、出現ルールを守る

### T-6.5 UpgradeDraftService の実装 [Domain]
- ファイル: `Assets/App/Features/Upgrades/Domain/UpgradeDraftService.cs`
- 強化フェーズ中のプレイヤーごとのドラフト状態
- `ReactiveProperty<IReadOnlyList<UpgradeDefinition>> CurrentChoices`（3 件）
- `GenerateChoices(PlayerModel)`: UpgradeRollRule を使用
- `Reroll(PlayerModel) -> bool`: コイン 1 枚消費、選択肢を再生成
- `SelectChoice(int index, PlayerModel) -> bool`: コストを検証し、成功を返す
- `Skip()`: 明示的スキップ
- `ReactiveProperty<DraftState> State`（Choosing, Selected, Skipped, TimedOut）
- **受入条件**: リロールでコイン消費、選択でコスト検証、状態遷移が正しい

### T-6.6 UpgradeApplyService の実装 [Application]
- ファイル: `Assets/App/Features/Upgrades/Application/UpgradeApplyService.cs`
- `Apply(UpgradeId, PlayerModel)` で PlayerBuild/PlayerStats に強化効果を適用
- UpgradeId ごとに switch またはストラテジーパターン:
  - 炎ボム飛距離: `build.FireFlightRange += 1`
  - 炎ボム範囲: `build.FireEffectRange += 1`
  - 炎ボム威力: `build.FireDamage += 1`
  - 炎ボム飛行時ダメージ: `build.FireFlightDamage = true`
  - 炎ボム持続時間: `build.FireDuration += 2.0f`
  - 炎壁貫通: `build.FireWallPenetration = true`
  - 炎ボム CD: `build.FireCooldown = max(0.5f, build.FireCooldown - 0.3f)`
  - 滑落ボム飛距離: `build.FallFlightRange += 1`
  - 滑落ボム範囲: `build.FallEffectRange += 1`
  - 滑落ボム威力: `build.FallDamage += 1`
  - 滑落ボム飛行時ダメージ: `build.FallFlightDamage = true`
  - 滑落ボム崩落時間: `build.FallCollapseTime += 2.0f`
  - 滑落ボム CD: `build.FallCooldown = max(1.0f, build.FallCooldown - 0.5f)`
  - 移動速度: `stats.MoveSpeed = min(2.0f, stats.MoveSpeed + 0.2f)`
  - HP 回復: `stats.Heal(3)`
- **受入条件**: 各強化が正しいステータスを正しい値と上限/下限で変更する

### T-6.7 強化 Domain ユニットテスト [Test]
- ファイル: `Assets/App/Tests/EditMode/Upgrades/`
- テスト対象: UpgradeAvailabilityRule の条件、UpgradeRollRule の重複なし、UpgradeDraftService のリロール/選択/スキップ、UpgradeApplyService の全 15 強化
- **受入条件**: 全テストグリーン

---

## Phase 7: マッチフェーズスケジューラ

> 目標: 20 秒周期・ステージ縮小・強化フェーズ・試合終了を駆動する単一オーケストレーターを構築する。

### T-7.1 MatchPhaseScheduler の実装 [Application]
- ファイル: `Assets/App/Features/MatchFlow/Application/MatchPhaseScheduler.cs`
- `ITickable`（VContainer）を実装
- `MatchClock` を所有
- 20 秒ごとの処理:
  1. ゲームを一時停止（フェーズを StageShrink に設定）
  2. `StageShrinkService.ShrinkOuterRing()` を呼び出し
  3. 縮小アニメーション待機（UniTask）
  4. フェーズを UpgradePhase に設定
  5. `UpgradePhaseUseCase` を開始
  6. 両プレイヤー完了または 10 秒タイムアウトまで待機（UniTask）
  7. ゲーム再開（フェーズを MatchRunning に設定）
- `ReactiveProperty<GamePhase>` を UI 向けに公開
- **受入条件**: 正しいタイミングでフェーズ遷移が発火し、一時停止/再開が正しく動作する

### T-7.2 UpgradePhaseUseCase の実装 [Application]
- ファイル: `Assets/App/Features/MatchFlow/Application/UpgradePhaseUseCase.cs`
- UpgradeDraftService で両プレイヤーのドラフトを作成
- `async UniTask RunAsync(CancellationToken ct)`
- 10 秒カウントダウンを開始
- 両プレイヤーの DraftState を監視
- 両者完了またはタイムアウトで完了
- タイムアウト時: 未完了プレイヤーを自動スキップ
- **受入条件**: 両者選択後に完了、タイムアウトでも完了、選択した強化が適用される

### T-7.3 MatchEndUseCase の実装 [Application]
- ファイル: `Assets/App/Features/MatchFlow/Application/MatchEndUseCase.cs`
- 両プレイヤーの HP を監視
- いずれかが 0 になった時: フェーズを Result に設定し、勝者を決定
- `ReactiveProperty<MatchResult>`（勝者の PlayerId）
- **受入条件**: 死亡でリザルト遷移、正しい勝者を識別

### T-7.4 MatchFlowOrchestrator の実装 [Application]
- ファイル: `Assets/App/Features/MatchFlow/Application/MatchFlowOrchestrator.cs`
- `IAsyncStartable`（VContainer）を実装
- 初期化シーケンス:
  1. WallGenerationService で壁を生成
  2. 壁を含めて StageModel を初期化
  3. 両 PlayerModel をスポーン位置で初期化
  4. MatchPhaseScheduler を開始
  5. SlimeSpawnService の定期チェックを開始
- 起動と破棄を統括
- **受入条件**: 全初期化がエラーなく実行され、全サービスが開始される

### T-7.5 SlimeTickService の実装 [Application]
- ファイル: `Assets/App/Features/Slimes/Application/SlimeTickService.cs`
- `ITickable` を実装
- 全生存スライムに対して SlimeAiService を呼び出し
- 5 秒間隔のスポーンタイマーを管理
- 間隔ごとに SlimeSpawnService を呼び出し
- **受入条件**: スライムが正しくティックし、5 秒ごとにスポーンチェックが実行される

### T-7.6 MatchFlow ユニットテスト [Test]
- ファイル: `Assets/App/Tests/EditMode/MatchFlow/`
- テスト対象: MatchPhaseScheduler のフェーズ遷移、UpgradePhaseUseCase のタイムアウト、MatchEndUseCase の死亡検出
- **受入条件**: 全テストグリーン

---

## Phase 8: 入力アダプタ

> 目標: Input System のアクションを Application 層のコマンドに接続する。

### T-8.1 PlayerInputAdapter の実装 [Infrastructure]
- ファイル: `Assets/App/Features/Input/Infrastructure/PlayerInputAdapter.cs`
- Input System の PlayerInput コンポーネント（プレイヤーごと）から読み取り
- Move アクションを `Direction8`（アナログスティックから 8 方向）に変換
- FallBombHold の started/canceled を `BombHoldCommand` に変換
- FireBombHold の started/canceled を `BombHoldCommand` に変換
- ボム照準用の「最後に入力した方向」を追跡
- **受入条件**: 8 方向入力が正しくマッピングされ、ホールド/リリースが検出される

### T-8.2 GameplayInputBridge の実装 [Application]
- ファイル: `Assets/App/Features/Input/Application/GameplayInputBridge.cs`
- PlayerInputAdapter からコマンドを受信
- PlayerMoveService と BombLaunchUseCase にディスパッチ
- フェーズに応じた制御: 強化フェーズ中・強制移動中は無効化
- **受入条件**: 強化フェーズ中は入力がブロックされ、ゲームプレイ中はサービスに入力が流れる

### T-8.3 UpgradeUIInputBridge の実装 [Application]
- ファイル: `Assets/App/Features/Input/Application/UpgradeUIInputBridge.cs`
- UpgradeUI アクションマップから Navigate/Submit/Skip/Reroll を受信
- UpgradeDraftService にディスパッチ
- 強化フェーズ中のみアクティブ
- **受入条件**: UI ナビゲーションが動作し、Submit で強化選択、Reroll でコイン消費

### T-8.4 InputMapSwitcher の実装 [Infrastructure]
- ファイル: `Assets/App/Features/Input/Infrastructure/InputMapSwitcher.cs`
- MatchPhaseScheduler の `GamePhase` を購読
- フェーズに応じてアクションマップを有効/無効化:
  - MatchRunning: Gameplay 有効、UpgradeUI 無効
  - UpgradePhase: Gameplay 無効、UpgradeUI 有効
  - Result: System のみ
- **受入条件**: フェーズごとに正しいアクションマップがアクティブ

---

## Phase 9: UI Toolkit HUD / オーバーレイ

> 目標: CLAUDE.md §11 に従い、全ランタイム UI を UI Toolkit で構築する。

### T-9.1 ルート UXML レイアウトの作成 [Presentation]
- ファイル: `Assets/App/Features/UI/RuntimeUI/UXML/MatchRoot.uxml`
- CLAUDE.md §11.2 に一致する構造:
  - TopLayer > SharedAnnouncements
  - MatchLayer > LeftHudRoot, RightHudRoot, CenterPhaseTimer
  - OverlayLayer > UpgradeOverlayRoot, PauseRoot, ResultRoot
- ファイル: `Assets/App/Features/UI/RuntimeUI/USS/MatchRoot.uss` — 基本スタイリング
- **受入条件**: UI Builder でレイアウトが描画され、左右分割が確認できる

### T-9.2 HUD UXML/USS の作成 [Presentation]
- ファイル: `Assets/App/Features/UI/RuntimeUI/UXML/PlayerHud.uxml`
- 要素: HP バー、コイン数、滑落ボム CD インジケーター、炎ボム CD インジケーター、取得済み強化一覧、フェーズタイマー
- P1（左）と P2（右）の両方にテンプレートを使用
- ファイル: `Assets/App/Features/UI/RuntimeUI/USS/PlayerHud.uss`
- **受入条件**: 両 HUD が正しい構造で表示され、ミラーレイアウト

### T-9.3 PlayerHudPresenter の実装 [Presentation]
- ファイル: `Assets/App/Features/UI/HUD/Presentation/PlayerHudPresenter.cs`
- PlayerStats.CurrentHp, Coins, BombCooldownState, PlayerBuild を購読（R3）
- HUD 要素を明示的に更新（バインディングではなく）
- destroy 時に購読を破棄
- **受入条件**: ダメージで HP バーが更新、コイン数が更新、CD インジケーターがアニメーション

### T-9.4 CenterTimerPresenter の実装 [Presentation]
- ファイル: `Assets/App/Features/UI/HUD/Presentation/CenterTimerPresenter.cs`
- MatchClock.Remaining を購読（R3）
- 次のフェーズまでの秒数を表示（20 からカウントダウン）
- **受入条件**: タイマーがカウントダウンし、各フェーズ後にリセットされる

### T-9.5 強化オーバーレイ UXML/USS の作成 [Presentation]
- ファイル: `Assets/App/Features/UI/RuntimeUI/UXML/UpgradeOverlay.uxml`
- 構造: LeftUpgradePane, RightUpgradePane, SharedCountdown, SharedRerollInfo
- 各ペインに 3 つの UpgradeCard スロット + Skip ボタン
- ファイル: `Assets/App/Features/UI/RuntimeUI/UXML/UpgradeCard.uxml` — 再利用テンプレート
- ファイル: `Assets/App/Features/UI/RuntimeUI/USS/UpgradeOverlay.uss`
- USS クラスによる状態表現: `.selectable`, `.selected`, `.unaffordable`, `.completed`, `.timed-out`
- **受入条件**: プレイヤーごとに 3 カードが描画され、状態が視覚的に区別できる

### T-9.6 UpgradeOverlayPresenter の実装 [Presentation]
- ファイル: `Assets/App/Features/UI/UpgradeOverlay/Presentation/UpgradeOverlayPresenter.cs`
- UpgradeDraftService.CurrentChoices と State を購読（R3）
- カードに強化名・コスト・説明を設定
- USS クラス切り替えで選択ハイライトを処理
- コイン不足の強化をグレーアウト
- カウントダウンタイマーを表示
- 一方のプレイヤーが完了時に「対戦相手を待っています」を表示
- オーバーレイ表示時に最初のカードに初期フォーカスを設定
- **受入条件**: カードが正しく表示され、選択が動作し、カウントダウンが表示される

### T-9.7 リザルトオーバーレイ UXML/USS の作成 [Presentation]
- ファイル: `Assets/App/Features/UI/RuntimeUI/UXML/ResultOverlay.uxml`
- 勝者表示、リマッチボタン、タイトルに戻るボタン
- ファイル: `Assets/App/Features/UI/RuntimeUI/USS/ResultOverlay.uss`
- **受入条件**: リザルト画面に勝者が表示され、ボタンが機能する

### T-9.8 ResultPresenter の実装 [Presentation]
- ファイル: `Assets/App/Features/UI/Result/Presentation/ResultPresenter.cs`
- MatchEndUseCase の結果を購読
- ResultOverlay の表示/非表示を切り替え
- リマッチ（Match シーン再読み込み）とタイトルに戻る（Title シーン読み込み）を処理
- **受入条件**: 試合終了時にリザルトが表示され、リマッチで試合が再開される

### T-9.9 MatchUIDocument セットアップの実装 [Presentation]
- ファイル: `Assets/App/Features/UI/RuntimeUI/Documents/MatchUIDocument.cs`
- UIDocument の GameObject 上の MonoBehaviour
- Screen Space Overlay + Scale with Screen Size の Panel Settings を参照
- 全プレゼンターに正しい VisualElement 参照を設定して初期化
- **受入条件**: 試合全体で単一の UIDocument、全プレゼンターが接続済み

---

## Phase 10: ステージ Presentation

> 目標: SpriteRenderer を使ってグリッドを視覚的に描画する（Tilemap 不使用）。

### T-10.1 TileView の実装 [Presentation]
- ファイル: `Assets/App/Features/Stage/Presentation/TileView.cs`
- SpriteRenderer 付き GameObject 上の MonoBehaviour
- `Initialize(GridPos)` で GridPos.ToWorldPosition により配置
- 自分の位置の StageModel.TileChanged を購読
- TileState に応じてスプライト/色を切り替え（Normal, OnFire, Collapsing, Wall, PermanentlyDestroyed）
- **受入条件**: タイルのビジュアルが状態と一致する

### T-10.2 StageViewFactory の実装 [Presentation]
- ファイル: `Assets/App/Features/Stage/Presentation/StageViewFactory.cs`
- 起動時に 30x30 グリッド分の全 TileView GameObject を生成
- タイル GameObject のプーリングと管理
- 各タイルに SpriteRenderer を使用（Tilemap 不使用）
- **受入条件**: 試合開始時に 900 個のタイル GameObject が表示される

### T-10.3 タイル崩落アニメーションの実装 [Presentation]
- ファイル: `Assets/App/Features/Stage/Presentation/TileCollapseAnimator.cs`
- DOTween で崩落アニメーション（縮小 + フェード + 落下）
- All In 1 Sprite Shader でディゾルブエフェクト
- TileState が Collapsing に変化した時にトリガー
- Normal に戻る時の復帰アニメーション
- **受入条件**: 崩落と復帰のアニメーションが視覚的に明確

### T-10.4 タイル上の炎 VFX の実装 [Presentation]
- ファイル: `Assets/App/Features/Stage/Presentation/TileFireVfx.cs`
- OnFire タイルに Epic Toon FX の炎パーティクルを使用
- TileState に基づきパーティクルシステムを生成/破棄
- **受入条件**: タイル状態に合わせて炎パーティクルが出現/消滅する

### T-10.5 ステージ縮小アニメーションの実装 [Presentation]
- ファイル: `Assets/App/Features/Stage/Presentation/StageShrinkAnimator.cs`
- 縮小フェーズ中に外周リングの崩落をアニメーション
- DOTween による連鎖的な崩落ウェーブエフェクト
- アニメーション完了時に完了する `UniTask` を返す（スケジューラが待機）
- **受入条件**: 外周リングが順次崩落し、UniTask が完了する

---

## Phase 11: プレイヤー Presentation

> 目標: プレイヤーの描画、移動アニメーション、ダメージフィードバックを処理する。

### T-11.1 PlayerView の実装 [Presentation]
- ファイル: `Assets/App/Features/Player/Presentation/PlayerView.cs`
- SpriteRenderer 付きの MonoBehaviour
- PlayerModel.Position を購読（R3）し、DOTween でスムーズに移動
- 方向に応じたスプライト反転
- **受入条件**: プレイヤーがグリッド位置間をスムーズに移動する

### T-11.2 PlayerDamageFeedback の実装 [Presentation]
- ファイル: `Assets/App/Features/Player/Presentation/PlayerDamageFeedback.cs`
- ダメージ時に Feel でヒットストップと画面シェイク
- All In 1 Sprite Shader でダメージフラッシュ（白フラッシュ）
- PlayerStats.CurrentHp の減少でトリガー
- **受入条件**: 全ダメージイベントで視覚フィードバックが出る

### T-11.3 ForcedMoveAnimator の実装 [Presentation]
- ファイル: `Assets/App/Features/Player/Presentation/ForcedMoveAnimator.cs`
- DOTween で強制移動をアニメーション（約 1 秒の弧を描く移動）
- アニメーション中はプレイヤー入力表示をブロック
- **受入条件**: 強制移動が視覚的にスムーズで、約 1 秒かかる

### T-11.4 InvulnerabilityVisual の実装 [Presentation]
- ファイル: `Assets/App/Features/Player/Presentation/InvulnerabilityVisual.cs`
- 無敵中のスプライト点滅
- All In 1 Sprite Shader のアウトラインエフェクト
- **受入条件**: 無敵中にプレイヤーが視覚的に区別できる

---

## Phase 12: ボム Presentation

> 目標: ボムの飛行、着弾エフェクト、効果範囲ビジュアルを描画する。

### T-12.1 BombFlightView の実装 [Presentation]
- ファイル: `Assets/App/Features/Bombs/Presentation/BombFlightView.cs`
- 発射元から着弾位置への放物線弧アニメーション
- DOTween で弧の軌道を処理
- ボム弾のスプライト
- **受入条件**: ボムが放物線を描いて飛行し、正しいグリッド位置に着弾する

### T-12.2 BombExplosionVfx の実装 [Presentation]
- ファイル: `Assets/App/Features/Bombs/Presentation/BombExplosionVfx.cs`
- 着弾時に Epic Toon FX の爆発/衝撃波を使用
- 滑落ボムと炎ボムで異なる VFX
- 影響タイルを短時間ハイライト
- **受入条件**: 爆発パーティクルが再生され、影響タイルがフラッシュする

### T-12.3 BombAreaHighlight の実装 [Presentation]
- ファイル: `Assets/App/Features/Bombs/Presentation/BombAreaHighlight.cs`
- ボムホールド中に影響範囲のプレビューを表示（オプションのポリッシュ）
- ボムの予測着弾地点に基づく十字パターンのハイライト
- **受入条件**: ボムボタン押下中に範囲プレビューが表示される

### T-12.4 BombSoundEffects の実装 [Infrastructure]
- ファイル: `Assets/App/Features/Bombs/Infrastructure/BombSoundEffects.cs`
- IAudioService で Medieval Fantasy SFX Bundle のサウンドを再生
- 発射音、飛行音、爆発音（種類ごとに異なる）
- **受入条件**: 正しいタイミングで音声が再生される

---

## Phase 13: スライム Presentation

> 目標: 種類に応じたビジュアルと死亡エフェクトでスライムを描画する。

### T-13.1 SlimeView の実装 [Presentation]
- ファイル: `Assets/App/Features/Slimes/Presentation/SlimeView.cs`
- SpriteRenderer 付きの MonoBehaviour
- 種類ごとの色分け: ノーマル（水色）、金色（金）、赤色（赤）
- SlimeModel.Position を購読しスムーズに移動
- **受入条件**: スライムが正しい色で描画され、スムーズに移動する

### T-13.2 SlimeViewFactory の実装 [Presentation]
- ファイル: `Assets/App/Features/Slimes/Presentation/SlimeViewFactory.cs`
- SlimeView の GameObject をインスタンス化/プーリング
- SlimeSpawnService のイベントで呼び出し
- スライム死亡時に破棄/リサイクル
- **受入条件**: Domain 状態に一致してスライムが出現/消滅する

### T-13.3 SlimeDeathVfx の実装 [Presentation]
- ファイル: `Assets/App/Features/Slimes/Presentation/SlimeDeathVfx.cs`
- 死亡時に Epic Toon FX のパーティクルを使用
- コイン/強化ドロップの視覚フィードバック
- **受入条件**: 死亡エフェクトが再生され、ドロップ表示が確認できる

---

## Phase 14: カメラシステム

> 目標: 分割画面のカメラ追従を設定する。

### T-14.1 分割画面カメラセットアップの実装 [Presentation]
- ファイル: `Assets/App/Features/Cameras/Presentation/SplitScreenCameraSetup.cs`
- 2 台のカメラ: P1（ビューポート 0,0,0.5,1）と P2（ビューポート 0.5,0,0.5,1）
- 各カメラは対応プレイヤーを中心に配置
- 仕様に従いビューポートは 10x10 タイル
- **受入条件**: 画面が正しく分割され、各カメラが正しいプレイヤーを表示する

### T-14.2 CameraFollowService の実装 [Presentation]
- ファイル: `Assets/App/Features/Cameras/Presentation/CameraFollowService.cs`
- PlayerModel.Position にスムーズ追従
- ステージ境界でクランプ（グリッド外を表示しない）
- DOTween またはシンプルな Lerp を使用
- **受入条件**: カメラがプレイヤーにスムーズに追従し、グリッド境界から出ない

### T-14.3 CameraShakeService の実装 [Presentation]
- ファイル: `Assets/App/Features/Cameras/Presentation/CameraShakeService.cs`
- Feel の MMCameraShaker で画面シェイクを実現
- トリガー: ボム爆発、プレイヤーダメージ、ステージ縮小
- イベント種類ごとに揺れの強度を変更
- **受入条件**: 正しいイベントでシェイクが発動し、カメラごとにシェイク（グローバルではない）

---

## Phase 15: Bootstrap / DI 配線

> 目標: VContainer の LifetimeScope で全てを結合する。

### T-15.1 ProjectLifetimeScope の実装 [Bootstrap]
- ファイル: `Assets/App/Bootstrap/ProjectLifetimeScope.cs`
- 登録: BalanceConfig, ITimeProvider (UnityTimeProvider), IRandomProvider (SeededRandomProvider), IAudioService
- Singleton ライフタイム
- **受入条件**: プロジェクトレベルのサービスが正しく解決される

### T-15.2 MatchLifetimeScope の実装 [Bootstrap]
- ファイル: `Assets/App/Bootstrap/MatchLifetimeScope.cs`
- Scoped で登録:
  - StageModel, StageBounds, WallGenerationService, StageShrinkService, SafeTileSearchService, StageQueryService, TileTimerService
  - PlayerModel x2（PlayerId でキー付き）, PlayerStats x2, PlayerBuild x2, InvulnerabilityState x2, ForcedMoveState x2
  - BombCooldownState x2, BombAreaResolver, BombLandingResolver, FallBombResolver, FireBombResolver, BombLaunchUseCase
  - SlimeRegistry, SlimeSpawnService, SlimeAiService, SlimeDropResolver, SlimeTickService
  - UpgradeCatalog, UpgradeAvailabilityRule, UpgradeRollRule, UpgradeDraftService x2, UpgradeApplyService
  - MatchFlowOrchestrator, MatchPhaseScheduler, UpgradePhaseUseCase, MatchEndUseCase
  - PlayerMoveService, PlayerDamageService
  - GameplayInputBridge, UpgradeUIInputBridge, InputMapSwitcher
- RegisterEntryPoint: MatchFlowOrchestrator (IAsyncStartable), MatchPhaseScheduler (ITickable), SlimeTickService (ITickable), TileTimerService (ITickable)
- **受入条件**: 全依存がエラーなく解決され、循環依存がない

### T-15.3 MatchPresentationInstaller の実装 [Bootstrap]
- ファイル: `Assets/App/Bootstrap/Installers/MatchPresentationInstaller.cs`
- Presentation 層コンポーネントの登録:
  - StageViewFactory, TileCollapseAnimator, TileFireVfx, StageShrinkAnimator
  - PlayerView x2, PlayerDamageFeedback x2, ForcedMoveAnimator, InvulnerabilityVisual
  - BombFlightView ファクトリ, BombExplosionVfx, BombSoundEffects
  - SlimeViewFactory, SlimeDeathVfx
  - SplitScreenCameraSetup, CameraFollowService, CameraShakeService
  - 全 HUD/オーバーレイプレゼンター
- **受入条件**: 全 Presentation サービスが解決され、View が正しくバインドされる

### T-15.4 TitleLifetimeScope の実装 [Bootstrap]
- ファイル: `Assets/App/Bootstrap/TitleLifetimeScope.cs`
- 最小構成: タイトル画面の UI プレゼンター、シーン遷移サービス
- **受入条件**: タイトルシーンが読み込まれ、Match への遷移が可能

### T-15.5 ResultLifetimeScope の実装 [Bootstrap]
- ファイル: `Assets/App/Bootstrap/ResultLifetimeScope.cs`
- 試合結果データを受け取り、リザルト UI プレゼンターを配線
- （注: リザルトがオーバーレイの場合は MatchLifetimeScope 内で処理される可能性あり）
- **受入条件**: 試合後にリザルトが正しく表示される

---

## Phase 16: オーディオ基盤

> 目標: Medieval Fantasy SFX Bundle を使った音声再生を実装する。

### T-16.1 AudioService の実装 [Infrastructure]
- ファイル: `Assets/App/Shared/Infrastructure/Audio/AudioService.cs`
- `IAudioService` を実装
- Medieval Fantasy SFX Bundle から SE クリップを読み込み・再生
- 位置音声に対応（2D ゲームだが分割画面の左右パンニング）
- サウンドカテゴリ: ボム発射、ボム爆発（滑落/炎）、崩落、炎、スライム死亡、スライム攻撃、プレイヤーダメージ、コイン取得、強化選択、フェーズ遷移、UI ナビゲーション/決定
- **受入条件**: 全 SE が正しいタイミングで再生され、オーディオ参照の欠落がない

### T-16.2 AudioClip カタログの作成 [Infrastructure]
- ファイル: `Assets/App/ScriptableObjects/Configs/AudioCatalog.asset`（ScriptableObject）
- 文字列 ID から SFX バンドルの AudioClip 参照へのマッピング
- **受入条件**: 必要な全サウンドがマッピング済み

---

## Phase 17: 統合テスト・E2E テスト

> 目標: タイトルから試合、リザルトまでのフルゲームループが動作することを検証する。

### T-17.1 統合テスト: フルマッチフロー [Test]
- 完全な試合サイクルを実行する PlayMode テスト:
  - 試合を初期化
  - 20 秒経過をシミュレート
  - ステージ縮小 + 強化フェーズが発生することを検証
  - プレイヤー死亡をシミュレート
  - リザルト画面を検証
- **受入条件**: フルフローが例外なく完了する

### T-17.2 統合テスト: ボム → タイル → ダメージ連鎖 [Test]
- PlayMode テスト:
  - プレイヤーがタイルに滑落ボムを発射
  - タイルが崩落する
  - タイル上のプレイヤーがダメージ + 強制移動を受ける
  - タイマー後にタイルが復帰する
- **受入条件**: 全連鎖が正しく解決される

### T-17.3 統合テスト: スライムライフサイクル [Test]
- PlayMode テスト:
  - スライムが正しい間隔でスポーン
  - スライムがプレイヤーに向かって移動
  - ボムがスライムを撃破
  - 正しいドロップが付与される
- **受入条件**: スライムのライフサイクルがエンドツーエンドで動作する

### T-17.4 統合テスト: 強化フェーズフロー [Test]
- PlayMode テスト:
  - 20 秒でフェーズが発動
  - 両プレイヤーに選択肢が表示される
  - P1 が選択、P2 がタイムアウト
  - 強化が正しく適用される
  - ゲームが再開される
- **受入条件**: 完全な強化フェーズが動作する

---

## Phase 18: ポリッシュ・FX・ジュース

> 目標: Feel、DOTween、Epic Toon FX、All In 1 Sprite Shader を使った最終的なビジュアル/オーディオの仕上げ。

### T-18.1 画面シェイクプリセットの追加 [Presentation]
- Feel の MMCameraShaker プリセットを設定:
  - 弱: スライム攻撃（0.1秒、低強度）
  - 中: ボム爆発（0.2秒、中強度）
  - 強: ステージ縮小（0.5秒、高強度）
- **受入条件**: イベントごとに異なるシェイク強度

### T-18.2 ヒットストップ / フリーズフレームの追加 [Presentation]
- ファイル: `Assets/App/Features/Player/Presentation/HitStopService.cs`
- 大ダメージイベント時に Feel の MMFreezeFrame を使用
- ボム命中時に短時間のタイムスケール停止（0.05 秒）
- **受入条件**: ボムダメージ時に認識できるヒットストップ

### T-18.3 UI トランジションアニメーションの追加 [Presentation]
- DOTween によるアニメーション:
  - 強化オーバーレイのスライドイン/スライドアウト
  - カード選択のバウンスエフェクト
  - 3-2-1 カウントダウン時のタイマーパルスエフェクト
  - リザルト画面の登場演出
  - HUD のコイン/HP 変化時のパンチスケール
- **受入条件**: 全 UI トランジションがスムーズで気持ちよい

### T-18.4 スプライトシェーダーエフェクトの追加 [Presentation]
- All In 1 Sprite Shader の設定:
  - ヒットフラッシュ（白、0.1秒）: ダメージ時
  - アウトライン（プレイヤーごとの色）: アクティブプレイヤー
  - ディゾルブエフェクト: スライム死亡時、タイル永久消滅時
- **受入条件**: シェーダーエフェクトが表示され正しく動作する

### T-18.5 環境音/雰囲気の追加 [Presentation]
- BGM（アセットバンドルに含まれる場合）
- ゲームプレイ中の環境音
- フェーズ遷移ジングル
- **受入条件**: オーディオの雰囲気が存在する

### T-18.6 パフォーマンスパス [Infrastructure]
- 900 個の SpriteRenderer のプロファイリング
- ボム VFX とスライム View のプーリング
- ターゲットハードウェアで安定 60fps を確保
- **受入条件**: 通常のゲームプレイ中に 60fps を下回るフレーム落ちがない

---

## Phase 19: レガシーコード削除

> 目標: 完全に置き換えられた旧コードを削除する。

### T-19.1 レガシースクリプトの削除 [Bootstrap]
- `Assets/Script/` 以下のファイルを削除（全レガシーコード）:
  - `GameManager.cs`, `Player.cs`, `Enemy.cs`, `Slime.cs`, `Mage.cs`
  - `GridData.cs`, `GridEntityManager.cs`, `ConvertVector.cs`
  - `GridData/Tiles/`（TileData, FireTile, WaterTile, NormalTile, TileDataDefault）
  - `GridData/TileType.cs`, `GridData/TileDataMapping.cs`, `GridData/TileGenerator.cs`, `GridData/TileMapUI.cs`
  - `VContainer/GameEventInjector.cs`, `VContainer/GameObjectInjector.cs`
- `Assets/Editor/CoordinateBrush.cs` を削除（Tilemap 固有）
- `Assets/Prefab/` 以下の旧プレハブ（Slime.prefab, Mage.prefab）を削除
- **受入条件**: 削除後にコンパイルエラーがなく、シーンから旧スクリプトへの参照がない

### T-19.2 Tilemap 依存の除去 [Bootstrap]
- 新コードに `UnityEngine.Tilemaps` への参照がないことを確認
- シーンに残っている Tilemap コンポーネントがあれば除去
- **受入条件**: プロジェクト全体で Tilemap の使用がゼロ

---

## 依存関係図（概要）

```
Phase 0 (プロジェクト基盤)
  │
Phase 1 (共通プリミティブ)
  │
  ├── Phase 2 (ステージ Domain) ────┐
  │                                  │
  ├── Phase 3 (プレイヤー Domain) ──┼── Phase 4 (ボムリゾルバ)
  │                                  │         │
  │                                  ├── Phase 5 (スライム スポーン/AI)
  │                                  │
  ├── Phase 6 (強化 Domain) ────────┘
  │                                  │
  └──────────────────────────────────┼── Phase 7 (マッチフェーズスケジューラ)
                                     │
                                     ├── Phase 8 (入力アダプタ)
                                     │
                                     ├── Phase 9 (UI Toolkit HUD/オーバーレイ)
                                     │
                            Phase 10〜14 (Presentation 層、並列作業可能)
                                     │
                                     ├── Phase 15 (Bootstrap/DI 配線)
                                     │
                                     ├── Phase 16 (オーディオ)
                                     │
                                     ├── Phase 17 (統合テスト)
                                     │
                                     ├── Phase 18 (ポリッシュ/FX)
                                     │
                                     └── Phase 19 (レガシー削除)
```

**注意事項:**
- Phase 2〜6（Domain）は並列作業可能（Phase 1 完了後）。ただし Bombs は Stage に依存、Slimes は Stage + Player に依存。
- Phase 10〜14（Presentation）は feature 間で独立しているため並列作業可能。
- **Phase 15（Bootstrap）は各 Phase と並行して段階的に進める。** 例: Phase 2 完了時点で Stage 関連の DI 登録を追加し、動作確認する。最終的な統合配線のみ Phase 15 で行う。
- Phase 19（レガシー削除）は Phase 17 で新コードの E2E 動作を確認した後に実施する。
- 各 Phase 内の [Test] タスクは Domain/Application コードと同時またはすぐ後に書く。
- **Stage Presentation（Phase 10）は Tilemap を使用しない。** SpriteRenderer ベースで全タイルを描画する（CLAUDE.md §0 参照）。

---

### 参照ドキュメント
- [`CLAUDE.md`](../CLAUDE.md) — アーキテクチャ仕様、依存ルール、feature 境界
- [`docs/implementation.md`](implementation.md) — ゲーム仕様書（全パラメータ値、アルゴリズム、ゲームフロー）
- [`Packages/manifest.json`](../Packages/manifest.json) — パッケージ依存関係（VContainer, R3, UniTask, Input System）
