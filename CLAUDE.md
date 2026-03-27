# CLAUDE.md

このファイルは、このリポジトリで作業する AI コーディングエージェント向けの運用ルールを定義する。
目的は、**Unity 6.3 + VContainer + UniTask + R3 + UI Toolkit** 前提で、`FLOOR BREAKER` を長期的に崩れにくい構造で開発し続けること。

このプロジェクトは、2人対戦・30x30 グリッド・8方向移動・2種類のボム・スライム湧き・20秒ごとの強化フェーズ・同周期のステージ縮小・分割画面 HUD・UIToolkit オーバーレイを持つリアルタイム対戦ゲームである。仕様を壊さないことを最優先にする。

### 関連ドキュメント

- **ゲーム仕様書**: [`docs/implementation.md`](docs/implementation.md) — ゲームルール、パラメータ、アルゴリズムの詳細定義。実装判断に迷ったら必ず参照すること。

---

## 0. プロジェクト前提

- テーマは「床」のリアルタイム対戦ゲーム。
- プレイヤーは 2 人、画面は左右分割。
- フィールドは 30x30 のグリッド。
- ボムは「ブレークボム」と「炎ボム」の 2 種。
- 20 秒ごとに
  - ステージ外周 1 列が永久消滅し
  - 強化フェーズが始まり
  - 強化 UI は UIToolkit オーバーレイで表示される。
- スライムは 5 秒ごとにスポーンチェックする。
- 退避先探索は 3x3 優先、なければ BFS を使う。

**重要:** このゲームは「物理でたまたまそう見えるゲーム」ではなく、**グリッド規則で成立するルール駆動ゲーム**として実装すること。

### Tilemap を使用しない方針

このプロジェクトでは **Unity Tilemap を使用しない**。理由は以下の通り。

- 30x30 のほぼ全マスがリアルタイムで状態遷移する（通常→炎上→崩落中→崩落済み→復帰、永久消滅、壁化など）。
- Tilemap はタイル単位のリアルタイム状態管理に向いておらず、`SetTile` / `RefreshTile` のコストと制約がボトルネックになる。
- タイル状態は Domain 側の `StageModel` が authoritative に管理し、見た目は Presentation 層で SpriteRenderer や VFX を使って反映する。
- グリッド座標 → ワールド座標の変換は単純な算術で行い、`Grid` コンポーネントに依存しない。

---

## 1. このプロジェクトで最重要の方針

1. **ゲームルールを pure C# へ寄せる。**
2. **MonoBehaviour は View / Adapter に限定する。**
3. **DI の composition root は LifetimeScope に限定する。**
4. **one-shot async は UniTask、継続監視は R3 で分ける。**
5. **Runtime UI は UI Toolkit を第一選択にする。**
6. **仕様に関わる時系列は、単一の Phase 制御から駆動する。**
7. **グリッド上の真実を先に更新し、見た目はその結果を反映する。**
8. **局所的で安全な変更を優先し、神クラスを作らない。**

迷ったら、以下を優先する。

- 薄い MonoBehaviour
- 明示的な依存
- 小さい UseCase
- 小さい Presenter
- 小さい LifetimeScope
- テスト可能な pure C#
- 仕様書に直接対応する命名

---

## 2. 技術スタックの役割分担

### VContainer

- 依存解決、ライフタイム管理、composition root を担当する。
- `LifetimeScope` / installer / bootstrap 以外で container に触れない。
- `IObjectResolver` を業務ロジックに持ち込まない。
- `RegisterEntryPoint` を使い、可能な限り純 C# の起動点を使う。

### UniTask

- one-shot async を担当する。
- 例:
  - 初期化
  - シーン遷移
  - アセットロード
  - 演出待機
  - フェーズ遷移の一回限り処理
  - 保存 / 読み込み

### R3

- 継続する状態変化・購読・UI 反映を担当する。
- 例:
  - HP
  - コイン
  - クールダウン残り
  - フェーズ残り時間
  - 選択中強化カード
  - 強化候補一覧
  - UI 開閉状態

### UI Toolkit

- ランタイム UI の第一選択。
- 例:
  - タイトル
  - HUD
  - 強化フェーズ UI
  - リザルト
  - 一時停止 UI
- ワールド上のプレイヤー / スライム / ボム / 床表現には使わない。

### MonoBehaviour

- Unity イベント受け口、SerializeField、参照保持、見た目更新、GameObject 操作に限定する。
- ルール、進行制御、永続状態、選択ロジックは持たない。

