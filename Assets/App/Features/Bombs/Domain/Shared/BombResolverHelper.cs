using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Bombs.Domain
{
    /// <summary>
    /// BombSpec から Resolver チェーンを構築して解決する便利ヘルパー。
    /// GimmickSimulation 等、DI 外のコンテキストでボム解決が必要な場合に使う。
    /// </summary>
    public static class BombResolverHelper
    {
        public static FireBombResult ResolveFireBomb(GridPos center, BombSpec spec, StageModel stage)
        {
            var query = new StageQueryService(stage);
            var area = new BombAreaResolver(query);
            var resolver = new FireBombResolver(area);
            return resolver.Resolve(center, spec, stage);
        }

        public static BreakBombResult ResolveBreakBomb(GridPos center, BombSpec spec, StageModel stage)
        {
            var query = new StageQueryService(stage);
            var area = new BombAreaResolver(query);
            var resolver = new BreakBombResolver(area);
            return resolver.Resolve(center, spec, stage);
        }
    }
}
