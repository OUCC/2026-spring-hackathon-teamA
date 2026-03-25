# FLOOR BREAKER — アーキテクチャ概要

## 全体構造

```mermaid
graph TB
    subgraph Bootstrap
        PLS[ProjectLifetimeScope]
        MLS[MatchLifetimeScope]
    end

    subgraph Shared
        SD[Domain]
        SA[Application]
        SI[Infrastructure]
        SP[Presentation]
    end

    subgraph Features
        Stage
        Player
        Bombs
        Slimes
        Upgrades
        MatchFlow
        Input
        UI
        Cameras
    end

    subgraph ScriptableObjects
        SO[BalanceConfig]
    end

    Bootstrap --> Features
    Bootstrap --> Shared
    Bootstrap --> ScriptableObjects
    Features --> Shared
    ScriptableObjects --> SA
    ScriptableObjects --> SD
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
    SD["App.Shared.Domain ✓"]
    SA["App.Shared.Application ✓"]
    SI[App.Shared.Infrastructure]
    SP[App.Shared.Presentation]
    AST["App.Stage ✓"]
    APL["App.Player ✓"]
    ABM["App.Bombs ✓ 予定"]
    ASL["App.Slimes 予定"]
    AUP["App.Upgrades 予定"]
    AMF["App.MatchFlow 予定"]
    AIN["App.Input 予定"]
    AUI["App.UI 予定"]
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
```

> 緑 = `noEngineReferences: true` (pure C# Domain)

## レイヤー構造

```mermaid
graph TB
    subgraph layers["依存方向 (上→下のみ)"]
        Pres[Presentation]
        Infra[Infrastructure]
        App[Application]
        Dom[Domain]
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

### Bombs (Phase 4 予定)

```mermaid
classDiagram
    class BombSpec {
        <<readonly struct>>
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
    class BombLandingResolver {
        +Resolve(cmd, stage, entityQuery) GridPos
    }
    class BombAreaResolver {
        +Resolve(center, range, penetrate, stage) IReadOnlyList~GridPos~
    }
    class FallBombResolver {
        +Execute(landingPos, spec, stage, timerService)
    }
    class FireBombResolver {
        +Execute(landingPos, spec, stage, timerService)
    }
    class BombCooldownState {
        +ReactiveProperty~float~ FallRemaining
        +ReactiveProperty~float~ FireRemaining
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
        TV[TileView]
        HUD_T[HUD Timer]
        HUD_HP[HUD HP Bar]
        HUD_C[HUD Coins]
        PV[PlayerView]
        TVFX[Tile VFX]
        OV[Overlay UI]
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
| 4 | ボム | BombSpec 等 6 クラス | BombLaunchUseCase | — | **次** |
| 5 | スライム | SlimeModel 等 4 クラス | SlimeTickService | — | 未着手 |
| 6 | 強化 | UpgradeDef 等 6 クラス | UpgradeApplyService | — | 未着手 |
| 7 | マッチフロー | — | Orchestrator, Scheduler | — | 未着手 |
| 8 | 入力 | — | InputBridge | InputAdapter | 未着手 |
| 9 | UI | — | — | UXML/USS/Presenter | 未着手 |
| 10-14 | Presentation | — | — | View, VFX, Camera | 未着手 |
| 15 | Bootstrap | — | — | LifetimeScope | 未着手 |
| 16-18 | 統合/ポリッシュ | — | — | テスト, FX, SE | 未着手 |