### 使用アセット（Asset Store / 外部）

以下のアセットをプロジェクトで使用する。各アセットの責務を明確にし、Presentation / Infrastructure 層でのみ利用すること。

| アセット | 用途 | 利用層 |
|---|---|---|
| **Feel** | 画面シェイク、ヒットストップ、被弾フラッシュ等のゲームフィール全般 | Presentation |
| **Epic Toon FX** | 炎・爆発・衝撃波の VFX パーティクル | Presentation |
| **DOTween Pro** | UI 演出、強制移動、タイル崩落アニメーション等のトゥイーン全般 | Presentation / Infrastructure |
| **Hot Reload** | コンパイルなし即反映で開発イテレーション高速化 | 開発専用（ビルドに含めない） |
| **Medieval Fantasy SFX Bundle** | ボム爆発・炎・崩落・スライム・UI 等の SE 全般 | Infrastructure (Audio) |
| **All In 1 Sprite Shader** | スプライトへのヒットフラッシュ・アウトライン・ディゾルブ等の視覚効果 | Presentation |

#### アセット利用ルール

- アセットの機能を Domain / Application 層に持ち込まない。
- DOTween のシーケンスは Presentation 層の演出コードに閉じ込める。Domain の状態遷移を DOTween コールバックで駆動しない。
- Feel の Feedback は MonoBehaviour (Presenter / View) から発火する。ゲームルールの判定に Feel の状態を使わない。
- SE 再生は Infrastructure 層の AudioService 経由で行い、SFX ファイルへの直接参照を Presentation 以外に持たない。
- Hot Reload はエディタ専用。`#if UNITY_EDITOR` ガード、またはエディタ asmdef に閉じること。

---

## 3. このゲームに適したアーキテクチャ

このゲームでは **feature-first + layer-within-feature** を採用する。

```text
Assets/App/
  Bootstrap/
    ProjectLifetimeScope.cs
    MatchLifetimeScope.cs
    TitleLifetimeScope.cs
    ResultLifetimeScope.cs
    Installers/

  Shared/
    Domain/
      Grid/
      Primitives/
      Timing/
      Random/
    Application/
      Interfaces/
      Events/
    Infrastructure/
      UnityTime/
      Persistence/
      AssetLoading/
    Presentation/
      Common/

  Features/
    MatchFlow/
      Domain/
      Application/
      Infrastructure/
      Presentation/

    Stage/
      Domain/
        GridPos.cs
        StageBounds.cs
        TileState.cs
        StageModel.cs
        SafeTileSearchService.cs
        WallGenerationService.cs
      Application/
      Infrastructure/
      Presentation/

    Player/
      Domain/
      Application/
      Infrastructure/
      Presentation/

    Bombs/
      Domain/
        BreakBomb/
        FireBomb/
        Shared/
      Application/
      Infrastructure/
      Presentation/

    Slimes/
      Domain/
      Application/
      Infrastructure/
      Presentation/

    Upgrades/
      Domain/
      Application/
      Infrastructure/
      Presentation/

    Cameras/
      Domain/
      Application/
      Presentation/

    Input/
      Application/
      Infrastructure/
      Presentation/

    UI/
      RuntimeUI/
        UXML/
        USS/
        Controls/
        Documents/
      HUD/
        Application/
        Presentation/
      UpgradeOverlay/
        Application/
        Presentation/
      Result/
        Application/
        Presentation/

  Scenes/
    Title.unity
    Match.unity
    Result.unity

  ScriptableObjects/
    Balance/
    UI/
    Configs/

  Tests/
    EditMode/
    PlayMode/
```

### 構造ルール

- まず feature を切る。
- feature の中で `Domain / Application / Infrastructure / Presentation` に分ける。
- 複数 feature で本当に安定利用されるものだけ `Shared/` に上げる。
- `Manager`, `Common`, `Util`, `Helper` を安易に増やさない。
- `GameManager` 1 個で全部持つ構造は禁止。

---

## 4. 依存方向

依存方向は必ず守る。

- `Domain`
  - 他レイヤーに依存しない。
  - 原則 Unity API に依存しない。
- `Application`
  - `Domain` に依存してよい。
  - 外部実装には依存しない。必要なら interface に依存する。
- `Infrastructure`
  - `Application` / `Domain` の interface 実装を置く。
  - Unity API、Addressables、保存、Input System、乱数供給、SE などを扱う。
