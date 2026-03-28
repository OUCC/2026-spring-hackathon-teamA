using System;
using System.Collections.Generic;
using System.Linq;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.CpuPlayer.Application
{
    /// <summary>
    /// CPU プレイヤーの移動・ボム発射を毎フレーム判断する。
    /// GameplayInputBridge を経由せず、Domain サービスを直接呼ぶ。
    /// </summary>
    public sealed class CpuPlayerBrain
    {
        private readonly IBalanceParameters _balance;
        private readonly PlayerModel _cpu;
        private readonly PlayerModel _opponent;
        private readonly StageModel _stage;
        private readonly PlayerMoveService _moveService;
        private readonly BombFlightTracker _bombFlightTracker;
        private readonly BombLaunchUseCase _bombLaunchUseCase;
        private readonly BombCooldownState _cooldown;
        private readonly SlimeRegistry _slimeRegistry;
        private readonly IReadOnlyList<PlayerModel> _allPlayers;

        private readonly Random _rng = new Random();

        private float _thinkTimer;
        private float _moveAccumulator;
        private float _bombReleaseTimer;
        private bool _waitingForRelease;
        private Direction8? _desiredDirection;
        private Direction8? _wanderDirection;
        private int _wanderStepsRemaining;

        public CpuPlayerBrain(
            IBalanceParameters balance,
            PlayerModel cpu,
            PlayerModel opponent,
            StageModel stage,
            PlayerMoveService moveService,
            BombFlightTracker bombFlightTracker,
            BombLaunchUseCase bombLaunchUseCase,
            BombCooldownState cooldown,
            SlimeRegistry slimeRegistry,
            IReadOnlyList<PlayerModel> allPlayers)
        {
            _balance = balance;
            _cpu = cpu;
            _opponent = opponent;
            _stage = stage;
            _moveService = moveService;
            _bombFlightTracker = bombFlightTracker;
            _bombLaunchUseCase = bombLaunchUseCase;
            _cooldown = cooldown;
            _slimeRegistry = slimeRegistry;
            _allPlayers = allPlayers;
        }

        public void Tick(float deltaTime)
        {
            if (_cpu.Stats.IsDead) return;
            if (_cpu.ForcedMove.IsForced) return;

            // ボムリリース待ち
            if (_waitingForRelease)
            {
                _bombReleaseTimer -= deltaTime;
                if (_bombReleaseTimer <= 0f)
                {
                    _bombFlightTracker.ReleaseBomb(_cpu.Id, _allPlayers);
                    _waitingForRelease = false;
                }
                return;
            }

            // 思考間隔
            _thinkTimer -= deltaTime;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = _balance.CpuThinkInterval;
                Think();
            }

            // 移動実行
            float moveInterval = _balance.CpuBaseMoveInterval / _cpu.Stats.MoveSpeed;
            _moveAccumulator += deltaTime;
            if (_moveAccumulator >= moveInterval && _desiredDirection.HasValue)
            {
                _moveService.TryMove(_cpu, _desiredDirection.Value, _stage);
                _moveAccumulator = 0f;
            }
        }

        private void Think()
        {
            _desiredDirection = EvaluateMovement();
            EvaluateBomb();
        }

        private Direction8? EvaluateMovement()
        {
            var myPos = _cpu.CurrentPosition;

            // 1. 危険回避: 足元が危険なら安全な隣接マスへ
            if (IsDangerous(myPos))
            {
                var safeDir = FindSafeDirection(myPos);
                if (safeDir.HasValue) return safeDir.Value;
            }

            // 2. スライム狩り: 近くのスライムに近づく (コイン獲得のため)
            var nearestSlime = FindNearestSlime(myPos, 8);
            if (nearestSlime.HasValue)
            {
                int slimeDist = myPos.ChebyshevDistance(nearestSlime.Value);
                // ボム射程内なら近づかない (そこからボムで狙う)
                int bombRange = _cpu.Build.BreakFlightRange;
                if (slimeDist > bombRange)
                {
                    var dir = TowardTarget(myPos, nearestSlime.Value);
                    if (dir.HasValue && IsSafeToMove(myPos, dir.Value))
                        return dir.Value;
                }
            }

            // 3. 相手プレイヤーに近づく (ボム射程の少し外を維持)
            if (!_opponent.Stats.IsDead)
            {
                var oppPos = _opponent.CurrentPosition;
                int dist = myPos.ChebyshevDistance(oppPos);
                int idealDist = Math.Max(2, _cpu.Build.BreakFlightRange - 1);

                if (dist > idealDist)
                {
                    var dir = TowardTarget(myPos, oppPos);
                    if (dir.HasValue && IsSafeToMove(myPos, dir.Value))
                        return dir.Value;
                }
                else if (dist < 2)
                {
                    // 近すぎ → 離れる
                    var awayDir = TowardTarget(oppPos, myPos);
                    if (awayDir.HasValue && IsSafeToMove(myPos, awayDir.Value))
                        return awayDir.Value;
                }
            }

            // 4. ステージ中心寄り
            var bounds = _stage.GetCurrentBounds();
            int centerX = (bounds.MinX + bounds.MaxX) / 2;
            int centerY = (bounds.MinY + bounds.MaxY) / 2;
            var center = new GridPos(centerX, centerY);

            if (IsNearEdge(myPos, bounds, 2))
            {
                var dir = TowardTarget(myPos, center);
                if (dir.HasValue && IsSafeToMove(myPos, dir.Value))
                    return dir.Value;
            }

            // 5. 徘徊: ターゲットがないときランダムに歩き回る
            if (_wanderStepsRemaining > 0 && _wanderDirection.HasValue)
            {
                if (IsSafeToMove(myPos, _wanderDirection.Value))
                {
                    _wanderStepsRemaining--;
                    return _wanderDirection.Value;
                }
                _wanderStepsRemaining = 0;
            }

            var newDir = PickRandomSafeDirection(myPos);
            if (newDir.HasValue)
            {
                _wanderDirection = newDir.Value;
                _wanderStepsRemaining = _rng.Next(2, 5);
                _wanderStepsRemaining--;
                return newDir.Value;
            }

            return null;
        }

        private void EvaluateBomb()
        {
            if (_bombFlightTracker.IsFlying(_cpu.Id)) return;

            var myPos = _cpu.CurrentPosition;

            // スライムを狙う
            var slimeTarget = FindBombTarget(myPos, isSlimeTarget: true);
            if (slimeTarget.HasValue)
            {
                TryLaunchBomb(slimeTarget.Value.Dir, slimeTarget.Value.Type);
                return;
            }

            // 相手プレイヤーを狙う
            if (!_opponent.Stats.IsDead)
            {
                var playerTarget = FindBombTarget(myPos, isSlimeTarget: false);
                if (playerTarget.HasValue)
                {
                    TryLaunchBomb(playerTarget.Value.Dir, playerTarget.Value.Type);
                }
            }
        }

        private (Direction8 Dir, BombType Type)? FindBombTarget(GridPos from, bool isSlimeTarget)
        {
            // 8方向それぞれにターゲットがいるか確認
            foreach (Direction8 dir in Enum.GetValues(typeof(Direction8)))
            {
                var offset = dir.ToOffset();
                int breakRange = _cpu.Build.BreakFlightRange;
                int fireRange = _cpu.Build.FireFlightRange;
                int maxCheck = Math.Max(breakRange, fireRange);

                for (int d = 1; d <= maxCheck; d++)
                {
                    var checkPos = from + offset * d;
                    if (!_stage.IsInBounds(checkPos)) break;

                    var tileData = _stage.GetTileData(checkPos);
                    if (TileData.IsImpassableType(tileData.Type)
                        || tileData.Condition == TileCondition.PermanentlyDestroyed)
                        break;

                    if (isSlimeTarget)
                    {
                        if (_slimeRegistry.IsOccupied(checkPos))
                        {
                            // BreakBomb を優先 (範囲崩落でスライム巻き込み)
                            if (d <= breakRange && _cooldown.CanFire(BombType.Break))
                                return (dir, BombType.Break);
                            if (d <= fireRange && _cooldown.CanFire(BombType.Fire))
                                return (dir, BombType.Fire);
                        }
                    }
                    else
                    {
                        if (_opponent.CurrentPosition == checkPos)
                        {
                            // 近距離は FireBomb (直接ダメージ), 遠距離は BreakBomb
                            if (d <= 3 && d <= fireRange && _cooldown.CanFire(BombType.Fire))
                                return (dir, BombType.Fire);
                            if (d <= breakRange && _cooldown.CanFire(BombType.Break))
                                return (dir, BombType.Break);
                            if (d <= fireRange && _cooldown.CanFire(BombType.Fire))
                                return (dir, BombType.Fire);
                        }
                    }
                }
            }

            return null;
        }

        private void TryLaunchBomb(Direction8 direction, BombType type)
        {
            _cpu.CurrentFacing = direction;

            var spec = type == BombType.Break
                ? _bombLaunchUseCase.CreateBreakBombSpec(_cpu.Build)
                : _bombLaunchUseCase.CreateFireBombSpec(_cpu.Build);

            if (_cpu.Build.HasDualShot)
            {
                var leftDir = direction.RotateCCW90();
                var rightDir = direction.RotateCW90();
                if (!_bombFlightTracker.StartFlight(_cpu.Id, _cpu.CurrentPosition, leftDir, spec))
                    return;
                _bombFlightTracker.StartDualFlight(_cpu.Id, _cpu.CurrentPosition, rightDir, spec);
            }
            else
            {
                if (!_bombFlightTracker.StartFlight(_cpu.Id, _cpu.CurrentPosition, direction, spec))
                    return;
            }

            // 少し遅延してリリース (即リリースだと不自然)
            _waitingForRelease = true;
            _bombReleaseTimer = _balance.CpuBombReleaseDelay;
        }

        private bool IsDangerous(GridPos pos)
        {
            var cond = _stage.GetTileCondition(pos);
            return TileData.IsBurning(cond)
                || cond == TileCondition.Collapsing
                || cond == TileCondition.PermanentlyDestroyed;
        }

        private Direction8? FindSafeDirection(GridPos from)
        {
            // 8方向から安全なマスを探す (Cardinal 優先)
            Direction8[] cardinals = { Direction8.N, Direction8.E, Direction8.S, Direction8.W };
            foreach (var dir in cardinals)
            {
                var target = from.Neighbor(dir);
                if (_stage.IsInBounds(target) && _stage.IsPassable(target) && !IsDangerous(target))
                    return dir;
            }

            Direction8[] diagonals = { Direction8.NE, Direction8.SE, Direction8.SW, Direction8.NW };
            foreach (var dir in diagonals)
            {
                var target = from.Neighbor(dir);
                if (_stage.IsInBounds(target) && _stage.IsPassable(target) && !IsDangerous(target))
                    return dir;
            }

            return null;
        }

        private GridPos? FindNearestSlime(GridPos from, int searchRange)
        {
            GridPos? nearest = null;
            int minDist = int.MaxValue;

            foreach (var slime in _slimeRegistry.GetAll())
            {
                if (!slime.IsAlive) continue;
                int d = from.ChebyshevDistance(slime.Position);
                if (d <= searchRange && d < minDist)
                {
                    minDist = d;
                    nearest = slime.Position;
                }
            }

            return nearest;
        }

        private Direction8? PickRandomSafeDirection(GridPos from)
        {
            var dirs = ((Direction8[])Enum.GetValues(typeof(Direction8)))
                .OrderBy(_ => _rng.Next())
                .ToArray();

            foreach (var dir in dirs)
            {
                if (IsSafeToMove(from, dir))
                    return dir;
            }

            return null;
        }

        private bool IsSafeToMove(GridPos from, Direction8 dir)
        {
            var target = from.Neighbor(dir);
            return _stage.IsInBounds(target)
                && _stage.IsPassable(target)
                && !IsDangerous(target);
        }

        private static bool IsNearEdge(GridPos pos, TileCoordRange bounds, int margin)
        {
            return pos.X - bounds.MinX < margin
                || bounds.MaxX - pos.X < margin
                || pos.Y - bounds.MinY < margin
                || bounds.MaxY - pos.Y < margin;
        }

        private static Direction8? TowardTarget(GridPos from, GridPos to)
        {
            int dx = Math.Sign(to.X - from.X);
            int dy = Math.Sign(to.Y - from.Y);

            if (dx == 0 && dy == 0) return null;

            // (dx, dy) -> Direction8 mapping
            return (dx, dy) switch
            {
                (0, 1)   => Direction8.N,
                (1, 1)   => Direction8.NE,
                (1, 0)   => Direction8.E,
                (1, -1)  => Direction8.SE,
                (0, -1)  => Direction8.S,
                (-1, -1) => Direction8.SW,
                (-1, 0)  => Direction8.W,
                (-1, 1)  => Direction8.NW,
                _        => null,
            };
        }
    }
}
