using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Player.Domain;
using FloorBreaker.MatchFlow.Application;

namespace FloorBreaker.Tests.EditMode.MatchFlow
{
    [TestFixture]
    public class MatchEndUseCaseTests
    {
        private MatchEndUseCase _useCase;
        private PlayerModel _player1;
        private PlayerModel _player2;
        private List<PlayerModel> _players;

        [SetUp]
        public void SetUp()
        {
            _useCase = new MatchEndUseCase();

            var stats1 = new PlayerStats(10, 1f, 3f);
            var build1 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player1 = new PlayerModel(PlayerId.Player1, new GridPos(2, 2), stats1, build1);

            var stats2 = new PlayerStats(10, 1f, 3f);
            var build2 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player2 = new PlayerModel(PlayerId.Player2, new GridPos(7, 7), stats2, build2);

            _players = new List<PlayerModel> { _player1, _player2 };
        }

        [TearDown]
        public void TearDown()
        {
            _useCase.Dispose();
            _player1.Dispose();
            _player2.Dispose();
        }

        [Test]
        public void CheckEnd_BothAlive_ReturnsNull()
        {
            var result = _useCase.CheckEnd(_players);
            Assert.IsNull(result);
        }

        [Test]
        public void CheckEnd_P1Dead_ReturnsP2()
        {
            _player1.Stats.TakeDamage(10);

            var result = _useCase.CheckEnd(_players);
            Assert.AreEqual(PlayerId.Player2, result);
        }

        [Test]
        public void CheckEnd_P2Dead_ReturnsP1()
        {
            _player2.Stats.TakeDamage(10);

            var result = _useCase.CheckEnd(_players);
            Assert.AreEqual(PlayerId.Player1, result);
        }

        [Test]
        public void Winner_InitiallyNull()
        {
            Assert.IsNull(_useCase.Winner.CurrentValue);
        }

        [Test]
        public void SetWinner_UpdatesWinnerProperty()
        {
            _useCase.SetWinner(PlayerId.Player1);
            Assert.AreEqual(PlayerId.Player1, _useCase.Winner.CurrentValue);
        }
    }
}
