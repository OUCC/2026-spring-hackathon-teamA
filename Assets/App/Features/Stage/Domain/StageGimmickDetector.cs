using System;

namespace FloorBreaker.Stage.Domain
{
    [Flags]
    public enum GimmickFlags
    {
        None = 0,
        Gas = 1,
        Bedrock = 2,
        Warp = 4,
        EternalFire = 8,
    }

    /// <summary>
    /// StageConfig のパラメータからステージに含まれるギミック種別を検出する。
    /// </summary>
    public static class StageGimmickDetector
    {
        public static GimmickFlags Detect(int gasVeinCount, PresetTileEntry[] presetTiles)
        {
            var flags = GimmickFlags.None;

            if (gasVeinCount > 0)
                flags |= GimmickFlags.Gas;

            if (presetTiles != null)
            {
                foreach (var p in presetTiles)
                {
                    if (p.type == TileType.Bedrock) flags |= GimmickFlags.Bedrock;
                    if (p.type == TileType.Warp) flags |= GimmickFlags.Warp;
                    if (p.type == TileType.Gas) flags |= GimmickFlags.Gas;
                    if (p.condition == TileCondition.EternalFire) flags |= GimmickFlags.EternalFire;
                }
            }

            return flags;
        }
    }
}
