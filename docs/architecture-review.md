# FLOOR BREAKER — アーキテクチャ違反レビュー

実装完了時点で CLAUDE.md の設計思想に対する違反・改善点を洗い出したドキュメント。
LifetimeScope 階層化、デバッグシーンの DI 化についても調査結果を含む。

---

## 目次

1. [重大な違反 (CRITICAL)](#1-重大な違反-critical)
2. [高優先度の違反 (HIGH)](#2-高優先度の違反-high)
3. [中優先度の違反 (MEDIUM)](#3-中優先度の違反-medium)
4. [低優先度の違反 (LOW)](#4-低優先度の違反-low)
5. [良好な実装 (COMPLIANT)](#5-良好な実装-compliant)
6. [LifetimeScope 階層化の調査](#6-lifetimescope-階層化の調査)
7. [デバッグシーンの DI 化調査](#7-デバッグシーンの-di-化調査)
8. [優先順位付きアクションプラン](#8-優先順位付きアクションプラン)

---

## 1. 重大な違反 (CRITICAL)

### 1.1 MatchModeSelection — static mutable state

**ファイル:** `Features/MatchFlow/Application/MatchModeSelection.cs`

```csharp
public static class MatchModeSelection
{
    public static bool IsCpuPlayer { get; set; }
}
```

**違反箇所:** CLAUDE.md §4 依存方向 + §20 禁止事項
- "static mutable state" は明示的に禁止されている
- TitlePresenter が書き込み → MatchLifetimeScope が読み取るという暗黙のデータフロー
- テスト不可能で差し替え不可能

**修正案:**
- `MatchModeSelection` を非 static クラスにし、ProjectLifetimeScope で Singleton 登録
- または ScriptableObject ベースの共有状態にする（シーン遷移を安全にまたげる）

---

### 1.2 TitlePresenter — Presentation 層からのシーン遷移

**ファイル:** `Features/UI/Title/Presentation/TitlePresenter.cs:36,44`

```csharp
SceneManager.LoadScene("Match");
```

**違反箇所:** CLAUDE.md §4 依存方向
- Presentation → Infrastructure (SceneManager) への直接依存
- 「Presentation は Application の結果を描画する」に反する
- シーン遷移は Infrastructure の責務

**修正案:**
- `ISceneTransitionService` インターフェースを Application 層に定義
- 実装を Infrastructure 層に置く (`UnitySceneTransitionService`)
- TitlePresenter はコールバック/コマンド経由で Application 層に遷移を依頼

---

### 1.3 ResultPresenter — 同上のシーン遷移直接呼び出し

**ファイル:** `Features/UI/Result/Presentation/ResultPresenter.cs:37-40`

```csharp
view.RematchButton.clicked += () => SceneManager.LoadScene("Match");
view.TitleButton.clicked += () => SceneManager.LoadScene("Title");
```

**違反箇所:** 1.2 と同一。Presentation 層から SceneManager を直接呼んでいる。

**修正案:** 1.2 と同じ `ISceneTransitionService` で統一。

---

### 1.4 TitleUIDocument.Start() — DI 不使用の手動サービスロケータ

**ファイル:** `Features/UI/RuntimeUI/Documents/TitleUIDocument.cs:89-121`

```csharp
private void Start()
{
    IAudioService audio = null;
    foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
    {
        if (mb is IAudioService svc) { audio = svc; break; }
    }
    // ...
    new TitlePresenter(this, audio, rebindService);
}
```

**違反箇所:** CLAUDE.md §8 VContainer ルール
- `FindObjectsByType` による手動サービスロケータパターン
- 「DI の composition root は LifetimeScope に限定する」に反する
- TitlePresenter のインスタンスが GC にのみ管理される（dispose なし）
- VContainer を経由しないため依存が暗黙的

**修正案:**
- TitleLifetimeScope を新設し、TitlePresenter を DI 管理下に置く
- IAudioService は ProjectLifetimeScope から継承される

---

### 1.5 TitleTestSpaceController — 539行の神 MonoBehaviour

**ファイル:** `Features/UI/Title/Presentation/TitleTestSpaceController.cs`

**違反箇所:** CLAUDE.md §1, §2, §8, §20
- 「MonoBehaviour は View / Adapter に限定する」に完全に反する
- 内部に Domain (StageModel, PlayerModel, BombLaunchUseCase) を手動 `new`
- Application 層 (BombFlightTracker, BombEffectSpreadService) を手動 `new`
- Presentation 層 (Presenter, AnimService) も手動 `new`
- 入力コールバックから直接 Domain 呼び出し
- Time.deltaTime を直接使用（ITimeProvider 不使用）

**修正案:**
- TitleTestSpaceLifetimeScope を新設するか、TitleLifetimeScope の子スコープで管理
- TestArea 内部の依存は VContainer のコンストラクタインジェクションで解決
- 詳細は §7 デバッグシーン DI 化を参照

---

## 2. 高優先度の違反 (HIGH)

### 2.1 MatchInitializer — Presenter を手動 `new` で生成

**ファイル:** `Bootstrap/MatchInitializer.cs:165-237`

```csharp
var stagePresenter = new StagePresenter(...);
_presenters.Stage = stagePresenter;
// ...
_presenters.PlayerP1 = new PlayerPresenter(...);
```

**違反箇所:** CLAUDE.md §8 VContainer ルール
- 約10個の Presenter を `new` で手動生成し、MatchPresenters ホルダーのプロパティに代入
- VContainer のコンストラクタインジェクションを活用していない
- MatchInitializer 自体が 30+ の依存を受け取る巨大コンストラクタ

**現状の理由:**
- Presenter は VFX プール・View インスタンスなどランタイム生成物に依存するため、DI 登録時点では存在しない
- `IAsyncStartable` のタイミングで初めて生成可能

**修正案:**
- VContainer のファクトリパターン (`Func<T>` or `IFactory<T>`) を活用
- VFX プール等は `IAsyncStartable` で先に生成し、その後 Presenter を VContainer のファクトリ経由で生成
- MatchPresenters ホルダーの mutable setter パターンを廃止し、一括生成時に immutable に構築

---

### 2.2 MatchInitializer — FindObjectsByType の使用

**ファイル:** `Bootstrap/MatchInitializer.cs:240`

```csharp
var inputAdapters = Object.FindObjectsByType<PlayerInputAdapter>(FindObjectsSortMode.None);
```

**違反箇所:** CLAUDE.md §8
- VContainer ではなく Unity のシーン検索でコンポーネントを探している
- `RegisterComponentInHierarchy` や `RegisterComponentInNewPrefab` で DI 管理すべき

**修正案:**
- PlayerInputAdapter を MatchLifetimeScope で `RegisterComponentInHierarchy` に登録
- MatchInitializer にはコンストラクタインジェクションで受け取る

---

### 2.3 MatchPresenters — mutable setter ホルダー

**ファイル:** `Bootstrap/MatchPresenters.cs`

```csharp
public StagePresenter Stage { get; set; }
public PlayerPresenter PlayerP1 { get; set; }
// ... 全て public setter
```

**違反箇所:** CLAUDE.md §5 read-only 公開の精神
- Domain の read-only 原則が Presenter ホルダーにも適用されるべき
- 外部から任意のタイミングで上書き可能な設計
- null チェックが各所に散在 (`?.` 演算子)

**修正案:**
- Presenter の一括生成後に immutable なオブジェクトとして構築
- MatchPresenters は Builder パターンまたはファクトリメソッドで一度だけ構築

---

### 2.4 ダッシュクールダウンのハードコード

**ファイル:** `Features/Input/Application/GameplayInputBridge.cs:243`

```csharp
_dashCooldowns[playerId.Index] = 1f; // TODO: BalanceParameters から取得
```

**違反箇所:** CLAUDE.md Phase 0 (T-0.5) — 全パラメータを BalanceConfig に一元管理
- マジックナンバーが残っている

**修正案:**
- `IBalanceParameters` に `DashCooldown` を追加
- `BalanceConfig` にフィールド追加

---

### 2.5 SafeTileSearchService — 3x3 優先探索の省略

**ファイル:** `Features/Stage/Domain/SafeTileSearchService.cs`

**状態:** **仕様変更により対象外** — BFS のみの実装を正式方針とする。CLAUDE.md §13.5 および仕様書を更新済み。3x3 優先探索は不採用とし、BFS で最寄りの安全マスを探索する設計に統一。

---

## 3. 中優先度の違反 (MEDIUM)

### 3.1 StageShrinkAnimator — Presentation 層でのフレームベース判定

**ファイル:** `Features/Stage/Presentation/StageShrinkAnimator.cs:50,77`

```csharp
int currentFrame = Time.frameCount;
if (currentFrame != _lastCollectFrame && _pendingDestroys.Count > 0)
{
    FlushPendingWave();
}
```

**違反箇所:** CLAUDE.md §5
- Presentation 層がフレームタイミングに基づいてバッチ判定を行っている
- "view-only" であるべき Presentation がタイミングロジックを持つ

**修正案:**
- Domain/Application 層から「縮小ウェーブ開始/終了」のイベントを明示的に発行
- Presentation はそのイベントを購読して演出を再生するだけに限定

---

### 3.2 Presenter の optional null パラメータ

**ファイル:** 複数 Presenter (PlayerPresenter, BombPresenter, SlimePresenter, StagePresenter)

```csharp
public PlayerPresenter(
    // ...
    IAudioService audio = null,
    ICameraShakeService cameraShake = null,
    IImpactFreezeService impactFreeze = null)
```

**違反箇所:** CLAUDE.md §8 — 依存を明示する
- optional null は依存が本当にオプショナルか曖昧にする
- デバッグシーンでの使い分けが目的だが、NullObject パターンで解決すべき

**修正案:**
- `NullAudioService`, `NullCameraShakeService` 等の NullObject を一貫して使用
- コンストラクタパラメータは非 null 必須にする
- デバッグシーンでは NullObject を DI 経由で注入

---

### 3.3 SlimeId — static mutable カウンタ

**ファイル:** `Features/Slimes/Domain/SlimeId.cs`

```csharp
private static int _nextId;
public static SlimeId Next() => new(Interlocked.Increment(ref _nextId));
```

**違反箇所:** CLAUDE.md §4 — static mutable state 禁止
- スレッドセーフではあるが、テスト間でカウンタがリセットされない
- テストの独立性を損なう可能性

**修正案 (低優先度):**
- `IIdGenerator<SlimeId>` インターフェースを導入し DI で注入
- または各テストの SetUp で `SlimeId.Reset()` を呼べるようにする

---

### 3.4 TitleLifetimeScope の不在

**現状:** Title シーンには ProjectLifetimeScope のみ。TitlePresenter, KeyRebindingService 等は手動生成。

**違反箇所:** CLAUDE.md §8 — DI の composition root は LifetimeScope に限定する

**修正案:**
- TitleLifetimeScope を新設（ProjectLifetimeScope の子スコープ）
- TitlePresenter, KeyRebindingService を Scoped 登録
- TitleUIDocument は `RegisterComponentInHierarchy` で DI 管理

---

### 3.5 ResultLifetimeScope の不在

**現状:** Result 画面は Match シーン内のオーバーレイとして実装されており、MatchLifetimeScope の管轄。ただし ResultPresenter が直接 SceneManager を呼んでいる。

**補足:** 独立シーンとして分離する場合は ResultLifetimeScope が必要。現状のオーバーレイ方式なら MatchLifetimeScope 内で管理すべき。

---

### 3.6 MatchInitializer のコンストラクタ引数過多

**ファイル:** `Bootstrap/MatchInitializer.cs:74-105`

コンストラクタに **31個** の引数がある。

**違反箇所:** CLAUDE.md §1 — 神クラスを作らない

**修正案:**
- Presenter 初期化部分を `PresentationInitializer` に分離
- Input 配線部分を `InputInitializer` に分離
- MatchInitializer は各 Initializer を順次呼ぶだけのオーケストレーターに

---

## 4. 低優先度の違反 (LOW)

### 4.1 PlayerPresenter — Domain 状態の重複追跡

**ファイル:** `Features/Player/Presentation/PlayerPresenter.cs:26-36`

```csharp
private bool _isDead;
private bool _wasInvulnerable;
```

- `_isDead` は Domain の `PlayerStats.CurrentHp` で判定可能
- 完全な違反ではないが、desync のリスクあり

### 4.2 BombPresenter — Application 層への直接クエリ

**ファイル:** `Features/Bombs/Presentation/BombPresenter.cs`

- `PlayImpactHighlights()` で `StageQueryService` を直接使用
- Presentation が Application サービスに問い合わせている

### 4.3 SlimePresenter — 直接 GameObject.Instantiate

**ファイル:** `Features/Slimes/Presentation/SlimePresenter.cs`

- VFX プレハブの Instantiate/Destroy を Presenter 内で直接実行
- Infrastructure 層の VfxPool に委譲すべき

---

## 5. 良好な実装 (COMPLIANT)

以下は CLAUDE.md の思想に沿った優れた実装：

| 領域 | 評価 | 備考 |
|------|------|------|
| Domain 層の純粋性 | **優秀** | UnityEngine 参照なし、全て pure C# |
| ReactiveProperty パターン | **優秀** | private mutable + public read-only が一貫 |
| MatchPhaseScheduler 単一オーケストレータ | **優秀** | 全 Tick を配布、独自タイマー禁止を遵守 |
| R3 購読の Dispose | **優秀** | 全 Domain/Presenter で IDisposable 実装 |
| asmdef による依存方向の強制 | **優秀** | Domain → Infrastructure の逆依存なし |
| noEngineReferences の活用 | **優秀** | 7 つの asmdef が noEngineReferences: true |
| TileView の薄い MonoBehaviour | **優秀** | View は状態を持たない、Presenter が駆動 |
| ScriptableObject による設定一元管理 | **良好** | BalanceConfig で仕様パラメータを集中管理 |
| NullAudioService パターン | **良好** | フォールバック用 NullObject |
| MatchPlayers ホルダー | **良好** | VContainer のキー登録不在への対処 |

---

## 6. LifetimeScope 階層化の調査

### 現状 (P0 修正後)

```
ProjectLifetimeScope (Title シーン, DontDestroyOnLoad)
├── TitleLifetimeScope (Title シーン) ← P0 で新設済み
│   ├── TitleInitializer (IStartable EntryPoint)
│   ├── KeyRebindingService (Scoped)
│   └── TitleUIDocument (RegisterComponentInHierarchy)
│
└── MatchLifetimeScope (Match シーン) — 既存
    └── (既存の ~30 サービス)
```

- TitleTestSpaceController は DI 不使用のまま (P3 で対処)
- Result 画面は Match 内オーバーレイ（MatchLifetimeScope 管轄）
- デバッグシーン 6 つは全て DI 不使用

### 推奨階層 (将来)

```
ProjectLifetimeScope (DontDestroyOnLoad)
├── TitleLifetimeScope (Title シーン) — 実装済み
│   └── TitleTestSpaceController → TestSpaceLifetimeScope (子スコープ) ← 将来
│
├── MatchLifetimeScope (Match シーン) — 既存
│   └── (既存の ~30 サービス)
│
└── DebugLifetimeScope (デバッグシーン共通) ← 将来
    └── 各プレビューシーンの子スコープ
```

---

## 7. デバッグシーンの DI 化調査

### 現状のデバッグシーン一覧

| シーン | コントローラー | 手動 `new` 数 |
|--------|--------------|-------------|
| StagePreview | StagePreviewController | ~15 |
| PlayerPreview | PlayerPreviewController | ~25 |
| BombPreview | BombPreviewController | ~20 |
| SlimePreview | SlimePreviewController | ~20 |
| CameraPreview | CameraPreviewController | ~15 |
| UIPreview | UIPreviewController | ~10 |

全コントローラーが共通パターン：
- `Start()` で Domain/Application/Presentation サービスを全て手動 `new`
- SerializeField で Factory (MonoBehaviour) のみ参照
- `OnDestroy()` で手動 Dispose

### DI 化の設計案

#### 方針: DebugLifetimeScope + Feature サブセット登録

```csharp
public abstract class DebugLifetimeScope : LifetimeScope
{
    [SerializeField] protected BalanceConfig _balance;

    protected override void Configure(IContainerBuilder builder)
    {
        // 共通グローバル (ProjectLifetimeScope のフォールバック)
        builder.RegisterInstance<IBalanceParameters>(_balance);
        builder.Register<IRandomProvider>(
            c => new SeededRandomProvider(42), Lifetime.Singleton);
        builder.Register<UnityTimeProvider>(Lifetime.Singleton).As<ITimeProvider>();
        builder.Register<NullAudioService>(Lifetime.Singleton).As<IAudioService>();
        builder.Register<NullCameraShakeService>(Lifetime.Singleton).As<ICameraShakeService>();

        // 共通 Domain
        RegisterStageDomain(builder);

        // 派生クラスで Feature 固有の登録
        ConfigureFeature(builder);
    }

    protected abstract void ConfigureFeature(IContainerBuilder builder);

    private static void RegisterStageDomain(IContainerBuilder builder)
    {
        // StageModel, TileTimerService, StageQueryService 等の共通登録
    }
}
```

#### StagePreview の例

```csharp
public sealed class StagePreviewLifetimeScope : DebugLifetimeScope
{
    protected override void ConfigureFeature(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<StageViewFactory>();
        builder.Register<TileAnimationService>(Lifetime.Scoped);
        builder.Register<StagePresenter>(Lifetime.Scoped);
        builder.RegisterEntryPoint<StagePreviewRunner>();
    }
}
```

### メリット

1. **依存方向の強制**: asmdef で守られた依存方向がデバッグシーンでも維持される
2. **テスト可能性**: サービスの差し替え（Mock 注入）が容易に
3. **コード削減**: 各コントローラーの手動 `new` + Dispose コードが不要に
4. **一貫性**: 本番コードと同じ DI パターンでデバッグ環境も動作

### 段階的移行計画

1. **Phase A**: `DebugLifetimeScope` 基底クラスを作成
2. **Phase B**: 最もシンプルな `StagePreview` を DI 化（パイロット）
3. **Phase C**: 残りのプレビューシーンを順次移行
4. **Phase D**: `TitleTestSpaceController` を DI 化（最も複雑）

---

## 8. 優先順位付きアクションプラン

### P0 — 即時対応 (アーキテクチャの根幹に関わる)

| # | 内容 | 関連違反 |
|---|------|---------|
| 1 | `MatchModeSelection` を非 static 化 + DI 管理 | §1.1 |
| 2 | `ISceneTransitionService` 導入 + Presenter からの SceneManager 直接呼び出し除去 | §1.2, §1.3 |
| 3 | `TitleLifetimeScope` 新設 + TitleUIDocument の手動サービスロケータ除去 | §1.4, §3.4 |

### P1 — 高優先度 (次のスプリントで対応)

| # | 内容 | 関連違反 |
|---|------|---------|
| ~~4~~ | ~~MatchInitializer の分割（Presenter 初期化 / Input 初期化を分離）~~ | ~~§2.1, §3.6~~ — **完了** (PresentationInitializer + InputInitializer に分離済み) |
| 5 | PlayerInputAdapter の DI 管理化 (FindObjectsByType 除去) | §2.2 |
| ~~6~~ | ~~SafeTileSearchService に 3x3 優先探索を実装~~ | ~~§2.5~~ — **仕様変更により対象外** |
| ~~7~~ | ~~ダッシュクールダウンを BalanceConfig に移動~~ | ~~§2.4~~ — **完了** |

### P2 — 中優先度 (余裕がある時に対応)

| # | 内容 | 関連違反 |
|---|------|---------|
| ~~8~~ | ~~Presenter の optional null パラメータを NullObject に統一~~ | ~~§3.2~~ — **完了** |
| 9 | DebugLifetimeScope 新設 + StagePreview パイロット移行 | §7 |
| 10 | StageShrinkAnimator のフレーム判定を Domain イベントに置換 | §3.1 |
| 11 | MatchPresenters の mutable setter を Builder/Factory に変更 | §2.3 |

### P3 — 低優先度 (リファクタリングの機会に)

| # | 内容 | 関連違反 |
|---|------|---------|
| 12 | TitleTestSpaceController の DI 化 | §1.5 |
| 13 | 残りデバッグシーンの DI 化 | §7 |
| 14 | SlimeId の static カウンタ改善 | §3.3 |
| 15 | BombPresenter の StageQueryService 直接使用の解消 | §4.2 |

---

## 9. P0 修正完了ログ (2026-03-27)

以下の P0 項目を修正済み:

| # | 内容 | 状態 |
|---|------|------|
| 1 | `MatchModeSelection` → `MatchModeConfig` (ProjectLifetimeScope Singleton) | **完了** |
| 2 | `ISceneTransitionService` 導入 + SceneManager 直接呼び出し除去 | **完了** |
| 3 | `TitleLifetimeScope` 新設 + TitleUIDocument の手動サービスロケータ除去 | **完了** |

### 変更ファイル

**新規 (5):** `MatchModeConfig.cs`, `ISceneTransitionService.cs`, `UnitySceneTransitionService.cs`, `TitleLifetimeScope.cs`, `TitleInitializer.cs`

**修正 (5):** `ProjectLifetimeScope.cs`, `MatchLifetimeScope.cs`, `MatchInitializer.cs`, `TitlePresenter.cs`, `ResultPresenter.cs`

**クリーンアップ (1):** `TitleUIDocument.cs` (Start 削除)

**削除 (1):** `MatchModeSelection.cs`

### §1.1〜1.4, §3.4 のステータス更新

- §1.1 `MatchModeSelection` → **修正済み** (MatchModeConfig に DI 管理化)
- §1.2 `TitlePresenter` SceneManager → **修正済み** (ISceneTransitionService 経由)
- §1.3 `ResultPresenter` SceneManager → **修正済み** (同上)
- §1.4 `TitleUIDocument` サービスロケータ → **修正済み** (TitleLifetimeScope + TitleInitializer)
- §3.4 TitleLifetimeScope 不在 → **修正済み** (新設)

### Layer 1〜4 修正完了ログ (2026-03-27)

#### Layer 1: 命名・namespace 基盤修正

| # | 内容 | 状態 |
|---|------|------|
| 10.1 | `FallBomb/` → `BreakBomb/` + `FallBombResolver.cs` → `BreakBombResolver.cs` リネーム | **完了** |
| 10.2 | `UpgradeApplyService` namespace: `Upgrades.Domain` → `Upgrades.Application` | **完了** |
| 10.5 | `PlayerMoveService`/`PlayerDamageService` namespace: `Player.Domain` → `Player.Application` | **完了** |
| 10.11 | 古い `ICameraShakeService.cs` トゥームストーン削除 | **完了** |

#### Layer 2: コンストラクタインジェクション化

| # | 内容 | 状態 |
|---|------|------|
| 10.10d | `SlimeSpawnService` — 全5引数をコンストラクタへ、`SpawnIfNeeded()` 引数なしに | **完了** |
| 10.10b | `SlimeAiService` — registry/players/stage/balance をコンストラクタへ、`TickAll(dt)` のみに | **完了** |
| 10.10c | `SlimeTickService` — 安定引数をコンストラクタへ、`Tick(dt)` のみに | **完了** |
| 10.10a | `PlayerDamageService` — stage/safeTileSearch をコンストラクタへ | **完了** |

#### Layer 3: 責務の再配置

| # | 内容 | 状態 |
|---|------|------|
| 10.7 | `FireDamageTickService` を `MatchFlow/Application` → `Bombs/Application` に移動 | **完了** |
| 10.6 | `UpgradeDraftService` を `Upgrades/Domain` → `Upgrades/Application` に移動 | **完了** |
| 10.8 | `MatchPhaseScheduler.TransitionToStageShrink` のダメージを `PlayerDamageService` に委譲 + `SafeTileSearchService` 依存除去 | **完了** |

#### Layer 4: 値の外部化

| # | 内容 | 状態 |
|---|------|------|
| 11.1 | `IBalanceParameters` + `BalanceConfig` に強化効果量 10 プロパティ追加 | **完了** |
| 11.1 | `UpgradeApplyService` に全 UpgradeId の処理を集約 | **完了** |
| 11.1 | `PlayerBuild.ApplyUpgrade()` switch 削除、setter を public 化 | **完了** |

---

## 10. 命名と責務の不一致

### 10.1 FallBombResolver.cs — ファイル名とクラス名の不一致

**ファイル:** `Features/Bombs/Domain/FallBomb/FallBombResolver.cs`
**ディレクトリ:** `FallBomb/`

ファイル名・ディレクトリ名は `FallBomb` だが、中のクラスは `BreakBombResolver` + `BreakBombResult`。

仕様書では「滑落ボム」= Break Bomb。コード内では `BombType.Break` が正式名。
`FallBomb/` ディレクトリと `FallBombResolver.cs` ファイル名がレガシーの名残。

**修正案:** ディレクトリ名を `BreakBomb/`、ファイル名を `BreakBombResolver.cs` にリネーム。

---

### 10.2 UpgradeApplyService — ディレクトリと namespace の不一致

**ファイル:** `Features/Upgrades/Application/UpgradeApplyService.cs`

ファイルは `Application/` ディレクトリにあるが、namespace が `FloorBreaker.Upgrades.Domain`。

```csharp
namespace FloorBreaker.Upgrades.Domain  // ← Application にあるのに Domain
{
    public sealed class UpgradeApplyService
```

**修正案:** namespace を `FloorBreaker.Upgrades.Application` に変更。

---

### 10.3 IAudioService — 音量設定メソッドの命名不一致

**ファイル:** `Shared/Application/Interfaces/IAudioService.cs`

| メソッド | 問題 |
|----------|------|
| `SetBgmVolume(float volume, float fadeDuration)` | ダッキング用。名前が `SetBgmVolumeLevel` と紛らわしい |
| `SetBgmVolumeLevel(float volume)` | 永続化用。`SetBgmVolume` との違いが名前だけでは不明 |

2つの異なる目的の BGM 音量メソッドが混在:
- `SetBgmVolume` = 一時的なダッキング (フェードあり、永続化なし)
- `SetBgmVolumeLevel` = ユーザー設定 (永続化あり)

**修正案:** `SetBgmVolume` → `DuckBgm(float volume, float fadeDuration)` にリネーム。

---

### 10.4 GameplayInputBridge — Application 層に Infrastructure 依存

**ファイル:** `Features/Input/Application/GameplayInputBridge.cs:10`

```csharp
using FloorBreaker.Input.Infrastructure;  // PlayerInputAdapter を参照
```

Application 層が Infrastructure 層に直接依存している。`PlayerInputAdapter` (MonoBehaviour) を受け取る `RegisterAdapter` メソッドがある。

**修正案:** `IPlayerInputSource` インターフェースを Application に定義し、PlayerInputAdapter が実装する形に変更。

---

### 10.5 PlayerMoveService / PlayerDamageService — ディレクトリと namespace の不一致

**ファイル:**
- `Features/Player/Application/PlayerMoveService.cs`
- `Features/Player/Application/PlayerDamageService.cs`

両方とも `Application/` ディレクトリにあるが、namespace が `FloorBreaker.Player.Domain`。
10.2 と同じ問題。namespace を `FloorBreaker.Player.Application` に変更すべき。

---

### 10.6 UpgradeDraftService — Domain 層に Application 依存

**ファイル:** `Features/Upgrades/Domain/UpgradeDraftService.cs`

Domain namespace だが `IBalanceParameters` (Application 層インターフェース) と `IRandomProvider` (同) に依存。
Application 層のサービスとして分類すべき。

---

### 10.7 FireDamageTickService — MatchFlow に配置されているが Bombs/Stage の責務

**ファイル:** `Features/MatchFlow/Application/FireDamageTickService.cs`

炎 DoT ダメージはボム/ステージの責務であり、MatchFlow (試合進行) の責務ではない。
`Bombs/Application` または `Stage/Application` に移動すべき。

---

### 10.8 MatchPhaseScheduler — Scheduler を超えた直接ゲームロジック実行

**ファイル:** `Features/MatchFlow/Application/MatchPhaseScheduler.cs`

`TransitionToStageShrink()` メソッド内で、プレイヤーへのダメージ適用・強制移動・スライム撃破を直接実行している。
名前の通り「スケジューラ」であれば、各サービスに委譲すべき。

---

### 10.9 BombLaunchUseCase — 名前より広い責務

**ファイル:** `Features/Bombs/Application/BombLaunchUseCase.cs`

「Launch」だが、スペック生成 (`CreateBreakBombSpec`, `CreateFireBombSpec`)、着弾解決 (`ResolveLanding`)、着弾後の効果適用 (`ExecuteLanding`) も含む。
名前を `BombUseCase` にするか、スペック生成を `BombSpecFactory` に分離すべき。

---

### 10.10 メソッド引数のアンチパターン — 安定参照を毎回渡す

以下のサービスで、Match スコープで不変の参照を `Tick()` や処理メソッドの引数で毎回渡している:

| クラス | メソッド | 毎回渡している引数 |
|--------|---------|-------------------|
| `PlayerDamageService` | `ApplyDamage(...)` | `stage`, `safeTileSearch`, `occupied` |
| `SlimeTickService` | `Tick(...)` | `players`, `stage`, `random`, `balance` |
| `SlimeAiService` | `TickAll(...)` | `registry`, `players`, `stage`, `balance` |
| `SlimeSpawnService` | `SpawnIfNeeded(...)` | `stage`, `registry`, `players`, `random`, `balance` |

これらは全てコンストラクタインジェクションで注入すべき安定した依存。

---

### 10.11 ICameraShakeService — 古いファイルの残存

**ファイル:** `Features/Cameras/Presentation/ICameraShakeService.cs`

移動済みのトゥームストーンファイルが残っている（実体は `Shared/Presentation/Common/` にある）。削除すべき。

---

### 10.12 SlimeModel — デフォルト引数にバランス値

**ファイル:** `Features/Slimes/Domain/SlimeModel.cs:15`

```csharp
public SlimeModel(SlimeId id, SlimeType type, GridPos position, float initialAttackCooldown = 1f)
```

`1f` は `IBalanceParameters.SlimeAttackCooldown` から取得すべき値。呼び出し側で渡すなら default は不要。

---

## 11. ハードコードされた数値・一元管理されていない値

### 11.1 PlayerBuild.ApplyUpgrade — 強化効果量のハードコード

**ファイル:** `Features/Player/Domain/PlayerBuild.cs:68-118`

| 行 | コード | 意味 | あるべき姿 |
|----|--------|------|-----------|
| 73 | `FireFlightRange += 2` | 炎ボム飛距離 +2 | UpgradeDefinition or IBalanceParameters |
| 82 | `FireDuration += 2f` | 炎持続時間 +2秒 | 同上 |
| 88 | `FireCooldown - 0.3f` | 炎ボム CD -0.3秒 | 同上 |
| 91 | `BreakFlightRange += 2` | ブレークボム飛距離 +2 | 同上 |
| 100 | `BreakCollapseTime += 2f` | 崩落時間 +2秒 | 同上 |
| 103 | `BreakCooldown - 0.5f` | ブレーク CD -0.5秒 | 同上 |

仕様書の強化効果量がそのまま Domain コードにハードコードされている。
`UpgradeDefinition` や `IBalanceParameters` に集約すべき。

---

### 11.2 GameplayInputBridge — 入力タイミング定数

**ファイル:** `Features/Input/Application/GameplayInputBridge.cs`

| 行 | 値 | 意味 |
|----|-----|------|
| 22 | `BaseMoveInterval = 0.2f` | 基本移動間隔 |
| 25 | `InitialRepeatDelay = 0.15f` | ホールドリピート開始遅延 |
| 32 | `InputBufferTime = 0.04f` | 入力バッファ時間 |
| 243 | `1f` (TODO付き) | ダッシュクールダウン |
| 259 | `0.1f` | 速度の最小値フォールバック |

`BaseMoveInterval` と `InputBufferTime` は操作フィール調整値で `IBalanceParameters` が妥当。
`DashCooldown` は既に `IBalanceParameters` にプロパティ存在 (TODO コメントあり)。

---

### 11.3 CpuPlayerBrain — AI パラメータのハードコード

**ファイル:** `Features/CpuPlayer/Application/CpuPlayerBrain.cs`

| 行 | 値 | 意味 |
|----|-----|------|
| 21 | `ThinkInterval = 0.2f` | 思考間隔 |
| 22 | `BaseMoveInterval = 0.2f` | CPU 基本移動間隔 |
| 23 | `BombReleaseDelay = 0.08f` | ボムリリース遅延 |

CPU AI のパラメータが全て const。ScriptableObject または IBalanceParameters で一元管理すべき。

---

### 11.4 CpuUpgradeSelector — 購入タイミングのハードコード

**ファイル:** `Features/CpuPlayer/Application/CpuUpgradeSelector.cs`

| 行 | 値 | 意味 |
|----|-----|------|
| 13 | `InitialDelay = 1.5f` | 強化フェーズ開始後の待機時間 |
| 14 | `PurchaseInterval = 0.6f` | 購入操作の間隔 |

---

### 11.5 SlimeAiService — 移動閾値のハードコード

**ファイル:** `Features/Slimes/Domain/SlimeAiService.cs:29`

```csharp
float moveThreshold = 1f; // 1マス分のアキュムレータ閾値
```

値自体は論理的に1マス=1.0で正しいが、`IBalanceParameters.SlimeSpeedMultiplier` との関連が暗黙的。

---

## 12. ランタイムバグ・動作不具合

### 12.1 シーン遷移時の MissingReferenceException

**発生条件:** Match → Title (Rematch/Title ボタン押下時)

```
MissingReferenceException: PlayerView has been destroyed but you are still trying to access it
  PlayerAnimationService.StartInvulnerabilityBlink()
  PlayerPresenter.Tick()
  MatchPresenters.TickPresenters()
  MatchTickRunner.Tick()
```

**原因:** `SceneManager.LoadScene` でシーンが切り替わる際、MatchLifetimeScope の Dispose より先に `MatchTickRunner.Tick()` が走り、破棄済みの PlayerView にアクセスしている。

**修正案:**
- `MatchTickRunner` に `_disposed` フラグを追加し、Tick 冒頭で早期リターン
- または `PlayerPresenter.Tick()` で View の null/destroyed チェック
- `ISceneTransitionService.LoadMatch/LoadTitle` 実行前に MatchPresenters.Dispose() を呼ぶ

---

### 12.2 Title 音量設定が Match の SFX に反映されない

**現象:** Title 画面で SFX 音量スライダーを変更しても、Match シーンの効果音に反映されない。

**想定原因:** AudioService の `SetSfxVolume` が AudioSource プールの音量に即時反映されていない、または新しい AudioSource に音量設定が引き継がれていない可能性。

**調査必要箇所:** `AudioService.SetSfxVolume()` と `PlaySfx()` の音量適用ロジック。

---

### 12.3 TitleTestSpaceController — Match とは別系統の独自ロジック

**現象:**
- タイルの効果位置がずれる
- SE が停止しない
- エフェクトが変な位置に表示される

**原因:** TitleTestSpaceController (§1.5) は Match のロジックとは完全に別系統。
独自の StageModel, PlayerModel, BombLaunchUseCase 等を手動 `new` しており:
- ワールドオフセット計算が Match と異なる
- SE の停止処理が未実装 (AudioService への dispose/stop 通知がない)
- VFX プールが Match と異なる管理方式

**根本原因:** TitleTestSpaceController が 539 行の神 MonoBehaviour であり (§1.5)、Match と同じ DI 管理下にないため、同じ挙動を保証できない。

**修正方針:**
- **短期:** タイル位置のワールドオフセットバグを修正、シーン遷移時に SE を停止
- **長期 (P3):** TitleTestSpaceController を DI 化し、Match と同じサービスインスタンスを使う構造に移行

---

### 12.4 ボムがスライムをすり抜ける (不安定)

**現象:** ボムがスライムに衝突せずすり抜ける場合がある。常に再現するわけではなく不安定。

**想定原因:**
- `BombFlightTracker` が 1 Tick で複数マス進む際に中間マスのエンティティチェックが漏れている可能性
- `SlimeRegistry` の位置情報と実際のスライム位置の同期タイミングの問題

**調査必要箇所:** `BombFlightTracker.TickPlayer()` のタイル進行ループ内のエンティティ衝突判定。

---

### 12.5 崩落マスでボムが止まったり通過したりする (不安定)

**現象:** 崩落中/崩落済みのマスにボムが衝突して止まる場合と通過する場合がある。

**想定原因:**
- `BombLandingResolver` の `IsPassable` 判定で `Collapsing` / `Collapsed` の扱いが状態遷移のタイミングに依存
- 炎ボムと滑落ボムで異なる判定条件が適用されている可能性

**調査必要箇所:** `BombLandingResolver.Resolve()` と `StageModel.IsPassable()` のタイル状態判定。

---

### 12.6 一時強化 (FireShield / Levitation) が機能しない

**発生条件:** 強化フェーズで「炎守りのマント」「風の羽衣」を選択した後、効果が一切発動しない。

**原因 (2 件):**

1. **`ClearTemporaryEffects()` の呼び出しタイミングが誤り**
   - `TransitionToRunning()` で呼んでいたため、強化フェーズで選択した直後にクリアされていた。
   - 仕様:「次の強化フェーズ開始時に消失する」→ `TransitionToUpgradePhase()` で呼ぶべき。

2. **ステージ縮小ダメージで `LevitationActive` チェックがない**
   - `MatchPhaseScheduler.TransitionToStageShrink()` のダメージ適用で、風の羽衣の判定がなかった。

**修正:**
- `ClearTemporaryEffects()` を `TransitionToRunning()` → `TransitionToUpgradePhase()` に移動。
- ステージ縮小ダメージに Levitation チェック追加（有効時はダメージ 0 + 強制移動のみ）。

---

## 付録: 違反サマリー表

| ID | 深刻度 | カテゴリ | ファイル | CLAUDE.md 該当 | 状態 |
|----|--------|---------|---------|--------------|------|
| 1.1 | ~~CRITICAL~~ | ~~static mutable state~~ | ~~MatchModeSelection.cs~~ | ~~§4, §20~~ | **修正済み** |
| 1.2 | ~~CRITICAL~~ | ~~依存方向~~ | ~~TitlePresenter.cs~~ | ~~§4~~ | **修正済み** |
| 1.3 | ~~CRITICAL~~ | ~~依存方向~~ | ~~ResultPresenter.cs~~ | ~~§4~~ | **修正済み** |
| 1.4 | ~~CRITICAL~~ | ~~DI 不使用~~ | ~~TitleUIDocument.cs~~ | ~~§8~~ | **修正済み** |
| 1.5 | CRITICAL | 神 MonoBehaviour | TitleTestSpaceController.cs | §1, §8, §20 | 未着手 |
| ~~2.1~~ | ~~HIGH~~ | ~~Presenter 手動生成~~ | ~~MatchInitializer.cs~~ | ~~§8~~ | **完了** (PresentationInitializer に分離) |
| 2.2 | HIGH | FindObjectsByType | MatchInitializer.cs | §8 | 未着手 |
| 2.3 | HIGH | mutable ホルダー | MatchPresenters.cs | §5 精神 | 未着手 |
| ~~2.4~~ | ~~HIGH~~ | ~~ハードコード値~~ | ~~GameplayInputBridge.cs~~ | ~~§0~~ | **完了** (IBalanceParameters に外部化) |
| ~~2.5~~ | ~~HIGH~~ | ~~仕様未実装~~ | ~~SafeTileSearchService.cs~~ | ~~§13.5~~ | **仕様変更により対象外** |
| 3.1 | MEDIUM | Presentation ロジック | StageShrinkAnimator.cs | §5 | 未着手 |
| ~~3.2~~ | ~~MEDIUM~~ | ~~optional null~~ | ~~複数 Presenter~~ | ~~§8~~ | **完了** (NullObject パターンに統一、NullCameraShakeService を Shared に移動) |
| 3.3 | MEDIUM | static カウンタ | SlimeId.cs | §4 | 未着手 |
| ~~3.4~~ | ~~MEDIUM~~ | ~~LifetimeScope 不在~~ | ~~Title シーン~~ | ~~§8~~ | **修正済み** |
| 3.5 | MEDIUM | LifetimeScope 不在 | Result 画面 | §8 | 未着手 |
| ~~3.6~~ | ~~MEDIUM~~ | ~~引数過多~~ | ~~MatchInitializer.cs~~ | ~~§1~~ | **完了** (10 引数に削減、Initializer 分離) |
| 4.1 | LOW | 状態重複 | PlayerPresenter.cs | §5 | 未着手 |
| 4.2 | LOW | 依存方向 | BombPresenter.cs | §4 | 未着手 |
| 4.3 | LOW | 直接 Instantiate | SlimePresenter.cs | §4 | 未着手 |
| ~~10.1~~ | ~~HIGH~~ | ~~命名不一致~~ | ~~FallBombResolver.cs / FallBomb/~~ | ~~§15~~ | **修正済み** |
| ~~10.2~~ | ~~MEDIUM~~ | ~~namespace 不一致~~ | ~~UpgradeApplyService.cs~~ | ~~§3, §4~~ | **修正済み** |
| ~~10.3~~ | ~~LOW~~ | ~~メソッド名紛らわしい~~ | ~~IAudioService.cs~~ | ~~§15~~ | **完了** (SetBgmVolume → DuckBgm) |
| 10.4 | MEDIUM | Application→Infrastructure依存 | GameplayInputBridge.cs | §4 | 未着手 |
| ~~10.5~~ | ~~HIGH~~ | ~~namespace 不一致~~ | ~~PlayerMoveService.cs, PlayerDamageService.cs~~ | ~~§3, §4~~ | **修正済み** |
| ~~10.6~~ | ~~MEDIUM~~ | ~~Domain に Application 依存~~ | ~~UpgradeDraftService.cs~~ | ~~§4~~ | **修正済み** |
| ~~10.7~~ | ~~MEDIUM~~ | ~~Feature 配置不適切~~ | ~~FireDamageTickService.cs~~ | ~~§3~~ | **修正済み** |
| ~~10.8~~ | ~~MEDIUM~~ | ~~Scheduler の責務超過~~ | ~~MatchPhaseScheduler.cs~~ | ~~§7~~ | **修正済み** |
| 10.9 | MEDIUM | クラス名より広い責務 | BombLaunchUseCase.cs | §15 | 未着手 |
| ~~10.10~~ | ~~MEDIUM~~ | ~~安定参照を毎回引数で渡す~~ | ~~複数 Service~~ | ~~§8~~ | **修正済み** |
| ~~10.11~~ | ~~LOW~~ | ~~古いファイル残存~~ | ~~Cameras/ICameraShakeService.cs~~ | ~~—~~ | **修正済み** |
| ~~10.12~~ | ~~LOW~~ | ~~デフォルト引数にバランス値~~ | ~~SlimeModel.cs~~ | ~~§0~~ | **完了** (デフォルト引数削除) |
| ~~11.1~~ | ~~HIGH~~ | ~~強化効果量ハードコード~~ | ~~PlayerBuild.cs~~ | ~~§0~~ | **修正済み** |
| ~~11.2~~ | ~~MEDIUM~~ | ~~入力定数ハードコード~~ | ~~GameplayInputBridge.cs~~ | ~~§0~~ | **完了** (IBalanceParameters に外部化) |
| ~~11.3~~ | ~~MEDIUM~~ | ~~AI定数ハードコード~~ | ~~CpuPlayerBrain.cs~~ | ~~§0~~ | **完了** (IBalanceParameters に外部化) |
| ~~11.4~~ | ~~LOW~~ | ~~CPU購入タイミングハードコード~~ | ~~CpuUpgradeSelector.cs~~ | ~~§0~~ | **完了** (IBalanceParameters に外部化) |
| 11.5 | LOW | 移動閾値ハードコード | SlimeAiService.cs | §0 | 未着手 |
| ~~12.1~~ | ~~HIGH~~ | ~~シーン遷移時 MissingReferenceException~~ | ~~MatchTickRunner / PlayerPresenter~~ | ~~—~~ | **修正済み** |
| ~~12.2~~ | ~~MEDIUM~~ | ~~Title 音量設定 SFX フィードバック~~ | ~~TitlePresenter~~ | ~~—~~ | **修正済み** |
| 12.3 | HIGH | Title テスト空間の位置ずれ・SE 残留 | TitleTestSpaceController | §1.5 | 未着手 |
| 12.4 | HIGH | ボムがスライムをすり抜ける (不安定) | BombFlightTracker / BombLandingResolver | — | **調査済み** ロジック正常、テスト追加で確認。タイミング依存の可能性、実プレイ確認要 |
| ~~12.5~~ | ~~MEDIUM~~ | ~~崩落マスでボムが止まったり通過したりする~~ | ~~BombLandingResolver / BombFlightTracker~~ | ~~—~~ | **修正済み** ボムは穴 (Collapsed/PermanentlyDestroyed) を飛び越えるよう修正 |
| ~~12.6~~ | ~~CRITICAL~~ | ~~一時強化 (FireShield/Levitation) が機能しない~~ | ~~MatchPhaseScheduler~~ | ~~§14~~ | **修正済み** ClearTemporaryEffects を TransitionToUpgradePhase に移動 + 縮小ダメージに Levitation チェック追加 |
