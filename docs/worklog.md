# FLOOR BREAKER — 作業ログ

## 2026-03-26: Phase 9 — UI Toolkit HUD / オーバーレイ

### 完了タスク
- **T-9.1** USS 変数 + ルート UXML/USS (Variables.uss, MatchRoot.uxml/uss)
- **T-9.2** HUD: PlayerHud.uxml/uss, PlayerHudView.cs, PlayerHudPresenter.cs (タイマー統合)
- **T-9.3** 強化オーバーレイ: UpgradeCard.uxml/uss, UpgradeOverlay.uss, UpgradeCardElement.cs, UpgradeIdDisplayHelper.cs, UpgradeOverlayView.cs, UpgradeOverlayPresenter.cs
- **T-9.3** UpgradeSelectionState.cs (Upgrades/Domain), UpgradeUIInputBridge.cs 修正 (X軸ナビ + SelectionState)
- **T-9.4** リザルト: Result.uss, ResultView.cs, ResultPresenter.cs (左右独立ペイン)
- **T-9.5** MatchUIDocument.cs, MatchPanelSettings.asset
- **T-9.6** UIPreview シーン + UIPreviewController.cs (Scenes/Debug/, RuntimeUI/Debug/)
- コンパイルエラー 0 件、EditMode テスト 223 件全件グリーン

### 設計判断
- **プレイヤー独立原則**: 全フェーズ (HUD/強化/リザルト) で各プレイヤーが自己完結した UI ペインを持つ。共有の中央タイマーやグローバルカウントダウンは廃止
- **スケーラブル設計**: PlayerHud テンプレートを繰り返すだけで 1〜N 人に対応可能
- **USS 変数定義**: `:root` ではなく `.match-root` に定義（UXML テンプレートインスタンスへの変数継承問題を回避）
- **PhaseTimerPresenter 廃止**: タイマーは PlayerHudPresenter に統合（各プレイヤー HUD 内に表示）
- **リロール**: Label ではなく Button に変更
- **開発用ファイル分離**: UIPreviewController → RuntimeUI/Debug/, UIPreview.unity → Scenes/Debug/
- CLAUDE.md §11.2 をプレイヤー独立構造に更新、§11.4 にデザイン方針を追加

---

## 2026-03-26: Phase 8.5 — Phase 9 前提修正

### 完了タスク
- **T-8.5.1** PlayerBuild に AcquiredUpgrades 追跡を追加 (IDisposable + ReactiveProperty + RecordUpgrade)
- **T-8.5.2** UpgradeApplyService に MoveSpeed/HpRecovery の RecordUpgrade 呼び出しを追加
- **T-8.5.3** PlayerModel.Dispose に Build.Dispose() を追加
- **T-8.5.4** MatchEndUseCase に Winner Observable を追加 (IDisposable + ReactiveProperty<PlayerId?>)
- **T-8.5.5** MatchPhaseScheduler.TransitionToResult に PlayerId パラメータ追加 + SetWinner 接続
- **T-8.5.6** UpgradePhaseUseCase.RemainingTime を ReadOnlyReactiveProperty<float> に Observable 化
- **T-8.5.7** App.UI.asmdef を新規作成 (App.Bombs 含む参照リスト)
- **T-8.5.8** EditMode テスト 12 件追加 (PlayerBuild 4件, MatchEndUseCase 2件, UpgradePhaseUseCase 4件, UpgradeApplyService 2件)

### 設計判断
- 取得済み強化の追跡は PlayerBuild に配置 (PlayerModel ではなく)。MoveSpeed/HpRecovery は UpgradeApplyService から RecordUpgrade を明示呼び出し
- MatchEndUseCase.Winner は ReactiveProperty で公開。Scheduler が TransitionToResult 時に SetWinner を呼ぶ
- UpgradePhaseUseCase.RemainingTime は MatchClock 流用ではなく専用 ReactiveProperty。理由: 強化フェーズ中は MatchClock が Pause 状態のため
- App.UI.asmdef に App.Bombs を含める (HUD の BombCooldownState 購読に必要)

---

## 2026-03-26: Phase 7+8 — MatchFlow + BombFlightTracker (PR #28)

### 完了タスク
- **T-7.1** MatchPhaseScheduler — 状態マシン (Running/StageShrink/UpgradePhase/Result)、全 Tick 配布
- **T-7.2** UpgradePhaseUseCase — P1/P2 DraftService、10秒タイムアウト、自動スキップ
- **T-7.3** MatchEndUseCase — HP0 検出→勝者判定
- **T-7.4** MatchFlowOrchestrator — 壁生成→プレイヤー→初期スライム→スケジューラ起動
- **T-7.5** SlimeTickService — AI Tick + 5秒スポーン + 崩落スライム自動死亡 (TimerCompleted購読)
- **T-7.6** FireDamageTickService — OnFire 滞在1秒ごと1ダメージ
- **T-8.1** BombFlightTracker — リアルタイム飛行追跡、壁/エンティティ/最大距離で自動着弾
- **T-8.2** BombHoldCommand — 入力コマンド値型
- IBalanceParameters に BombFlightSpeed/StageShrinkAnimDuration/InvulnerabilityDuration 追加
- EditMode テスト 24 件追加 (累計 211 件全件グリーン)

