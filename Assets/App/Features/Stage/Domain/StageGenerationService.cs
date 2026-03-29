using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    /// <summary>
    /// ステージのタイル配置（壁 → ガス脈 → プリセット）を一括で行う Domain サービス。
    /// MatchInitializer と StagePreviewRenderer の両方から利用される。
    /// </summary>
    public sealed class StageGenerationService
    {
        private static readonly (int dx, int dy)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        /// <summary>
        /// StageModel にタイルを配置する。壁生成 → ガス脈生成 → プリセットタイル適用の順。
        /// </summary>
        /// <param name="stage">配置先の StageModel（Intact 状態で初期化済みであること）</param>
        /// <param name="genParams">生成パラメータ</param>
        /// <param name="spawnProtectionPositions">壁・ガス脈を配置しない保護位置（プレイヤースポーン地点等）</param>
        /// <param name="random">乱数プロバイダ</param>
        public void PopulateStage(
            StageModel stage,
            StageGenerationParams genParams,
            IReadOnlyList<GridPos> spawnProtectionPositions,
            IRandomProvider random)
        {
            var bounds = stage.GetCurrentBounds();

            // 1. 壁生成
            var wallService = new WallGenerationService(
                genParams.WallSeedPercent,
                genParams.WallGrowthChance,
                genParams.WallTargetPercent,
                genParams.SpawnProtectionRadius);

            var walls = wallService.Generate(bounds, spawnProtectionPositions, random);
            foreach (var pos in walls)
                stage.SetTileData(pos, new TileData
                {
                    Type = TileType.Wall,
                    Condition = TileCondition.Intact,
                    WarpPairId = -1,
                });

            // 2. ガス脈ランダムウォーク生成
            if (genParams.GasVeinCount > 0)
                GenerateGasVeins(stage, genParams, bounds, spawnProtectionPositions, random);

            // 3. プリセットタイル配置
            if (genParams.PresetTiles != null)
            {
                foreach (var preset in genParams.PresetTiles)
                {
                    var pos = new GridPos(preset.x, preset.y);
                    if (!stage.IsInBounds(pos)) continue;
                    stage.SetTileData(pos, new TileData
                    {
                        Type = preset.type,
                        Condition = preset.condition,
                        WarpPairId = preset.warpPairId,
                    });
                }
            }
        }

        private static void GenerateGasVeins(
            StageModel stage,
            StageGenerationParams genParams,
            TileCoordRange bounds,
            IReadOnlyList<GridPos> spawnProtectionPositions,
            IRandomProvider random)
        {
            int protectRadius = genParams.SpawnProtectionRadius;

            for (int v = 0; v < genParams.GasVeinCount; v++)
            {
                // ランダム seed 位置（スポーン保護を避ける）
                int attempts = 0;
                int sx, sy;
                do
                {
                    sx = random.Range(bounds.MinX + 3, bounds.MaxX - 2);
                    sy = random.Range(bounds.MinY + 3, bounds.MaxY - 2);
                    attempts++;
                } while (attempts < 50 && IsNearSpawn(sx, sy, spawnProtectionPositions, protectRadius));

                if (attempts >= 50) continue;

                // ランダムウォークで vein を伸ばす
                int length = random.Range(genParams.GasVeinMinLength, genParams.GasVeinMaxLength + 1);
                int cx = sx, cy = sy;
                var dir = Dirs[random.Range(0, 4)];

                for (int step = 0; step < length; step++)
                {
                    var pos = new GridPos(cx, cy);
                    if (!stage.IsInBounds(pos)) break;

                    var existing = stage.GetTileData(pos);
                    if (existing.Type == TileType.Wall || existing.Type == TileType.Bedrock)
                        break;
                    if (existing.Type != TileType.Gas && existing.Condition == TileCondition.Intact)
                    {
                        stage.SetTileData(pos, new TileData
                        {
                            Type = TileType.Gas,
                            Condition = TileCondition.Intact,
                            WarpPairId = -1,
                        });
                    }

                    // 次のステップ: 70% で同方向、30% で直角に曲がる
                    if (random.Range(0, 100) < 30)
                    {
                        if (dir.dx != 0)
                            dir = random.Range(0, 2) == 0 ? (0, 1) : (0, -1);
                        else
                            dir = random.Range(0, 2) == 0 ? (1, 0) : (-1, 0);
                    }

                    cx += dir.dx;
                    cy += dir.dy;
                }
            }
        }

        private static bool IsNearSpawn(int x, int y, IReadOnlyList<GridPos> spawns, int radius)
        {
            foreach (var s in spawns)
            {
                if (Math.Abs(x - s.X) <= radius && Math.Abs(y - s.Y) <= radius)
                    return true;
            }
            return false;
        }
    }
}
