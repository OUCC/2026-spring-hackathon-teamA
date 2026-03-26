# FLOOR BREAKER

2人対戦リアルタイム対戦ゲーム。30x30 のグリッド上でボムを投げ合い、床を壊し、スライムを倒してコインを集め、強化カードでビルドを組む。

## ゲーム概要

- **ジャンル**: リアルタイム対戦アクション（ローカル2人対戦・左右分割画面）
- **テーマ**: 中世ファンタジー × ポップアーケード
- **勝利条件**: 相手の HP を 0 にする

### コアループ

1. **移動** — 8方向でグリッド上を移動
2. **ボム** — ブレークボム（床崩落）と炎ボム（炎上ダメージ）を投げる
3. **スライム討伐** — コインを稼ぐ
4. **20秒ごとのフェーズ** — ステージ外周が永久消滅 → 強化カード選択フェーズ
5. **強化** — 3枚のカードから選んでビルドを強化（Vampire Survivors 風）

### 特徴

- グリッド座標が authoritative（物理演算ではなくルール駆動）
- 20秒周期でステージが縮小し、強化フェーズが発生
- スライム3種（通常 / ゴールド / レッド）
- 強化カードにレアリティ（Common / Rare / Epic）とコストあり

## 技術スタック

| 技術 | 用途 |
|------|------|
| **Unity 6.3** (URP) | ゲームエンジン |
| **VContainer** | 依存性注入 (DI) |
| **UniTask** | 非同期処理（初期化・シーン遷移・演出待機） |
| **R3** | リアクティブ状態管理（HP・コイン・UI反映） |
| **UI Toolkit** | ランタイム UI（HUD・強化オーバーレイ・リザルト） |
| **Input System** | 入力管理 |
| **DOTween Pro** | トゥイーンアニメーション |
| **Feel** | ゲームフィール（画面シェイク・ヒットストップ） |
| **Epic Toon FX** | VFX パーティクル |
| **All In 1 Sprite Shader** | スプライト視覚効果 |

## アーキテクチャ

**Feature-First + Layer-Within-Feature** 構成を採用。

```
Assets/App/
├── Bootstrap/          # DI composition root (LifetimeScope)
├── Shared/             # 共有プリミティブ・インターフェース
├── Features/
│   ├── MatchFlow/      # フェーズ制御・オーケストレーション
│   ├── Stage/          # グリッド・タイル状態・壁生成・縮小
│   ├── Player/         # プレイヤー状態・HP・ビルド
│   ├── Bombs/          # ボム仕様・着弾解決・範囲計算
│   ├── Slimes/         # スポーン・AI・ドロップ
│   ├── Upgrades/       # 強化定義・候補生成・適用
│   ├── Cameras/        # 分割カメラ・追従・シェイク
│   ├── Input/          # 入力アダプター
│   └── UI/             # UXML/USS・HUD・強化オーバーレイ・リザルト
├── Scenes/             # Title / Match / Result
├── ScriptableObjects/  # バランス調整・設定
└── Tests/              # EditMode / PlayMode
```

### レイヤー依存方向

```
Domain（pure C#, Unity非依存）
  ↑
Application（ユースケース・インターフェース）
  ↑
Infrastructure（Unity API・Audio・永続化）
  ↑
Presentation（MonoBehaviour・UIToolkit・View）
  ↑
Bootstrap（配線のみ）
```

## セットアップ

### 前提条件

- Unity 6.3
- Git

### 手順

1. リポジトリをクローン
   ```bash
   git clone https://github.com/OUCC/2026-spring-hackathon-teamA.git
   ```
2. Unity Hub からプロジェクトを開く
3. `Assets/App/Scenes/Title.unity` を開いて Play

## テスト

Unity Test Runner で EditMode テストを実行:

- **Window > General > Test Runner > EditMode > Run All**
- アセンブリ: `App.Tests.EditMode`

## ドキュメント

| ファイル | 内容 |
|----------|------|
| [`docs/implementation.md`](docs/implementation.md) | ゲーム仕様書（ルール・パラメータ・アルゴリズム） |
| [`CLAUDE.md`](CLAUDE.md) | AI コーディングエージェント向け運用ルール |

## 提出情報

### 見てほしいポイント

- **グリッド駆動のルール設計**: 物理エンジンに頼らず、30x30グリッド上の状態遷移ですべてのゲームルール（ボム着弾・炎延焼・床崩落・退避先探索・ステージ縮小）を確定させている。Domain層はpure C#で、MonoBehaviourから完全に独立している
- **アーキテクチャ**: feature-first + layer-within-feature構成で、VContainer（DI）+ UniTask（非同期）+ R3（リアクティブ）+ UI Toolkit を責務ごとに使い分けている。Domain/Application/Infrastructure/Presentationの依存方向を厳密に守っている
- **1キーボード2人対戦**: Input Systemのアクションマップを P1/P2 で分離し、1台のキーボードでローカル対戦を実現。キーリバインド機能と操作テスト空間もタイトル画面に用意した
- **20秒周期の連動システム**: ステージ縮小・強化フェーズ・ゲーム一時停止を単一のPhaseSchedulerで駆動し、タイマーの分散管理を防いでいる
- **強化カードシステム**: data-driven な強化定義、コスト・スタック上限・出現条件をDomainルールとして持ち、UIは表示のみに徹している

### 今後こうしようと思っていること

- 1P vs CPU モード（スライムAIの拡張でCPU対戦を実装）
- ネットワーク対戦対応（Time.timeScale非依存の設計は済み、Presentation層のみの演出設計に移行中）
- 強化カードの種類追加とバランス調整
- ステージバリエーション（初期壁配置パターンの追加）
- SE / VFX の充実（Feel + Epic Toon FX をさらに活用）

### 開発期間

2週間

### チーム人数

4人

### その他

- GitHub: https://github.com/OUCC/2026-spring-hackathon-teamA
- Unity 6.3 を使用
- 使用アセット: DOTween Pro, Feel, Epic Toon FX, All In 1 Sprite Shader, Medieval Fantasy SFX Bundle
- EditModeテストでDomainロジックの品質を担保している

## ライセンス

Asset Store アセット（DOTween Pro, Feel, Epic Toon FX, All In 1 Sprite Shader, Medieval Fantasy SFX Bundle）は各ライセンスに従います。再配布不可。
