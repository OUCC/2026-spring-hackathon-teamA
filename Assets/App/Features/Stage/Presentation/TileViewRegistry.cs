using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Presentation
{
    /// <summary>
    /// Dictionary&lt;GridPos, TileView&gt; のラッパー。
    /// VContainer 登録時は空で、MatchInitializer が生成後に SetViews で格納する。
    /// </summary>
    public sealed class TileViewRegistry
    {
        private Dictionary<GridPos, TileView> _views;

        public Dictionary<GridPos, TileView> Views => _views;

        public void SetViews(Dictionary<GridPos, TileView> views)
        {
            _views = views;
        }
    }
}