- `Presentation`
  - `Application` の結果を描画する。
  - MonoBehaviour、UIDocument、VisualElement Adapter、Presenter Bridge を置く。
- `Bootstrap`
  - 配線だけを担当する。

禁止事項:

- `Domain -> Infrastructure / Presentation`
- `Application -> concrete implementation`
- 任意層 -> `LifetimeScope` 直接依存
- 任意層 -> `IObjectResolver` 直接依存
- static mutable state
- 「どこからでも触れるイベントバス」

---

## 5. このゲームで authoritative にするもの

このプロジェクトでは、以下を **authoritative model** として扱う。

- グリッド座標
- タイル状態
  - 通常
  - 崩落中
  - 崩落済み / 永久消滅
  - 炎上中
  - 壁
- プレイヤー状態
  - 位置
  - HP
  - コイン
  - 移動不能状態
  - 無敵状態
  - ボム性能
  - クールダウン
- スライム状態
- 強化候補 / 所持強化 / 適用済みビルド
- フェーズ状態
  - タイトル
  - 試合中
  - 縮小演出中
  - 強化中
  - リザルト

以下は **view-only** または **presentation-derived** とする。

- 放物線アニメーション
- カメラ追従
- 被弾フラッシュ
- ノックバック見た目
- UI 表示
- 演出タイムライン

**重要:**
衝突、範囲、崩落、炎、退避先探索、ステージ縮小、スライム湧き判定は、物理エンジンや見た目オブジェクトではなく、**グリッドロジック側で確定**させること。

---

## 6. まず作るべき境界

このゲームでは最初に以下の契約を固める。

### Shared Domain

- `PlayerId`
- `GridPos`
- `Direction8`
- `CardinalDirection4`
- `GamePhase`
- `MatchClock`
- `TileCoordRange`
- `BalanceValue`

### Stage Domain

- `StageModel`
- `TileState`
- `WallGenerationService`
- `StageShrinkService`
- `SafeTileSearchService`
- `StageQueryService`

### Player Domain

- `PlayerModel`
- `PlayerStats`
- `PlayerBuild`
- `InvulnerabilityState`
- `ForcedMoveState`

### Bombs Domain

- `BombSpec`
- `BombFlightCommand`
- `BombLandingResolver`
- `BombAreaResolver`
- `BreakBombResolver`
- `FireBombResolver`
- `BombCooldownState`

### Slimes Domain

- `SlimeModel`
- `SlimeSpawnService`
- `SlimeAiService`
- `SlimeDropResolver`

### Upgrades Domain

- `UpgradeDefinition`
- `UpgradeCatalog`
- `UpgradeDraftService`
- `UpgradeApplyService`
- `UpgradeRollRule`
- `UpgradeAvailabilityRule`

### MatchFlow Application

- `MatchFlowOrchestrator`
- `MatchPhaseScheduler`
- `UpgradePhaseUseCase`
- `MatchEndUseCase`

---

## 7. MatchFlow は単一オーケストレーターで持つ

このゲームでは、20 秒周期で以下が連動する。

- ステージ縮小
- 強化フェーズ開始
- ゲーム一時停止
- 両者の選択完了または 10 秒経過で再開

したがって、これらを別々の `MonoBehaviour.Update()` や別々のタイマーで管理してはいけない。

必ず **単一の phase scheduler / orchestrator** を中心に持つこと。

### ルール

- `MatchPhaseScheduler` が唯一の真実の時計を持つ。
- `UpgradePhaseUseCase` は scheduler からのみ起動する。
- `StageShrinkService` は scheduler からのみ起動する。
- UI は phase の通知を購読して表示を切り替える。
- 演出待機は UniTask で行う。
- phase の永続状態は Domain/Application 側で保持する。

禁止事項:

- HUD タイマーが独自に残り時間を進める
- ステージ縮小が独自タイマーを持つ
- Upgrade UI が自分で match resume を決定する

---

## 8. VContainer ルール

### 基本

- 依存登録は `LifetimeScope` または installer に限定する。
- constructor injection を第一選択にする。
- Lifetime は必ず明示する。
- `RegisterEntryPoint` を優先する。

### Scope 方針

#### ProjectLifetimeScope

置いてよいもの:

- 設定アセット読み込み
- グローバルログ
- セーブ / ロード
- 共通ファクトリ
- 共通設定
- アセットローダ
- Audio 設定などのアプリ横断サービス

