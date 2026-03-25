using System.Collections.Generic;
using NUnit.Framework;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Stage
{
    [TestFixture]
    public class TileTimerServiceTests
    {
        private StageModel _model;
        private TileTimerService _svc;

        [SetUp]
        public void SetUp()
        {
            _model = new StageModel(TileCoordRange.FromSize(10));
            _svc = new TileTimerService(_model);
        }

        [TearDown]
        public void TearDown()
        {
            _svc.Dispose();
            _model.Dispose();
        }

        [Test]
        public void CollapseTimer_SetsTileToCollapsed()
        {
            var pos = new GridPos(5, 5);
            _model.SetTileState(pos, TileState.Collapsing);
            _svc.StartCollapseTimer(pos, 3f, 5f);

            _svc.Tick(2.9f);
            Assert.AreEqual(TileState.Collapsing, _model.GetTileState(pos));

            _svc.Tick(0.2f); // total 3.1
            Assert.AreEqual(TileState.Collapsed, _model.GetTileState(pos));
        }

        [Test]
        public void CollapseTimer_AutoChainsToRecovery()
        {
            var pos = new GridPos(5, 5);
            _model.SetTileState(pos, TileState.Collapsing);
            _svc.StartCollapseTimer(pos, 3f, 5f);

            _svc.Tick(3.1f); // collapse done
            Assert.AreEqual(TileState.Collapsed, _model.GetTileState(pos));
            Assert.IsTrue(_svc.HasActiveTimer(pos)); // recovery started

            _svc.Tick(5.1f); // recovery done
            Assert.AreEqual(TileState.Normal, _model.GetTileState(pos));
            Assert.IsFalse(_svc.HasActiveTimer(pos));
        }

        [Test]
        public void FireTimer_SetsTileToNormal()
        {
            var pos = new GridPos(3, 3);
            _model.SetTileState(pos, TileState.OnFire);
            _svc.StartFireTimer(pos, 3.5f);

            _svc.Tick(3.4f);
            Assert.AreEqual(TileState.OnFire, _model.GetTileState(pos));

            _svc.Tick(0.2f);
            Assert.AreEqual(TileState.Normal, _model.GetTileState(pos));
        }

        [Test]
        public void CancelTimer_PreventsCompletion()
        {
            var pos = new GridPos(5, 5);
            _model.SetTileState(pos, TileState.OnFire);
            _svc.StartFireTimer(pos, 3f);

            _svc.CancelTimer(pos);
            _svc.Tick(5f);

            Assert.AreEqual(TileState.OnFire, _model.GetTileState(pos));
        }

        [Test]
        public void Observable_FiresOnCompletion()
        {
            var events = new List<TileTimerCompletedEvent>();
            _svc.TimerCompleted.Subscribe(e => events.Add(e));

            var pos = new GridPos(5, 5);
            _model.SetTileState(pos, TileState.Collapsing);
            _svc.StartCollapseTimer(pos, 1f, 2f);

            _svc.Tick(1.1f);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(TileTimerType.Collapse, events[0].Type);

            _svc.Tick(2.1f);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(TileTimerType.Recovery, events[1].Type);
        }
    }
}
