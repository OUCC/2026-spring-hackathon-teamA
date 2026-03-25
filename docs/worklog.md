# FLOOR BREAKER — 作業ログ

## 2026-03-26: Phase 0 — プロジェクト基盤 (PR #TBD)

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
