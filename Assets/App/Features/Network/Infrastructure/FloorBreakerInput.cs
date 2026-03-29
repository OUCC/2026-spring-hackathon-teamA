using Fusion;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// Fusion 2 のネットワーク入力構造体。
    /// 各クライアントが毎 Tick OnInput() でパックし、ホストが GetInput() で受け取る。
    /// クライアント側でホールドリピート処理済み — MoveHeld=true は「このTickで移動実行」を意味する。
    /// </summary>
    public struct FloorBreakerInput : INetworkInput
    {
        /// <summary>移動方向（8方向）。MoveHeld=true のときのみ有効。</summary>
        public Direction8 MoveDirection;

        /// <summary>このTickで移動を実行すべきか。クライアント側リピートロジックが決定。</summary>
        public NetworkBool MoveHeld;

        /// <summary>ブレークボムボタンが押された瞬間。</summary>
        public NetworkBool BreakBombPressed;

        /// <summary>ブレークボムボタンが離された瞬間。</summary>
        public NetworkBool BreakBombReleased;

        /// <summary>炎ボムボタンが押された瞬間。</summary>
        public NetworkBool FireBombPressed;

        /// <summary>炎ボムボタンが離された瞬間。</summary>
        public NetworkBool FireBombReleased;

        /// <summary>ダッシュがトリガーされた。</summary>
        public NetworkBool DashTriggered;

        /// <summary>ダッシュ方向。DashTriggered=true のときのみ有効。</summary>
        public Direction8 DashDirection;

        /// <summary>強化フェーズ中のUIアクション。UpgradePhase 中のみホストが処理。</summary>
        public UpgradeInputAction UpgradeAction;
    }
}
