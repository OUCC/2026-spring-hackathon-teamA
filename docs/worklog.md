# FLOOR BREAKER — 作業ログ

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
