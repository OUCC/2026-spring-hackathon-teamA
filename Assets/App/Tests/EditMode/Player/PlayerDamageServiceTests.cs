using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Player.Domain;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Player
{
    [TestFixture]
    public class PlayerDamageServiceTests
    {
        private StageModel _stage;
        private PlayerModel _player;
        private PlayerDamageService _svc;
        private SafeTileSearchService _safeTile;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            var stats = new PlayerStats(10, 1f, 3f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player = new PlayerModel(PlayerId.Player1, new GridPos(5, 5), stats, build);
            _svc = new PlayerDamageService(invulnerabilityDuration: 1.5f, forcedMoveDuration: 1f);
            _safeTile = new SafeTileSearchService();
        }

        [TearDown]
        public void TearDown()
        {
            _player.Dispose();
            _stage.Dispose();
        }

        [Test]
        public void ApplyDamage_ReducesHp()
        {
            _svc.ApplyDamage(_player, 3, false, _stage, _safeTile, new HashSet<GridPos>());
            Assert.AreEqual(7, _player.Stats.CurrentHp.CurrentValue);
        }

        [Test]
        public void ApplyDamage_ActivatesInvulnerability()
        {
            _svc.ApplyDamage(_player, 1, false, _stage, _safeTile, new HashSet<GridPos>());
            Assert.IsTrue(_player.Invulnerability.IsInvulnerable);
        }

        [Test]
        public void ApplyDamage_WhileInvulnerable_IsIgnored()
        {
            _player.Invulnerability.Activate(5f);
            var result = _svc.ApplyDamage(_player, 3, false, _stage, _safeTile, new HashSet<GridPos>());
            Assert.IsFalse(result);
            Assert.AreEqual(10, _player.Stats.CurrentHp.CurrentValue);
        }

        [Test]
        public void ApplyDamage_WithRelocate_StartsForcedMove()
        {
            _stage.SetTileState(new GridPos(5, 5), TileState.Collapsing);
            _svc.ApplyDamage(_player, 2, true, _stage, _safeTile, new HashSet<GridPos>());
            Assert.IsTrue(_player.ForcedMove.IsForced);
        }

        [Test]
        public void ApplyDamage_WithRelocate_UpdatesPosition()
        {
            _stage.SetTileState(new GridPos(5, 5), TileState.Collapsing);
            _svc.ApplyDamage(_player, 2, true, _stage, _safeTile, new HashSet<GridPos>());
            Assert.AreNotEqual(new GridPos(5, 5), _player.CurrentPosition);
            Assert.AreEqual(_player.ForcedMove.Target, _player.CurrentPosition);
        }

        [Test]
        public void ApplyDamage_WithoutRelocate_NoForcedMove()
        {
            _svc.ApplyDamage(_player, 2, false, _stage, _safeTile, new HashSet<GridPos>());
            Assert.IsFalse(_player.ForcedMove.IsForced);
        }
    }
}
