# FLOOR BREAKER — アーキテクチャ概要

## 全体構造

```mermaid
flowchart TB
    subgraph BS["Bootstrap / VContainer"]
        PLS[ProjectLifetimeScope]
        MLS[MatchLifetimeScope]
    end

    subgraph SH["Shared"]
        SH_D[Domain]
        SH_A[Application]
        SH_I[Infrastructure]
        SH_P[Presentation]
    end

    subgraph FT["Features"]
        FT_ST[Stage]
        FT_PL[Player]
        FT_BM[Bombs]
        FT_SL[Slimes]
        FT_UP[Upgrades]
        FT_MF[MatchFlow]
        FT_IN["Input System"]
        FT_UI["UI Toolkit"]
        FT_CM[Cameras]
    end

    SOB["BalanceConfig / ScriptableObject"]

    BS --> FT
    BS --> SH
    BS --> SOB
    FT --> SH
    SOB --> SH_A
    SOB --> SH_D
```

### Shared 内容

| レイヤー | クラス |
|---|---|
| Domain | GridPos, Direction8, CardinalDirection4, TileCoordRange, PlayerId, Float2, UpgradeId, GamePhase, MatchClock |
| Application | IBalanceParameters, ITimeProvider, IRandomProvider, IAudioService |
| Infrastructure | UnityTimeProvider, SeededRandomProvider |
| Presentation | Float2Extensions |

## アセンブリ依存グラフ

```mermaid
graph LR
    SD["App.Shared.Domain<br/>noEngine ✓"]
    SA["App.Shared.Application<br/>noEngine ✓"]
    SI[App.Shared.Infrastructure]
    SP[App.Shared.Presentation]
    AST["App.Stage<br/>noEngine ✓"]
    APL["App.Player<br/>noEngine ✓"]
    ABM["App.Bombs<br/>noEngine ✓"]
    ASL["App.Slimes<br/>noEngine ✓"]
    AUP["App.Upgrades<br/>noEngine ✓"]
    AMF["App.MatchFlow<br/>noEngine ✓"]
    AIN["App.Input"]
    AUI["App.UI<br/>予定"]
    ASO[App.ScriptableObjects]
    ABT[App.Bootstrap]

    SA --> SD
    SI --> SD
    SI --> SA
    SP --> SD
    AST --> SD
    AST --> SA
    APL --> SD
    APL --> SA
    APL --> AST
    ABM --> SD
    ABM --> SA
    ABM --> AST
    ABM --> APL
    ASL --> SD
    ASL --> AST
    ASL --> APL
    AUP --> SD
    AUP --> APL
    AMF --> SD
    AMF --> AST
    AMF --> APL
    AMF --> ABM
    AMF --> ASL
    AMF --> AUP
    ASO --> SD
    ASO --> SA
    ABT --> AMF
    ABT --> ASO

    style SD fill:#e8f5e9
    style SA fill:#e8f5e9
    style AST fill:#e8f5e9
    style APL fill:#e8f5e9
    style ABM fill:#e8f5e9
    style ASL fill:#e8f5e9
    style AUP fill:#e8f5e9
    style AMF fill:#e8f5e9
```