### 設計判断
- MatchPhaseScheduler は UniTask async ループではなく同期状態マシン + Tick 方式を採用（EditMode テスト容易性）
- BombFlightTracker は Bombs/Application に配置（ボムの動作はボム feature の責務）
- 炎 DoT のスライム撃破者は追跡困難→ドロップなし
- PlayerId に公開コンストラクタがないため、BombFlightTracker は P1/P2 フィールド分離で対応
- Input Infrastructure (MonoBehaviour) は Unity API 依存のため別 PR に分離

---

## 2026-03-26: Phase 5+6 — スライム + 強化 (PR #27)

### 完了タスク
- Upgrades: UpgradeDefinition, UpgradeCatalog (15強化), AvailabilityRule, RollRule, DraftService, ApplyService
- Slimes: SlimeType/Id/Model, SlimeRegistry, SpawnService, AiService, DropResolver
- BombLaunchUseCase にスライム死亡+ドロップ統合
- EditMode テスト 8 ファイル追加

---

## 2026-03-26: Phase 4 — ボムリゾルバ (PR #26)

### 完了タスク
- **T-4.0** BombType enum を Shared/Domain/Primitives に追加
- **T-4.1** BombSpec — readonly struct、全ボムパラメータを保持
- **T-4.2** BombFlightCommand — 発射コマンド値型 (Origin, Direction, Spec, Owner)
- **T-4.3** BombCooldownState — R3 ReactiveProperty で FallRemaining/FireRemaining を read-only 公開
- **T-4.4** BombAreaResolver — StageQueryService.GetTilesInCross に委譲する薄いラッパー
- **T-4.5** BombLandingResolver — 壁・エンティティ・Collapsed/PermanentlyDestroyed の衝突解決
- **T-4.6** FallBombResolver — FallBombResult 構造体を返す副作用なしリゾルバ
- **T-4.7** FireBombResolver — FireBombResult 構造体を返す副作用なしリゾルバ
- **T-4.8** BombLaunchUseCase — BombSpec 組み立て + 壁破壊→タイル変更→タイマー→ダメージ適用
- **T-4.9** EditMode テスト 6 ファイル (BombCooldownState, BombAreaResolver, BombLandingResolver, FallBombResolver, FireBombResolver, BombLaunchUseCase)
- App.Bombs.asmdef (noEngineReferences: true)、App.Tests.EditMode.asmdef に参照追加

### 設計判断
- Resolver は結果構造体を返し副作用なし。Application 層 (BombLaunchUseCase) が状態変更を統括
- BombSpec は純粋な値型 (PlayerBuild 非依存)。PlayerBuild → BombSpec の組み立ては BombLaunchUseCase に配置
- RecoveryTime は IBalanceParameters.FallBombRecoveryDuration から取得 (PlayerBuild にはない固定値)
- エンティティ衝突は `Func<GridPos, bool> isEntityAt` で注入。Phase 5 で SlimeRegistry を追加可能
- 壁衝突時はボムが壁の位置で着弾 (壁タイルが効果範囲の中心になり破壊される)
- 炎 DoT / 飛行時ダメージ / 飛行状態追跡は後続 Phase に委ねる

---

## 2026-03-26: ドキュメント整備

- docs/architecture.md を作成 (mermaid 図付き: 依存グラフ、レイヤー構造、クラス図、マッチフロー、R3 データフロー)
- アーキテクチャ原則を明文化: 時間管理の単一化、Domain read-only 公開、UI Toolkit ルート戦略
- Phase 4 タスクレビューを実施し tasks.md に反映 (BombType 追加、BombSpec 組み立て方針、スライム衝突の注入パターン等)

---

## 2026-03-26: Phase 3 — プレイヤー Domain (PR #24)

### 完了タスク
- **T-3.1** PlayerStats — ReactiveProperty HP/Coins、TakeDamage/Heal/AddCoins/SpendCoins
- **T-3.2** PlayerBuild — 全 15 強化の追跡、ApplyUpgrade、CD 下限対応
- **T-3.3** PlayerModel — PlayerId/Stats/Build/Position/FacingDirection を集約
- **T-3.4** InvulnerabilityState — Activate/Tick で時限無敵
- **T-3.5** ForcedMoveState — Start/Tick/Complete で強制移動管理
- **T-3.6** PlayerMoveService — 8 方向移動、通行可能チェック、強制移動中ブロック
- **T-3.7** PlayerDamageService — 無敵チェック、ダメージ適用、崩落時の強制移動
- **T-3.8** EditMode テスト 6 ファイル
- UpgradeId enum を Shared/Domain/Primitives に追加

