using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Bombs.Application
{
    public sealed class BombEffectSpreadService
    {
        private readonly StageModel _stage;
        private readonly TileTimerService _tileTimerService;
        private readonly PlayerDamageService _damageService;
        private readonly SafeTileSearchService _safeTileSearch;
        private readonly SlimeRegistry _slimeRegistry;
        private readonly SlimeDropResolver _slimeDropResolver;
        private readonly IRandomProvider _random;
        private readonly ITileIgnitionHandler _tileIgnitionHandler;

        private readonly List<SpreadWave> _activeWaves = new();

        // 全アクティブ波の未適用タイルを集約。
        // SafeTileSearchService に渡して「広がる予定地」への退避を防ぐ。
        private readonly HashSet<GridPos> _pendingTiles = new();

        public bool HasActiveWaves => _activeWaves.Count > 0;

        public BombEffectSpreadService(
            StageModel stage,
            TileTimerService tileTimerService,
            PlayerDamageService damageService,
            SafeTileSearchService safeTileSearch,
            SlimeRegistry slimeRegistry = null,
            SlimeDropResolver slimeDropResolver = null,
            IRandomProvider random = null,
            ITileIgnitionHandler tileIgnitionHandler = null)
        {
            _stage = stage;
            _tileTimerService = tileTimerService;
            _damageService = damageService;
            _safeTileSearch = safeTileSearch;
            _slimeRegistry = slimeRegistry;
            _slimeDropResolver = slimeDropResolver;
            _random = random;
            _tileIgnitionHandler = tileIgnitionHandler;
        }

        public void EnqueueBreakBomb(
            BreakBombResult result,
            GridPos center,
            IReadOnlyList<PlayerModel> players,
            PlayerModel owner,
            float interval)
        {
            var wave = BuildWave(result.AffectedTiles, result.WallsDestroyed, center, players, owner, interval);
            wave.IsBreakBomb = true;
            wave.BreakDamage = result.Damage;
            wave.CollapseTime = result.CollapseTime;
            wave.RecoveryTime = result.RecoveryTime;

            _activeWaves.Add(wave);
            RebuildPendingTiles();
            ApplyWaveUpToDistance(wave, 0);
        }

        public void EnqueueFireBomb(
            FireBombResult result,
            GridPos center,
            IReadOnlyList<PlayerModel> players,
            PlayerModel owner,
            float interval)
        {
            var wave = BuildWave(result.AffectedTiles, result.WallsDestroyed, center, players, owner, interval);
            wave.IsBreakBomb = false;
            wave.ContactDamage = result.ContactDamage;
            wave.FireDuration = result.FireDuration;

            _activeWaves.Add(wave);
            RebuildPendingTiles();
            ApplyWaveUpToDistance(wave, 0);
        }

        public void Tick(float deltaTime)
        {
            bool anyApplied = false;

            for (int w = _activeWaves.Count - 1; w >= 0; w--)
            {
                var wave = _activeWaves[w];
                wave.Elapsed += deltaTime;

                int reachedDist = wave.Interval > 0f
                    ? (int)(wave.Elapsed / wave.Interval)
                    : wave.MaxDistance;

                if (ApplyWaveUpToDistance(wave, reachedDist))
                    anyApplied = true;

                if (wave.AllApplied)
                    _activeWaves.RemoveAt(w);
            }

            if (anyApplied)
                RebuildPendingTiles();
        }

        // ----------------------------------------------------------------
        // 内部
        // ----------------------------------------------------------------

        private SpreadWave BuildWave(
            IReadOnlyList<GridPos> affectedTiles,
            IReadOnlyList<GridPos> wallsDestroyed,
            GridPos center,
            IReadOnlyList<PlayerModel> players,
            PlayerModel owner,
            float interval)
        {
            var wallSet = new HashSet<GridPos>(wallsDestroyed);
            var entries = new List<SpreadEntry>(affectedTiles.Count);
            int maxDist = 0;

            foreach (var pos in affectedTiles)
            {
                int dist = center.ManhattanDistance(pos);
                if (dist > maxDist) maxDist = dist;

                entries.Add(new SpreadEntry(pos, dist, wallSet.Contains(pos)));
            }

            return new SpreadWave(entries, players, owner, interval, maxDist, center);
        }

        /// <summary>
        /// wave 内の distance &lt;= maxDist のエントリを適用する。
        /// 1 件でも適用したら true を返す。
        /// </summary>
        private bool ApplyWaveUpToDistance(SpreadWave wave, int maxDist)
        {
            bool anyApplied = false;
            var occupied = BuildOccupiedWithPending(wave.Players);

            for (int i = 0; i < wave.Entries.Count; i++)
            {
                var entry = wave.Entries[i];
                if (entry.Applied || entry.Distance > maxDist) continue;

                // 炎ボム (壁貫通なし): 段階的広がり中に崩落したタイルで遮断
                // 壁タイル (entry.IsWall) は破壊対象なのでスキップしない
                if (!wave.IsBreakBomb && !entry.IsWall && entry.Distance > 0
                    && !CanFireReach(wave.Center, entry.Pos, entry.Distance, wave.Players))
                {
                    wave.Entries[i] = entry.WithApplied();
                    continue;
                }

                // 永久消滅タイルには適用しない (ブレークボム用 — 炎は上で遮断済み)
                var tileData = _stage.GetTileData(entry.Pos);
                if (tileData.Condition == TileCondition.PermanentlyDestroyed)
                {
                    wave.Entries[i] = entry.WithApplied();
                    continue;
                }

                // 壁破壊: Type を Normal に戻す
                if (entry.IsWall)
                    _stage.SetTileData(entry.Pos, new TileData
                    {
                        Type = TileType.Normal,
                        Condition = TileCondition.Intact,
                        WarpPairId = -1,
                    });

                // タイル状態変更 + タイマー
                if (wave.IsBreakBomb)
                {
                    _stage.SetTileCondition(entry.Pos, TileCondition.Collapsing);
                    _tileTimerService.StartCollapseTimer(entry.Pos, wave.CollapseTime, wave.RecoveryTime);
                }
                else
                {
                    // EternalFire は炎ボムで上書きしない
                    if (tileData.Condition != TileCondition.EternalFire)
                    {
                        _stage.SetTileCondition(entry.Pos, TileCondition.OnFire);
                        _tileTimerService.StartFireTimer(entry.Pos, wave.FireDuration);

                        // 炎着火をハンドラーに通知（ガス連鎖引火等）
                        _tileIgnitionHandler?.OnTileIgnited(entry.Pos);
                    }
                }

                // ダメージ (そのタイルにいるプレイヤー)
                ApplyDamageAtTile(wave, entry.Pos, occupied);

                // スライム
                KillSlimeAtTile(entry.Pos, wave.Owner);

                wave.Entries[i] = entry.WithApplied();
                anyApplied = true;
            }

            if (anyApplied)
            {
                bool allDone = true;
                foreach (var e in wave.Entries)
                {
                    if (!e.Applied) { allDone = false; break; }
                }
                wave.AllApplied = allDone;
            }

            return anyApplied;
        }

        private void ApplyDamageAtTile(SpreadWave wave, GridPos pos, HashSet<GridPos> occupied)
        {
            if (wave.Players == null) return;

            foreach (var player in wave.Players)
            {
                if (player.CurrentPosition != pos) continue;

                // 炎守りのマント: 炎ボム接触ダメージ免疫
                if (!wave.IsBreakBomb && player.Stats.FireShieldActive.CurrentValue)
                    continue;

                // 風の羽衣: ブレークボム崩落ダメージ免疫
                if (wave.IsBreakBomb && player.Stats.LevitationActive.CurrentValue)
                    continue;

                int damage = wave.IsBreakBomb ? wave.BreakDamage : wave.ContactDamage;
                bool forceRelocate = wave.IsBreakBomb;

                _damageService.ApplyDamage(player, damage, forceRelocate, occupied);
            }
        }

        private void KillSlimeAtTile(GridPos pos, PlayerModel killer)
        {
            if (_slimeRegistry == null || _slimeDropResolver == null) return;

            var slime = _slimeRegistry.GetAt(pos);
            if (slime == null) return;

            slime.Kill();
            _slimeDropResolver.Resolve(slime, killer, _random);
            _slimeRegistry.Remove(slime.Id);
        }

        /// <summary>
        /// プレイヤー位置 + 全波の未適用タイルを occupied として返す。
        /// これにより SafeTileSearchService が「広がる予定地」を退避先から除外する。
        /// </summary>
        private HashSet<GridPos> BuildOccupiedWithPending(IReadOnlyList<PlayerModel> players)
        {
            var occupied = new HashSet<GridPos>(_pendingTiles);
            if (players != null)
            {
                foreach (var p in players)
                    occupied.Add(p.CurrentPosition);
            }
            return occupied;
        }

        /// <summary>
        /// 炎が center から pos まで到達できるか。
        /// 経路上 (手前のマス) にタイル障害物またはエンティティがあれば遮断。
        /// 対象マス自身の通行可否もチェックする。
        /// </summary>
        private bool CanFireReach(GridPos center, GridPos pos, int distance,
            IReadOnlyList<PlayerModel> players)
        {
            int dx = pos.X - center.X;
            int dy = pos.Y - center.Y;
            int stepX = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int stepY = dy == 0 ? 0 : (dy > 0 ? 1 : -1);

            for (int d = 1; d <= distance; d++)
            {
                var tile = new GridPos(center.X + stepX * d, center.Y + stepY * d);

                // タイル自体が通行不可なら遮断
                if (!_stage.IsPassable(tile))
                    return false;

                // 手前のマスにエンティティがいれば遮断 (対象マス自身は通す)
                if (d < distance && IsEntityAt(tile, players))
                    return false;
            }
            return true;
        }

        private bool IsEntityAt(GridPos pos, IReadOnlyList<PlayerModel> players)
        {
            if (players != null)
            {
                foreach (var p in players)
                {
                    if (p.CurrentPosition == pos) return true;
                }
            }
            if (_slimeRegistry != null && _slimeRegistry.GetAt(pos) != null)
                return true;
            return false;
        }

        private void RebuildPendingTiles()
        {
            _pendingTiles.Clear();
            foreach (var wave in _activeWaves)
            {
                foreach (var entry in wave.Entries)
                {
                    if (!entry.Applied)
                        _pendingTiles.Add(entry.Pos);
                }
            }
        }

        // ================================================================
        // 内部型
        // ================================================================

        internal readonly struct SpreadEntry
        {
            public readonly GridPos Pos;
            public readonly int Distance;
            public readonly bool IsWall;
            public readonly bool Applied;

            public SpreadEntry(GridPos pos, int distance, bool isWall, bool applied = false)
            {
                Pos = pos;
                Distance = distance;
                IsWall = isWall;
                Applied = applied;
            }

            public SpreadEntry WithApplied() => new(Pos, Distance, IsWall, true);
        }

        internal sealed class SpreadWave
        {
            public readonly List<SpreadEntry> Entries;
            public readonly IReadOnlyList<PlayerModel> Players;
            public readonly PlayerModel Owner;
            public readonly float Interval;
            public readonly int MaxDistance;
            public readonly GridPos Center;

            public float Elapsed;
            public bool AllApplied;

            // Break 固有
            public bool IsBreakBomb;
            public int BreakDamage;
            public float CollapseTime;
            public float RecoveryTime;

            // Fire 固有
            public int ContactDamage;
            public float FireDuration;

            public SpreadWave(
                List<SpreadEntry> entries,
                IReadOnlyList<PlayerModel> players,
                PlayerModel owner,
                float interval,
                int maxDistance,
                GridPos center)
            {
                Entries = entries;
                Players = players;
                Owner = owner;
                Interval = interval;
                MaxDistance = maxDistance;
                Center = center;
            }
        }
    }
}