> 緑 = `noEngineReferences: true` (pure C# Domain)

## レイヤー構造

```mermaid
graph TB
    subgraph layers["依存方向 (上→下のみ)"]
        Pres["Presentation<br/>MonoBehaviour, UIDocument<br/>SpriteRenderer, VFX, DOTween"]
        Infra["Infrastructure<br/>Unity API, Input System<br/>AudioService, 保存"]
        App["Application<br/>UseCase, Orchestrator<br/>Presenter Bridge"]
        Dom["Domain<br/>Model, Service, Resolver<br/>pure C#, R3 ReactiveProperty"]
    end

    Pres --> App
    Pres --> Infra
    Infra --> App
    Infra --> Dom
    App --> Dom

    style Dom fill:#e8f5e9
    style App fill:#fff3e0
    style Infra fill:#e3f2fd
    style Pres fill:#fce4ec
```

| レイヤー | 責務 | 例 |
|---|---|---|
| Domain | ゲームルール、モデル、サービス (pure C#, R3) | StageModel, PlayerBuild, BombSpec |
| Application | ユースケース、オーケストレーション | BombLaunchUseCase, PlayerMoveService |
| Infrastructure | Unity API ラッパー、外部実装 | UnityTimeProvider, InputAdapter, AudioService |
| Presentation | MonoBehaviour, UI, VFX, アニメーション | TileView, PlayerView, HUD Presenter |

## アーキテクチャ原則

### 時間管理の単一化

全ての時間進行は **MatchPhaseScheduler (Phase 7)** が唯一のドライバーとなる。

```mermaid
flowchart TD
    MPS[MatchPhaseScheduler] -->|Tick dt| MC[MatchClock]
    MPS -->|Tick dt| TTS[TileTimerService]
    MPS -->|Tick dt| BCS[BombCooldownState]
    MPS -->|Tick dt| INV[InvulnerabilityState x2]
    MPS -->|Tick dt| FM[ForcedMoveState x2]
    MPS -->|Tick dt| STS[SlimeTickService]
```

- 各サービスは自分でタイマーを持たず、外部から `Tick(float dt)` を受ける
- `MatchPhaseScheduler` が一時停止すると全ての Tick が止まる
- 独自の `Update()` や `InvokeRepeating` でタイマーを回すことを禁止

### Domain の公開面は read-only

Domain の `ReactiveProperty<T>` は private に閉じ、外部には `ReadOnlyReactiveProperty<T>` を公開する。

```
// 内部
private readonly ReactiveProperty<int> _currentHp;

// 公開
public ReadOnlyReactiveProperty<int> CurrentHp => _currentHp;
```

**適用済みの箇所**: StageModel.TileChanged, MatchClock.Remaining/CurrentPhase/IsPaused, PlayerStats.CurrentHp/Coins, PlayerModel.Position/FacingDirection, TileTimerService.TimerCompleted

**原則**: 状態を変更できるのは所有者のメソッドのみ。外部は購読だけ行う。

### UI Toolkit ルート戦略

Match 画面では **1 枚のフルスクリーン UIDocument** を使い、内部を領域分割する。

```mermaid
flowchart TB
    subgraph UIDocument
        subgraph TopLayer
            Announce[SharedAnnouncements]
        end
        subgraph MatchLayer
            LeftHUD[LeftHudRoot]
            Timer[CenterPhaseTimer]
            RightHUD[RightHudRoot]
        end
        subgraph OverlayLayer
            UpgradeOV[UpgradeOverlayRoot]
            PauseOV[PauseRoot]
            ResultOV[ResultRoot]
        end
    end
```

- パネルを増やさない（focus/navigation/sort order の複雑化回避）
- 左右 HUD は同一テンプレートを使う（P1/P2 で別実装にしない）
- オーバーレイの開閉は `display` / USS class 切り替えで制御
- 強化フェーズ中は gameplay input を凍結し、UI input のみ許可

---

## Feature 別クラス構成

### Stage

```mermaid
classDiagram
    class StageModel {
        -TileState[,] _tiles
        -Subject~TileChangedEvent~ _tileChanged
        +StageBounds Bounds
        +GetTileState(GridPos) TileState
        +SetTileState(GridPos, TileState)
        +IsPassable(GridPos) bool
        +GetAliveTileCount() int
    }
    class StageBounds {
        +TileCoordRange Current
        +Shrink()
        +GetOuterRing() IReadOnlyList~GridPos~
    }
    class WallGenerationService {
        +Generate(bounds, p1, p2, random) HashSet~GridPos~
    }
    class StageQueryService {
        +GetPassableTiles() IReadOnlyList~GridPos~
        +GetTilesInCross(center, range, penetrate) IReadOnlyList~GridPos~
        +RaycastGrid(from, dir, maxDist) RaycastResult?
    }
    class SafeTileSearchService {
        +FindSafeTile(model, from, occupied) GridPos?
    }
    class StageShrinkService {
        +ShrinkOuterRing(model) IReadOnlyList~GridPos~
    }
    class TileTimerService {
        -Dictionary~GridPos,TileTimerEntry~ _activeTimers
        +StartCollapseTimer(pos, collapse, recovery)
        +StartFireTimer(pos, duration)
        +Tick(dt)
        +TimerCompleted Observable
    }

    StageModel *-- StageBounds
    StageQueryService --> StageModel
    SafeTileSearchService --> StageModel
    StageShrinkService --> StageModel
    TileTimerService --> StageModel
```

### Player

```mermaid
classDiagram
    class PlayerModel {
        +PlayerId Id
        +PlayerStats Stats
        +PlayerBuild Build
        +InvulnerabilityState Invulnerability
        +ForcedMoveState ForcedMove
        +ReactiveProperty~GridPos~ Position
        +ReactiveProperty~Direction8~ FacingDirection
    }
    class PlayerStats {
        +ReactiveProperty~int~ CurrentHp
        +ReactiveProperty~int~ Coins
        +float MoveSpeed
        +TakeDamage(int)
        +Heal(int)
        +SpendCoins(int) bool
    }
    class PlayerBuild {
        +Fire/Fall ボム各パラメータ
        +ApplyUpgrade(UpgradeId)
    }
    class PlayerMoveService {
        +TryMove(player, dir, stage) bool
    }
    class PlayerDamageService {
        +ApplyDamage(player, dmg, relocate, stage, safeSearch, occupied) bool
    }

    PlayerModel *-- PlayerStats
    PlayerModel *-- PlayerBuild
    PlayerModel *-- InvulnerabilityState
    PlayerModel *-- ForcedMoveState
    PlayerMoveService --> PlayerModel
    PlayerMoveService --> StageModel
    PlayerDamageService --> PlayerModel
    PlayerDamageService --> SafeTileSearchService
```

### Bombs

```mermaid
classDiagram
    class BombSpec {
        <<readonly struct>>
        +BombType Type
        +int MaxFlightDistance
        +int EffectRange
        +int Damage
        +float Cooldown
        +bool HasFlightDamage
        +bool WallPenetration
        +float Duration
        +float CollapseTime
        +float RecoveryTime
    }
    class BombFlightCommand {
        <<readonly struct>>
        +GridPos Origin
        +Direction8 Direction
        +BombSpec Spec
        +PlayerId Owner
    }
    class BombLandingResolver {
        +Resolve(cmd, actualDist, isEntityAt) GridPos
    }
    class BombAreaResolver {
        +Resolve(center, range, penetrateWalls) IReadOnlyList~GridPos~
    }
    class FallBombResolver {
        +Resolve(landingPos, spec, stage) FallBombResult
    }
    class FireBombResolver {
        +Resolve(landingPos, spec, stage) FireBombResult
    }
    class BombCooldownState {
        +ReadOnlyReactiveProperty~float~ FallBombRemaining
        +ReadOnlyReactiveProperty~float~ FireBombRemaining
        +StartCooldown(type, duration)
        +CanFire(type) bool
    }
    class BombLaunchUseCase {
        +Launch(player, bombType, stage)
    }

    BombLaunchUseCase --> BombLandingResolver
    BombLaunchUseCase --> FallBombResolver
    BombLaunchUseCase --> FireBombResolver
    BombLaunchUseCase --> BombCooldownState
    FallBombResolver --> BombAreaResolver
    FireBombResolver --> BombAreaResolver
    BombAreaResolver --> StageQueryService
```

## マッチフロー ライフサイクル

```mermaid
sequenceDiagram
    participant MFO as MatchFlowOrchestrator
    participant MPS as MatchPhaseScheduler
    participant SS as StageShrinkService
    participant UP as UpgradePhaseUseCase
    participant MC as MatchClock

    MFO->>MFO: 壁生成 + StageModel 初期化
    MFO->>MFO: Player x2 スポーン
    MFO->>MPS: 開始

    loop 20秒ごと
        MPS->>MC: SetPhase(StageShrink)
        MPS->>SS: ShrinkOuterRing()
        Note over SS: 外周 PermanentlyDestroyed
        MPS->>MC: SetPhase(UpgradePhase)
        MPS->>UP: RunAsync(ct)
        Note over UP: 両者選択 or 10秒タイムアウト
        MPS->>MC: SetPhase(MatchRunning)
        MPS->>MC: ResetTimer()
    end

    Note over MPS: HP 0 検出
    MPS->>MC: SetPhase(Result)
```

## R3 Observable データフロー

```mermaid
flowchart LR
    subgraph Domain
        SM_TC[StageModel.TileChanged]
        MC_R[MatchClock.Remaining]
        MC_P[MatchClock.CurrentPhase]
        PS_HP[PlayerStats.CurrentHp]
        PS_C[PlayerStats.Coins]
        PM_Pos[PlayerModel.Position]
        TTS[TileTimerService.TimerCompleted]
    end

    subgraph Presentation
        TV["TileView<br/>スプライト切替"]
        HUD_T[HUD Timer]
        HUD_HP[HUD HP Bar]
        HUD_C[HUD Coins]
        PV["PlayerView<br/>移動アニメ"]
        TVFX["Tile VFX<br/>崩落/炎エフェクト"]
        OV["Overlay<br/>強化UI/リザルト"]
    end

    SM_TC --> TV
    SM_TC --> TVFX
    MC_R --> HUD_T
    MC_P --> OV
    PS_HP --> HUD_HP
    PS_C --> HUD_C
    PM_Pos --> PV
    TTS --> TVFX
```

## 実装進捗

| Phase | 内容 | Domain | Application | Infra/Pres | 状態 |
|---|---|---|---|---|---|
| 0 | 基盤 | — | — | asmdef, シーン, SO | **完了** |
| 1 | 共通プリミティブ | GridPos 等 | Interfaces | TimeProvider 等 | **完了** |
| 2 | ステージ | StageModel 等 7 クラス | — | — | **完了** |
| 3 | プレイヤー | PlayerModel 等 5 クラス | MoveService, DamageService | — | **完了** |
| 4 | ボム | BombSpec 等 8 クラス | BombLaunchUseCase | — | **完了** |
| 5 | スライム | SlimeModel 等 7 クラス | SlimeTickService | — | **完了** |
| 6 | 強化 | UpgradeDef 等 7 クラス | UpgradeApplyService | — | **完了** |
| 7 | マッチフロー | — | Scheduler, FireDmg 等 6 クラス | — | **完了** |
| 8 | 入力 | BombHoldCommand, BombFlightTracker | InputBridge | InputAdapter | **完了** |
| 8.5 | Phase 9 前提修正 | AcquiredUpgrades, Winner Observable | RemainingTime Observable | App.UI.asmdef | **完了** |
| 9 | UI | UpgradeSelectionState | — | UXML/USS/Presenter/View 10クラス | **完了** |
| 10 | ステージ Presentation | — | — | TileView, AnimService, VfxPool, Presenter 8クラス | **完了** |
| 11 | プレイヤー Presentation | — | — | PlayerView, AnimService, Presenter, Factory 7クラス | **完了** |
| 12-14 | Bomb/Slime/Camera Pres | — | — | View, VFX, Camera | 未着手 |
| 15 | Bootstrap | — | — | LifetimeScope | 未着手 |
| 16-18 | 統合/ポリッシュ | — | — | テスト, FX, SE | 未着手 |
