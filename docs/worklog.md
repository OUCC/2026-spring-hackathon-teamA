# FLOOR BREAKER — 作業ログ

## 2026-03-26: Phase 1 — 共通プリミティブ (PR #TBD)

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