#### MatchLifetimeScope

置くもの:

- MatchFlow
- Stage
- Player 状態
- Bombs
- Slimes
- Upgrade ドラフト
- HUD Presenter
- Upgrade Overlay Presenter
- Camera 制御
- Match Input Adapter

### Lifetime の目安

- `Singleton`
  - アプリ全体で 1 つでよい stateless service または設定系
- `Scoped`
  - Match 単位の状態やサービス
  - 基本はこれを第一候補にする
- `Transient`
  - 軽量で短命な helper
  - 乱用しない

### EntryPoint ルール

- `IStartable` / `IAsyncStartable` / `ITickable` / `IFixedTickable` を活用する。
- `IAsyncStartable` で初期化する処理は、開始順序を明示する。
- `Tickable` を増やしすぎず、phase 単位で責務をまとめる。

禁止事項:

- container を service locator として使う
- MonoBehaviour から Resolve する
- DI で循環依存をごまかす

---

## 9. UniTask ルール

- one-shot async は UniTask を使う。
- async メソッドは原則 `CancellationToken` を最後の引数で受ける。
- 受けた token は必ず下流へ渡す。
- fire-and-forget は原則禁止。
- `async void` は UI の入口以外で禁止。
- 演出待機、フェーズ待機、ロード待機は UniTask で記述する。

### このゲームで UniTask を使う場所

- シーン起動時の初期化
- フィールド生成の演出待機
- ボム着弾演出後の後処理
- ステージ縮小演出
- 強化フェーズの開始 / 終了待機
- リザルト遷移

### このゲームで UniTask を使いすぎない場所

- HP 表示更新
- クールダウン残り表示
- 選択中カード反映
- 進行中タイマー通知

これらは R3 側を優先する。

---

## 10. R3 ルール

- 長寿命の状態通知と UI 反映に使う。
- mutable な source は private に閉じる。
- 外部公開は read-only を優先する。
- 購読は必ず破棄タイミングを持つ。
- MonoBehaviour / UIDocument で開始した購読は destroy token に結びつける。

### このゲームで R3 を使うべき対象

- `PlayerModel.CurrentHp`
- `PlayerModel.Coin`
- `PlayerBuild`
- `BombCooldownState`
- `MatchPhase`
- `MatchClock.Remaining`
- `UpgradeDraft.CurrentChoices`
- `UpgradeOverlayState`
- `ResultState`

### このゲームで R3 を乱用しない対象

- 一回しか発生しないロード処理
- 単発の SE 再生
- 単発の SceneLoad
- その場限りの await チェーン

### 購読の方針

- `ReactiveProperty` は所有者を明確にする。
- UI に見せる公開面は `ReadOnlyReactiveProperty` など read-only 化を優先する。
- `EveryUpdate` を安易に使わず、既存イベントや phase 通知で表現できるならそちらを使う。
- フレーム単位観測が必要なときは、用途を明示する。

---

## 11. Unity 6.3 UI Toolkit ルール

Unity 6.3 の UI Toolkit はランタイム UI、Panel Settings、Input System、runtime data binding、ListView、複数パネルの Sort Order を前提に設計できる。だが、このプロジェクトでは **多機能だから全部使う** のではなく、**ゲームと相性が良い使い方だけを採用**する。

### 11.1 採用方針

- Runtime UI は UI Toolkit を第一選択にする。
- Render Mode は原則 `Screen Space Overlay`。
- パネルはむやみに増やさない。
- UI は **1つのルート HUD/Overlay パネル**を基本とし、必要なときだけ補助パネルを使う。
- UI Builder を使って UXML / USS を設計する。
- C# は binding / event hookup / presenter bridge に集中させる。

### 11.2 このプロジェクトの推奨 UI 構成

原則として、**Match 画面では 1 枚のフルスクリーン UIDocument** を使い、全フェーズで **プレイヤー独立のペイン構造** を維持する。

#### プレイヤー独立の原則

- **全フェーズ（HUD / 強化 / リザルト）でプレイヤーごとに完結した UI ユニットを使う。**
- 共有の中央タイマーやグローバルなカウントダウンは置かない。
- タイマー、HP、コイン、カード選択、勝敗表示はすべて各プレイヤーのペイン内に含める。
- この設計により、1 人プレイ・2 人プレイ・将来の N 人対応でも同一テンプレートを繰り返すだけで済む。

#### ルート構造

