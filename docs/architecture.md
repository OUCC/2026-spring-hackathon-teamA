# FLOOR BREAKER — アーキテクチャ概要

## 全体構造

```mermaid
graph TB
    subgraph Bootstrap["Bootstrap (VContainer)"]
        PLS[ProjectLifetimeScope]
        MLS[MatchLifetimeScope]
    end

    subgraph Shared["Shared"]
        SD[Domain<br/>GridPos, Direction8, PlayerId<br/>GamePhase, MatchClock<br/>Float2, TileCoordRange<br/>UpgradeId]
        SA[Application<br/>IBalanceParameters<br/>ITimeProvider<br/>IRandomProvider<br/>IAudioService]
        SI[Infrastructure<br/>UnityTimeProvider<br/>SeededRandomProvider]
        SP[Presentation<br/>Float2Extensions]
    end

    subgraph Features["Features"]
        Stage[Stage]
        Player[Player]
        Bombs[Bombs]
        Slimes[Slimes]
        Upgrades[Upgrades]
        MatchFlow[MatchFlow]
        Input[Input]
        UI[UI]
        Cameras[Cameras]
    end

    subgraph External["ScriptableObjects"]
        SO[BalanceConfig]
    end

    Bootstrap --> Features
    Bootstrap --> Shared
    Bootstrap --> External
    Features --> Shared
    External --> SA
    External --> SD
```

## アセンブリ依存グラフ

```mermaid
graph LR
    SD["App.Shared.Domain<br/><i>noEngine ✓</i>"]
    SA["App.Shared.Application<br/><i>noEngine ✓</i>"]
    SI["App.Shared.Infrastructure"]
    SP["App.Shared.Presentation"]
    AST["App.Stage<br/><i>noEngine ✓</i>"]
    APL["App.Player<br/><i>noEngine ✓</i>"]
    ABM["App.Bombs<br/><i>noEngine ✓</i><br/>(予定)"]
    ASL["App.Slimes<br/>(予定)"]
    AUP["App.Upgrades<br/>(予定)"]
    AMF["App.MatchFlow<br/>(予定)"]
    AIN["App.Input<br/>(予定)"]
    AUI["App.UI<br/>(予定)"]
    ASO["App.ScriptableObjects"]
    ABT["App.Bootstrap"]

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
        Pres["Presentation<br/>MonoBehaviour, UIDocument<br/>SpriteRenderer, VFX, DOTween"]
        Infra["Infrastructure<br/>Unity API ラッパー, Input System<br/>AudioService, 保存"]
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
        SM_TC["StageModel<br/>.TileChanged"]
        MC_R["MatchClock<br/>.Remaining"]
        MC_P["MatchClock<br/>.CurrentPhase"]
        PS_HP["PlayerStats<br/>.CurrentHp"]
        PS_C["PlayerStats<br/>.Coins"]
        PM_Pos["PlayerModel<br/>.Position"]
        TTS["TileTimerService<br/>.TimerCompleted"]
    end

    subgraph Presentation
        TV["TileView<br/>スプライト切替"]
        HUD_T["HUD Timer"]
        HUD_HP["HUD HP Bar"]
        HUD_C["HUD Coins"]
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
| 4 | ボム | BombSpec 等 6 クラス | BombLaunchUseCase | — | **次** |
| 5 | スライム | SlimeModel 等 4 クラス | SlimeTickService | — | 未着手 |
| 6 | 強化 | UpgradeDef 等 6 クラス | UpgradeApplyService | — | 未着手 |
| 7 | マッチフロー | — | Orchestrator, Scheduler | — | 未着手 |
| 8 | 入力 | — | InputBridge | InputAdapter | 未着手 |
| 9 | UI | — | — | UXML/USS/Presenter | 未着手 |
| 10-14 | Presentation | — | — | View, VFX, Camera | 未着手 |
| 15 | Bootstrap | — | — | LifetimeScope | 未着手 |
| 16-18 | 統合/ポリッシュ | — | — | テスト, FX, SE | 未着手 |