### 設計判断
- PlayerBuild は BombSpec に依存しない — 生の強化値のみ保持、BombSpec 組み立ては Phase 4
- App.Player asmdef は Feature ルートに配置 (Domain + Application をカバー)
- UpgradeId enum を Shared に配置し Phase 6 の UpgradeDefinition と共有

---

## 2026-03-26: Phase 2 — ステージ Domain (PR #22)

### 完了タスク
- **T-2.1** TileState 列挙型 (Normal/OnFire/Collapsing/Collapsed/PermanentlyDestroyed/Wall)
- **T-2.2** StageModel — 30x30 TileState 2D配列 + R3 Subject で変更通知
- **T-2.3** StageBounds — TileCoordRange ラッパー、Shrink/GetOuterRing
- **T-2.4** WallGenerationService — シード 8% + 成長 40% → 目標 20%、5x5 スポーン保護
- **T-2.5** StageShrinkService — 外周を PermanentlyDestroyed に設定し境界縮小
- **T-2.6** SafeTileSearchService — BFS で最近の安全マスを探索
- **T-2.7** StageQueryService — GetPassableTiles, GetTilesInCross, RaycastGrid
- **T-2.8** TileTimerService — 崩落→復帰の自動チェーン、炎タイマー、R3 完了通知
- **T-2.9** EditMode テスト 6 ファイル

### 設計判断
- App.Stage asmdef は noEngineReferences: true、pure C# Domain
- WallGenerationService は壁位置を返すだけ (副作用なし)、呼び出し側が StageModel に適用
- TileTimerService は崩落完了後に自動で復帰タイマーを開始
- SafeTileSearchService は BFS のみ (3x3 優先探索は省略しシンプルに)

---

## 2026-03-26: Phase 1 — 共通プリミティブ (PR #21)

### 完了タスク
- **T-1.1** GridPos 値型 — 算術、ManhattanDistance/ChebyshevDistance、Neighbors4/8、ToWorldCenter/FromWorld
- **T-1.2** Direction8 / CardinalDirection4 — ToOffset, Opposite, IsCardinal, ToDirection8
- **T-1.3** PlayerId — Player1/Player2/Opponent
- **T-1.4** GamePhase 列挙型
- **T-1.5** MatchClock — R3 ReactiveProperty で Remaining/CurrentPhase/IsPaused を公開
- **T-1.6** TileCoordRange — Contains, Shrink, GetAllPositions, GetOuterRing
- **T-1.7** ITimeProvider / IRandomProvider / IAudioService インターフェース
- **T-1.8** UnityTimeProvider / SeededRandomProvider 実装
- Float2 (Domain 用軽量ベクトル) + Float2Extensions (Vector2 変換)
- EditMode テスト 4 ファイル (GridPos, Direction8, TileCoordRange, SeededRandomProvider)

### 設計判断
- Domain に Float2 を自作し UnityEngine.Vector2 を排除、noEngineReferences: true を維持
- R3.dll を precompiledReferences で Domain asmdef から参照し MatchClock を Domain/Timing に配置
- GridPos.ToWorldCenter() は Float2 を返し、Presentation 層で Vector2 に変換

---

## 2026-03-26: Phase 0 — プロジェクト基盤 (PR #19)

### 完了タスク
- **T-0.1** ディレクトリ構造の作成 — CLAUDE.md §3 に従い Assets/App/ 配下のフルツリーを作成
- **T-0.2** asmdef ファイルの作成 — App.Shared.Domain/Application/Infrastructure/Presentation, App.Bootstrap, App.ScriptableObjects, App.Tests.EditMode の 7 個
- **T-0.3** 初期シーンの作成 — Title.unity, Match.unity, Result.unity を作成し EditorBuildSettings に登録
- **T-0.4** Input System アクションアセットの設定 — FloorBreakerActions.inputactions (Gameplay, UpgradeUI_P1, UpgradeUI_P2, System)
- **T-0.5** バランス設定 ScriptableObject の作成 — IBalanceParameters インターフェース + BalanceConfig SO + DefaultBalance.asset

### 設計判断
- Domain asmdef は `noEngineReferences: true` で Unity API を排除
- IBalanceParameters (pure C# interface) と BalanceConfig (ScriptableObject) の 2 層構造を採用
- Feature 別 asmdef は Phase 2 以降で各 Feature にコードが入る時に作成（空 asmdef 回避）