```text
MatchRoot (.match-root)
  MatchLayer (row)
    LeftHudRoot (.hud-root.hud-root--p1)
      PlayerHud (template instance)  ← タイマー・HP・コイン・CD・取得済み強化
    MatchDivider
    RightHudRoot (.hud-root.hud-root--p2)
      PlayerHud (template instance)
  OverlayLayer (absolute)
    UpgradeOverlayRoot
      UpgradePanes (row)
        LeftUpgradePane   ← タイトル・カウントダウン・カード3枚・リロールボタン・ステータス
        UpgradeDivider
        RightUpgradePane
    ResultRoot
      ResultPanes (row)
        LeftResultPane    ← 勝敗表示・ボタン
        ResultDivider
        RightResultPane   ← 勝敗表示
```

### 11.3 この方針を採る理由

- ゲームは 2 カメラ分割だが、HUD と強化 UI は画面座標系の情報である。
- **各プレイヤーが自分の画面領域で情報を完結して確認できる**ことが対戦ゲームでは重要。
- プレイヤー数の変更に対して、ペインの追加/削除だけで対応できるスケーラブルな構造。
- パネルを増やしすぎると focus、navigation、sort order、入力配送の複雑さが増える。

したがって、**複数パネル前提で始めず、単一ルートパネル + プレイヤー独立ペインで構成する**こと。

例外:

- デバッグ HUD
- 開発専用 overlay
- ワールドスペース UI が本当に必要な特殊画面

### 11.4 デザイン方針

#### ビジュアルトーン

- **ファンタジーアーケード**: 中世ファンタジーの騎士 × ポップな対戦ゲーム。ダークファンタジーではなく明るく楽しいトーン。
- 暗色半透明パネル（`rgba(20, 15, 30, 0.75)`）をベースに、暖色系アクセントで情報を際立たせる。
- 数字のむき出し表示は避け、バッジ・コンテナ・アイコンラップで装飾する。

#### 配色

| 用途 | カラー |
|------|--------|
| 背景パネル | `rgba(20, 15, 30, 0.75)` |
| テキスト | `#F0E8D8`（暖かい白） |
| 補助テキスト | `#A09888` |
| P1 アクセント | `#4A90D9`（青） |
| P2 アクセント | `#D94A4A`（赤） |
| 炎ボム系 | `#F08030`（オレンジ） |
| ブレークボム系 | `#3080C0`（ブルー） |
| 汎用強化 | `#40A050`（グリーン） |
| コイン | `#F0C040`（金） |
| HP | `#E04040`（赤） |
| 購入不可 | `#606060`（グレー） |

#### USS 変数

- CSS カスタムプロパティは `.match-root` セレクターで定義する（`:root` ではなく）。
- これにより UXML テンプレートインスタンス内の子要素にも確実に変数が継承される。
- 定義は `Variables.uss` に集約し、他の USS は `var()` で参照する。

#### 強化カード

- カテゴリ色帯（上部 6px）で炎/ブレーク/汎用を色分け。
- テキスト主体。アイコンは将来追加可能だが必須ではない。
- 状態: `.card--selected`（金ボーダー + スケール）、`.card--locked`（グレーアウト）、`.card--done`（半透明）。

### 11.5 UXML / USS / C# の責務分離

#### UXML

- 静的レイアウト
- VisualTree の構造
- 名前付き要素
- 再利用テンプレート

#### USS

- 色、余白、フォント、境界、アニメーション用 class
- 状態 class の見た目差分
- プレイヤー左右差分の軽い表現

#### C#

- VisualElement 参照解決
- バインディング
- イベント購読
- focus 制御
- Presenter から View への反映

禁止事項:

- UXML に仕様ロジックを埋め込む
- MonoBehaviour で USS 値を大量に都度書き換える
- UI のためだけに Domain モデルを汚す

### 11.6 UI Toolkit の命名規則

- UXML: `PascalCase.uxml`
- USS: `PascalCase.uss`
- VisualElement wrapper: `PascalCaseView.cs`
- 再利用コントロール: `PascalCaseElement.cs`
- 例:
  - `MatchHud.uxml`
  - `UpgradeOverlay.uxml`
  - `UpgradeCard.uxml`
  - `MatchHudView.cs`
  - `UpgradeCardElement.cs`

### 11.7 VisualElement の再利用

