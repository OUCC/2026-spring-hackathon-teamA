using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Shared.Application.Interfaces
{
    /// <summary>
    /// タイルに炎が付いた際の処理を抽象化するインターフェース。
    /// ガス連鎖引火など、炎着火に反応する処理を疎結合に接続する。
    /// </summary>
    public interface ITileIgnitionHandler
    {
        void OnTileIgnited(GridPos pos);
    }
}
