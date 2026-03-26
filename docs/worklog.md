# FLOOR BREAKER — 作業ログ

## 2026-03-26: Phase 14 — カメラシステム

### 完了タスク
- **T-14.0** App.Cameras.Presentation.asmdef 新設 (参照: App.Shared.* + App.Player + App.Stage + R3)
- **T-14.1** SplitScreenCameraSetup MonoBehaviour (2カメラ生成、ビューポート 0-50% / 50-100%、orthographicSize=5、Initialize/Tick/IDisposable)
- **T-14.2** CameraFollower pure C# (R3 購読 PlayerModel.Position、Lerp 追従 SmoothSpeed=8、ステージ境界クランプ、orthographic 視錐台考慮)
- **T-14.3** ICameraShakeService インターフェース + NullCameraShakeService No-op 実装 (ShakeIntensity enum: Light/Medium/Heavy、Feel 統合は Phase 18)
- **T-14.4** CameraPreviewController + CameraPreview.unity デバッグシーン (WASD/矢印で P1/P2 移動、Space でステージ縮小、手動壁生成)

### 設計判断
- **CameraFollower は pure C#**: MonoBehaviour ではなく、SplitScreenCameraSetup が Tick で座標を受け取り Camera.transform に適用。テスト可能性を維持
- **ICameraShakeService + Null 実装**: Feel の MMCameraShaker 統合は Phase 18 に委ねる。インターフェースを先に定義し、Bootstrap で差し替え可能にする
- **orthographicSize = 5**: 仕様「視界範囲 縦横10マス」→ 縦10タイル ÷ 2 = 5。ビューポート半幅による水平方向のアスペクト比自動調整

---

## 2026-03-26: Phase 13 — スライム Presentation

### 完了タスク
- **T-13.0** SlimeEvents.cs 新規 (3 readonly struct: SlimeSpawnedEvent/SlimeMovedEvent/SlimeKilledEvent)
- **T-13.0** SlimeRegistry に R3 イベント通知追加 (IDisposable + 3 Subject/Observable、Add/Remove/UpdatePosition から自動発火)
- **T-13.0t** SlimeRegistryTests にイベントテスト 4件追加 (Spawned/Killed/Moved 発火 + 非存在 Remove 不発火)
- **T-13.1** App.Slimes.Presentation.asmdef 新設 (参照: App.Shared.* + App.Slimes + App.Stage/Stage.Presentation + App.Player/Player.Presentation + App.Bombs + App.MatchFlow + App.ScriptableObjects + R3 + DOTween)
- **T-13.2** SlimeSpriteConfig ScriptableObject (8方向スプライト、3種別色 Normal/Gold/Red、スケール・ソートオーダー・アニメーションパラメータ一元管理)
- **T-13.3** SlimeView MonoBehaviour (薄い View、SpriteRenderer + Initialize/SetDirection/SetPositionImmediate、R3 購読なし)
- **T-13.4** SlimeViewFactory MonoBehaviour (SlimeView の GameObject 生成・破棄、SerializeField SlimeSpriteConfig)
- **T-13.5** SlimeAnimationService (DOTween: PlaySpawn OutBack ポップイン / PlayMove OutQuad / PlayDeath InBack 縮小+フェード、SlimeId 別 tween 追跡)
- **T-13.6** SlimePresenter pure C# (R3 購読 Spawned/Moved/Killed → View/AnimService ディスパッチ、方向導出 dx/dy→Direction8、Tick() 不要)
- **T-13.7** SlimePreviewController + SlimePreview.unity デバッグシーン (T/Y/U でスライムスポーン、N で撃破、M で自動スポーン ON/OFF、WASD/矢印で P1/P2 移動、ランダムボム 1.5秒間隔)

### 設計判断
- **SlimeRegistry にイベント追加 (Option C)**: 全ミューテーションが Registry 経由のため、4箇所のキルサイト (BombEffectSpreadService, SlimeTickService, FireDamageTickService, MatchPhaseScheduler) の変更不要。StageModel.TileChanged と同パターン
- **SlimePresenter に Tick() 不要**: PlayerPresenter と異なり、スライムには無敵ブリンク・歩行フレームトグルがない。全更新がイベント駆動で完結
- **方向導出**: SlimeAiService.PickMoveTarget は X or Y 軸のみ移動するため、SlimeMovedEvent の OldPosition→NewPosition 差分から 4方向を導出。念のため 8方向に対応
- **KilledEvent は Remove 前に発火**: SlimeRegistry.Remove() で辞書削除前に SlimeModel の Type/Position を取得しイベント発火。Presenter が View を特定できるようにする