- 繰り返し UI は VisualTreeAsset テンプレート化する。
- 「強化カード 3 枚」は固定数でもテンプレート再利用でよい。
- カード UI が複雑化するなら custom `VisualElement` を作る。
- ただし 3 枚しかない UI に ListView を無理に使わない。

### 11.8 Runtime Binding の使い分け

Runtime data binding は使ってよいが、全面採用しない。

使ってよい対象:

- タイトル画面の設定項目
- リザルト画面の一覧
- 強化履歴リスト
- デバッグパネル
- 比較的低頻度で変化するデータ一覧

Presenter の明示更新を優先する対象:

- 残り時間の秒表示
- HP
- コイン
- クールダウンゲージ
- 選択中カードハイライト
- リアルタイム対戦中の頻繁更新 HUD

理由:

- runtime binding は便利だが、対戦中の高速 HUD では「どこがいつ更新されるか」を explicit に保った方が追いやすい。
- 一方、List や設定系 UI では binding の恩恵が大きい。

### 11.9 入力とフォーカス

- 入力は Input System 前提。
- UI Toolkit は Input System の UI actions からイベントを生成できる。
- マウス依存前提にしない。
- オーバーレイを開いたら **初期フォーカスを必ず明示**する。
- 左右プレイヤーの選択 UI は、フォーカス領域が交差しないように設計する。
- 強化フェーズ中は gameplay input と UI input の責務を明確に切り替える。

### 11.10 このゲームにおける UI 入力ルール

- 試合中:
  - gameplay action map を有効
  - UI action map は必要最小限
- 強化フェーズ中:
  - gameplay action を凍結
  - 左右プレイヤーの選択入力のみ許可
  - 決定 / スキップ / リロールのみ有効
- リザルト:
  - rematch / back のみ有効

### 11.11 UI の状態表現

- UI 開閉は `display` / class 切り替えで制御する。
- 選択状態は USS class で表現する。
- class 切り替えで済むものを inline style で毎回書き換えない。
- 要素移動が必要なら layout を組み替える前に `style.translate` を検討する。

### 11.12 Panel Settings ルール

- 共通テーマは `Theme Style Sheet` で与える。
- 画面解像度差に備え、基本は `Scale with Screen Size` を使う。
- 複数パネルを使う場合は `Sort Order` を明示する。
- `Target Texture` を使う特殊構成は、必要性が明確なときだけ採用する。

### 11.13 UI Toolkit で避けること

- uGUI と混在させて責務があいまいになること
- 各 feature が勝手に UIDocument を増やすこと
- 画面ごとに似た USS をコピペすること
- プレイヤー 1 / 2 で別実装の HUD を作ること
- `Q<VisualElement>("...")` を各所に散らすこと

---

## 12. Input System ルール

- Input System を前提とする。
- action map は最低でも以下で分ける。

```text
Gameplay
  Move
  BreakBombHold
  FireBombHold

UpgradeUI_P1
  Navigate
  Submit
  Skip
  Reroll

UpgradeUI_P2
  Navigate
  Submit
  Skip
  Reroll

System
  Pause
  Confirm
  Cancel
```

### ルール

- 「試合操作」と「UI 操作」を同一責務のスクリプトに混ぜない。
- Input callback ではルール判定しない。
- callback は command 化して Application へ渡す。
- 押しっぱなしボムは `started / canceled` の意味を明示して扱う。
- ボム飛行方向は「最後に入力した方向」を authoritative state として明示保持する。

---

## 13. グリッド / 進行ロジックの実装ルール

### 13.1 グリッド優先

- 座標は grid を真実にする。
- 見た目 Transform は grid から導出する。
- 斜め 8 方向も grid で扱う。
- 距離計算は「仕様上の距離」を明示する。

### 13.2 壁配置

- 壁生成は deterministic service 化する。
- `seed pass` と `growth pass` を分ける。
- スポーン保護 5x5 は専用ルールとして切り出す。
- 生成結果の妥当性チェックを持つ。

### 13.3 ボム

- 飛行中の見た目は presentation。
- 着弾判定は grid 側で決める。
- 「壁に当たった時点で着弾」「効果範囲の壁貫通有無」は bomb resolver が決める。
- ブレークボムと炎ボムの差分は spec class / strategy で表現する。
- 共有ロジックを base class に寄せすぎない。

### 13.4 崩落・炎

- タイル状態の変化は stage model に集約する。
- Damage と tile state 更新を別々にばらさない。
- 崩落終了後の復帰タイミングは stage service が一元管理する。
- 永久消滅タイルと一時崩落タイルは別概念として持つ。

