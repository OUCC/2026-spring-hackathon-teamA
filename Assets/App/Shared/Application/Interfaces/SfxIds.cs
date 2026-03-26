namespace FloorBreaker.Shared.Application.Interfaces
{
    public static class SfxIds
    {
        // ボム
        public const string BombLaunch = "bomb_launch";
        public const string BombExplodeFire = "bomb_explode_fire";
        public const string BombExplodeFall = "bomb_explode_fall";

        // タイル
        public const string TileCollapse = "tile_collapse";
        public const string TileFire = "tile_fire";
        public const string TileDestroy = "tile_destroy";

        // スライム
        public const string SlimeSpawn = "slime_spawn";
        public const string SlimeAttack = "slime_attack";
        public const string SlimeDeath = "slime_death";

        // プレイヤー
        public const string PlayerHit = "player_hit";
        public const string PlayerDeath = "player_death";

        // コイン
        public const string CoinPickup = "coin_pickup";

        // 強化フェーズ
        public const string UpgradeSelect = "upgrade_select";
        public const string UpgradeReroll = "upgrade_reroll";
        public const string UpgradeDone = "upgrade_done";

        // フェーズ遷移
        public const string PhaseShrink = "phase_shrink";
        public const string PhaseUpgrade = "phase_upgrade";
        public const string MatchResult = "match_result";

        // UI
        public const string UiNavigate = "ui_navigate";
    }
}