---

## 2026-03-26: Phase 12 — ボム Presentation

### 完了タスク
- **T-12.0** App.Bombs.Presentation.asmdef 新設 (参照: App.Bombs + App.Stage.Presentation + App.Player.Presentation + App.MatchFlow + App.ScriptableObjects + R3 + DOTween)
- **T-12.1** BombSpriteConfig ScriptableObject (滑落/炎ボム各スプライト・色・トレイル・爆発VFX・インパクトフラッシュパラメータ)
- **T-12.2** BombFlightView MonoBehaviour (薄い View、SpriteRenderer + TrailRenderer 保持、Initialize/Reinitialize/Show/Hide)
- **T-12.3** BombAnimationService (DOTween: PlayFlight/KillFlight/PlayImpactFlash、PlayerId 別 tween 追跡)
- **T-12.4** BombExplosionVfxPool (Epic Toon FX 爆発パーティクル 2 種プール、Tick で自動返却)
- **T-12.5** BombPresenter (pure C#、R3 購読 FlightStarted/BombLanded → View/AnimService/VfxPool ディスパッチ、Tick で VFX 管理)
- **T-12.6** BombViewFactory (MonoBehaviour、BombFlightView の生成 + プーリング)
- **T-12.7** BombPreviewController + BombPreview.unity デバッグシーン (F/G/I/O ホールドで炎/滑落ボム、+/- で最大飛行距離、1-5 で効果範囲、BalanceConfig SO 参照)
- **T-12.8** BombSpriteConfig.asset 作成
- **前提修正** BombFlightTracker: R3 Subject (FlightStarted/BombLanded) + IDisposable 追加
- **前提修正** MatchFlowOrchestrator: BombFlightTracker フィールド化 + Dispose
- **機能追加** BombSpec: MinFlightDistance フィールド追加 (ボタン即離しでも最低距離まで飛行)
- **機能追加** BombFlightTracker: IsReleased フラグ + MinFlightDistance ロジック (ReleaseBomb で min 未達なら飛行継続、TickPlayer ループ内で min 到達チェック)
- **機能追加** IBalanceParameters + BalanceConfig: BombMinFlightDistance (デフォルト 3)
- コンパイルエラー 0 件、EditMode テスト 240 件全件グリーン

### 設計判断
- **BombFlightTracker に Observable 追加**: Presentation が飛行開始/着弾を購読するために R3 Subject を追加。BombFlightStartedEvent / BombLandedEvent を Bombs/Application namespace 内に定義
- **Presenter パターン踏襲**: Phase 10/11 と同じ構造 (thin View + pure C# Presenter + AnimationService + VfxPool + Factory)
- **直線飛行**: 放物線ではなく DOTween DOMove Linear で直線飛行。BombLanded イベントで tween 即キル + スナップ
- **MinFlightDistance**: BombSpec に追加し Domain レベルで最小飛行距離を保証。BombFlightState.IsReleased フラグで「リリース済みだが min 未達」状態を管理。TickPlayer のタイル進行ループ内でも min チェック (一気に複数タイル進む場合に正確な着弾位置を保証)
- **デバッグコントローラーでの BalanceConfig 参照**: PreviewBalanceParameters 重複クラスを廃止し、DefaultBalance.asset を SerializeField で参照 (Phase 11 で確立したパターン)
- **VFX フォールバック廃止**: SerializeField が未設定の場合はエラーログを出し初期化をスキップ。暗黙のフォールバックは行わない

---

## 2026-03-26: Phase 11 — プレイヤー Presentation

### 完了タスク
- **T-11.0** App.Player.Presentation.asmdef 新設 (noEngineReferences: false, 参照: App.Player + App.Stage + App.Stage.Presentation + App.Bombs + App.Slimes + App.MatchFlow + App.ScriptableObjects + R3 + DOTween)
- **T-11.1** PlayerSpriteConfig ScriptableObject (8方向 stand/walk スプライト、P1/P2 色、移動/被弾/無敵/死亡アニメーションパラメータ、スケール)
- **T-11.2** PlayerView MonoBehaviour (薄い View、Initialize/SetDirection/SetWalkFrame/SetPositionImmediate、R3 購読なし)
- **T-11.3** PlayerAnimationService (DOTween 一元管理: PlayMove/PlayForcedMove/PlayHitFlash/Start・StopInvulnerabilityBlink/PlayDeath、PlayerId 別 tween 追跡)
- **T-11.4** PlayerPresenter (pure C#、R3 購読 Position/FacingDirection/CurrentHp.Pairwise → View/AnimService ディスパッチ、Tick で歩行フレームトグル + 無敵エッジ検出)
- **T-11.5** PlayerViewFactory (MonoBehaviour、PlayerView の GameObject 生成 + スケール適用)
- **T-11.6** PlayerPreviewController + PlayerPreview.unity デバッグシーン (WASD/矢印キーで P1/P2 移動、ダメージ/強制移動/即死/リセット、1.5秒ごとランダムボム範囲10、FireDamageTickService 配線)
- **T-11.7** PlayerSpriteConfig.asset (全16スプライト割り当て済み)
- **バグ修正** PlayerDamageService: 強制移動時に CurrentPosition を即座に更新
- **バグ修正** FallBombResolver: Collapsing/Collapsed タイルを affectedTiles に含める (タイマーリセット)
- **バグ修正** StageQueryService.GetTilesInCross: penetrateWalls=false 時に通行不可タイル全般で停止
- **バグ修正** BombEffectSpreadService: 炎ボム段階広がり中の崩落タイル遮断 + エンティティ遮断 (CanFireReach)
- **バグ修正** FireDamageTickService: SlimeRegistry null 安全チェック追加
- **改善** TileFireVfxPool: VFX スケール 0.12 → 0.24 (2倍)
- コンパイルエラー 0 件、EditMode テスト 234 件全件グリーン

### 設計判断
- **Presenter パターン踏襲**: Phase 10 の StagePresenter と同じ構造 (thin View + pure C# Presenter + AnimationService)。PlayerView は 2 個しかないが一貫性のため Presenter 経由
- **R3 Pairwise でダメージ検出**: HP の前回値→現在値を比較してダメージ/死亡を検出。Domain に明示イベントを追加不要
- **スケール問題**: プレイヤースプライト (363x473px, PPU=100) がタイル (32x32px, PPU=32) の約4倍 → PlayerSpriteConfig._playerScale=0.22 で調整
- **FallBombResolver の仕様変更**: Collapsing/Collapsed タイルを affectedTiles に含めるように変更。滑落ボムの再適用でタイマーがリセットされ、効果が正しく延長される
- **GetTilesInCross の遮断強化**: penetrateWalls=false 時に壁だけでなく通行不可タイル全般 (IsPassable) で停止。炎ボムの十字パターンが崩落タイルで止まる
- **CanFireReach (段階的広がり用)**: Resolve 時点と適用時点でタイル状態が変わるケースに対応。GetTilesInCross と同じ IsPassable 判定基準 + エンティティ位置チェック
- **BalanceConfig SO 参照**: デバッグコントローラーでは IBalanceParameters の再実装を避け、既存の DefaultBalance.asset を SerializeField で参照

---

## 2026-03-26: Phase 10 追補 — ボム効果の段階的十字広がり

### 完了タスク
- **BombEffectSpreadService** 新規作成 (Bombs/Application) — 距離ごとにキューイングし Tick(dt) で段階的に SetTileState
- **BombLaunchUseCase** リファクタ — ExecuteFall/FireBomb を SpreadService に委譲、不要フィールド削除でスリム化
- **IBalanceParameters** + **BalanceConfig** — FireBombSpreadInterval (0.15s) / FallBombSpreadInterval (0.3s) 追加
- **MatchPhaseScheduler** — Tick 配布先に BombEffectSpreadService 追加
- **MatchFlowOrchestrator** — SpreadService の生成・注入
- **StagePreviewController** — BombEffectSpreadService 経由のボムシミュレーションに変更
- **BombEffectSpreadServiceTests** 新規 7 件 — 距離0即時、距離1/2遅延、速度差テスト
- **BombLaunchUseCaseTests** 修正 — SpreadService の Tick を挟んだアサートに更新
- 全テストファイル (12件) の TestBalanceParameters に新プロパティ追加
- **docs/implementation.md** — ボム共通仕様に段階的広がり記述追加
- コンパイルエラー 0 件、EditMode テスト 232 件全件グリーン

### 設計判断
- **Domain/Application 層で段階的適用**: 演出ではなくゲームルール。BombEffectSpreadService が Tick で距離ごとに状態変更
- **ボム種別ごとの速度**: 炎ボム 0.15s/マス (素早い延焼)、滑落ボム 0.3s/マス (重い崩落)
- **距離 0 は即座に適用**: 着弾地点は Enqueue 時点で即適用
- **PendingTiles で退避先を保護**: SpreadService が未適用タイルを HashSet で公開。ダメージ時の退避先探索 (SafeTileSearchService) に occupied として渡し、「広がる予定のタイル」へのリスポーンを防止
- **SpreadEntry を immutable readonly struct に**: WithApplied() パターンで安全に更新。壁判定は HashSet で O(1)
- **BombLaunchUseCase のスリム化**: ダメージ・タイマー・スライム処理を SpreadService に移譲。UseCase は Resolver → SpreadService への橋渡しのみ

---

## 2026-03-26: Phase 10 — ステージ Presentation

### 完了タスク
- **T-10.0** App.Stage.Presentation.asmdef 新設 (noEngineReferences: false, 参照: App.Stage + App.Shared.* + R3 + DOTween)
- **T-10.1** TileSpriteConfig ScriptableObject (全6状態のスプライト・色・VFXプレハブ・アニメーションパラメータ一元管理)
- **T-10.2** TileView MonoBehaviour (薄い View、ApplyState で即座にスプライト/色切替、自身では R3 購読しない)
- **T-10.3** StageViewFactory (30x30 = 900 個の TileView 生成、Dictionary<GridPos, TileView> 返却)
- **T-10.4** TileAnimationService (DOTween: 崩落 Ease.InBack / 復帰 Ease.OutBack / 炎パルス Yoyo / 永久消滅)
- **T-10.5** TileFireVfxPool (Epic Toon FX 炎パーティクル用オブジェクトプール、初期20個)
- **T-10.6** StagePresenter (単一 R3 購読 → Dictionary ディスパッチ、全6状態遷移ハンドリング)
- **T-10.7** StageShrinkAnimator (バッチ検出 + 時計回りソート + stagger delay ウェーブ崩落演出)
- **T-10.8** StagePreviewController + Debug シーン (キー操作で全タイル状態・縮小・炎・崩落を目視確認)
- コンパイルエラー 0 件、EditMode テスト 223 件全件グリーン

### 設計判断
- **asmdef 分割**: 既存 `App.Stage.asmdef` (noEngineReferences: true) を維持し、Presentation 用に `App.Stage.Presentation.asmdef` を新設。Domain の純粋性を保護
- **900 個の個別購読を回避**: StagePresenter が StageModel.TileChanged を 1 回だけ購読し、Dictionary<GridPos, TileView> でディスパッチ。パフォーマンス重視
- **StageShrinkAnimator のバッチ検出**: 同一フレームで 8 タイル以上の PermanentlyDestroyed をウェーブと判定。StagePresenter は IsShrinkAnimating フラグで即時処理をスキップ
- **MatchPhaseScheduler 変更不要**: ドメインが即座に状態変更 → Presentation が TileChanged 購読でアニメーション。sync timer (1.0s) の範囲内で演出完了
- **TileView は購読しない**: MonoBehaviour は薄く保ち、StagePresenter / TileAnimationService が状態遷移と演出を統括
- **スプライト6種**: 既存の Tile_Floor_Normal/Wall/Burning/Collapsing/Collapsed/Destroyed.png を TileSpriteConfig SO で参照

---

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