### 13.5 退避先探索

- 3x3 優先探索と BFS フォールバックを別関数に分ける。
- 「安全マス」の定義を 1 箇所にまとめる。
- プレイヤーとスライムで共有可能なら Query を共通化する。

### 13.6 スライム

- AI は高級な常時追跡でなく、仕様に必要な単純さを保つ。
- スポーン目標数は `current alive tiles * 3%` を単一 service で計算する。
- ドロップ判定は death resolver に集約する。
- 赤色スライムの強化即時付与は Upgrade feature へ委譲する。

---

## 14. 強化システムの実装ルール

このゲームでは強化システムが中核なので、設計を軽視しない。

### 14.1 強化定義

強化は data-driven にする。

推奨:

- `UpgradeDefinition` は ScriptableObject または静的 catalog で管理
- `UpgradeId` を必ず持つ
- cost / stackability / max stack / availability rule を明示する

### 14.2 強化適用

- 強化適用は `UpgradeApplyService` に集約する。
- `switch` が膨れ上がるなら `IUpgradeEffectApplier` 群へ分割する。
- ただし interface の分けすぎに注意する。

### 14.3 候補生成

- 候補生成は「引ける候補集合」を先に作り、その後ランダム抽選する。
- 出現不可条件を UI 側で弾かない。
- `HP5以下の時のみ出現` のような条件は domain rule として持つ。
- 「無制限取得可能な強化」集合を 1 箇所で定義する。

### 14.4 UI

- Upgrade UI は左右プレイヤーで同一テンプレートを使う。
- 選択可 / 不可 / 選択済み / 時間切れ / 完了待ち を視覚状態として分離する。
- コイン不足のグレーアウトは UI 表示と選択不可ロジックを一致させる。

---

## 15. 命名規則

### 良い命名

- `MatchPhaseScheduler`
- `StageShrinkService`
- `SafeTileSearchService`
- `UpgradeDraftService`
- `BombLandingResolver`
- `PlayerHudPresenter`
- `UpgradeOverlayView`
- `MatchResultUseCase`

### 避ける命名

- `GameManager`
- `UIManager`
- `BattleController`
- `CommonUtil`
- `HelperService`
- `DataHolder`

### インターフェース方針

- 差し替え境界があるときだけ interface を作る。
- 実装 1 個、差し替え予定なし、テストも fake 不要なら無理に interface を作らない。

---

## 16. asmdef ルール

- asmdef は feature ごとに切る。
- layer の依存方向を asmdef でも守る。
- UI Toolkit 用 asmdef と Gameplay 用 asmdef を必要に応じて分ける。
- Tests asmdef は対象 feature のみ参照する。
- 循環参照は禁止。

推奨例:

```text
App.Shared
App.MatchFlow
App.Stage
App.Player
App.Bombs
App.Slimes
App.Upgrades
App.Input
App.UI
App.Bootstrap
App.Tests.EditMode
App.Tests.PlayMode
```

---

## 17. テスト方針

優先順位:

1. Domain unit test
2. Application unit test
3. Infrastructure integration test
4. Presentation の必要最小限 test

### このゲームで優先的にテストするもの

- 壁生成率とスポーン保護
- ボム着弾解決
- 効果範囲解決
- 炎 / 崩落ダメージ
- 退避先探索
- スライム湧き数計算
- 強化候補生成条件
- クールダウン下限
- フェーズ遷移
- ステージ縮小後のサイズ変化

### R3 / 時間依存のテスト

- 時間依存は provider を差し替え可能にする。
- フレーム依存は抽象化する。
- 購読ベースのロジックは observable behavior をテストする。

### バグ修正時

- 可能なら先に再現テストを書く。
- 再現できないときも、最低限仕様ケースを追加する。

---

## 18. 変更手順

変更時は必ずこの順で考える。

1. 仕様上どの feature の変更か
2. どの layer の責務か
3. authoritative state はどこか
4. phase 制御への影響はあるか
5. UI Toolkit 側は表示だけで済むか
6. async の token 伝播は正しいか
7. R3 の購読解除漏れはないか
8. テストを追加 / 更新すべきか

### 複数層にまたがるとき

- 先に Domain/Application の契約を固める
- 次に Infrastructure / Presentation を合わせる
- 最後に LifetimeScope を更新する

---

## 19. レビュー用チェックリスト

- 仕様書の該当ルールと一致しているか
- 20 秒周期の処理が単一 scheduler に集約されているか
- Domain / Application に Unity 依存が漏れていないか
- MonoBehaviour が太っていないか
- UI Toolkit の責務が UI に限定されているか
- gameplay input と UI input が分離されているか
- async に `CancellationToken` が通っているか
- fire-and-forget が紛れていないか
- R3 購読の解放漏れがないか
- 複数パネルや複数 UIDocument を不必要に増やしていないか
- 名前で責務が分かるか
- テストが足りているか

---

## 20. このプロジェクトで避けること

- なんでも MonoBehaviour に書く
- なんでも R3 にする
- なんでも UniTask にする
- なんでも singleton にする
- なんでも interface にする
- なんでも Shared に上げる
- なんでも UI Toolkit binding にする
- 2プレイヤー UI を別実装にする
- 仕様時間を UI 側タイマーに持たせる
- 物理挙動を authoritative にする
- `GameManager` 1 個で押し切る

---

## 21. 実装優先順

新規に組み始める場合は、以下の順を推奨する。

1. Shared primitives
2. Stage domain
3. Player domain
4. Bomb resolvers
5. Slime spawn / AI
6. Upgrade domain
7. Match phase scheduler
8. Input adapters
9. UI Toolkit HUD / overlay
10. Camera / polish / FX

---

## 22. コンパイル・テスト・結果確認フロー

コード変更後は、**必ず以下のフローを順に実行**し、全件グリーンを確認してから commit / push / PR 作成に進むこと。

### Step 1: コンパイル確認

Unity MCP 経由で `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)` を実行し、コンパイルエラーがないことを確認する。

```csharp
// Unity MCP RunCommand
AssetDatabase.ImportAsset("Assets/App/...", ImportAssetOptions.ForceUpdate);
AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
```

- `GetConsoleLogs(logTypes: "error")` でコンパイルエラーが 0 件であることを確認する。
- **注意**: 単に `AssetDatabase.Refresh()` だけでは再コンパイルがトリガーされない場合がある。変更ファイルに対して `ImportAsset(..., ForceUpdate)` を明示的に呼ぶこと。

### Step 2: テスト実行

```csharp
// Unity MCP RunCommand
var api = ScriptableObject.CreateInstance<TestRunnerApi>();
var filter = new Filter {
    testMode = TestMode.EditMode,
    assemblyNames = new[] { "App.Tests.EditMode" }
};
api.Execute(new ExecutionSettings(filter));
```

### Step 3: テスト結果の確認

テスト結果は以下のファイルに XML 形式で出力される:

```
C:/Users/herring/AppData/LocalLow/OUCC/SpringGame_teamA/TestResults.xml
```

このファイルを読み取り、`result="Failed"` を検索して失敗テストがないことを確認する。

```
// 確認コマンド例（Grep ツール）
pattern: test-run.*result=
→ result="Passed" total="N" passed="N" failed="0" であること
```

- `failed="0"` であれば全件グリーン。
- `failed` が 1 以上の場合、`result="Failed"` の `test-case` 要素を特定し、`<message>` と `<stack-trace>` からエラー内容を読み取って修正する。
- **テスト実行は非同期**なので、実行開始後 5〜10 秒待ってから結果ファイルを確認すること。`start-time` のタイムスタンプが更新されていることで新しい結果であることを検証する。

### Step 4: commit / push / PR

上記 Step 1〜3 で全件グリーンを確認した後にのみ、commit → push → PR 作成に進む。

### ビルド

- CI 未設定。ローカルビルドは Unity Editor の `File > Build Settings` から実行。

---

## 23. テスト完了の必須確認

PR を作成する前に、**必ず §22 のフローで全ての EditMode テストが通ることを確認**すること。

- 1 件でも失敗がある場合は、修正してから再実行し、全件グリーンを確認するまで PR を作成しない。
- **TestResults.xml を実際に読み取って `failed="0"` を目視確認**すること。MCP の RunCommand 手動テストだけでは NUnit テストの合否を代替できない。
- テスト結果ファイルの `start-time` が最新の実行であることを必ず検証する（古い結果を読んでいないか確認）。

---

## 24. 最終ルール

- 仕様変更をしない。
- 仕様を実装都合でねじ曲げない。
- まず責務を正しい場所に置く。
- その後で最小限の実装を行う。
- 曖昧なら「grid authoritative」「phase authoritative」「UI は薄く」の三原則に戻る。
