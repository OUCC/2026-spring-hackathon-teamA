using NUnit.Framework;
using FloorBreaker.Shared.Infrastructure.Random;

namespace FloorBreaker.Tests.EditMode
{
    [TestFixture]
    public class SeededRandomProviderTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new SeededRandomProvider(42);
            var b = new SeededRandomProvider(42);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(a.Range(0, 1000), b.Range(0, 1000));
            }
        }

        [Test]
        public void Range_RespectsMinMax()
        {
            var rng = new SeededRandomProvider(123);
            for (int i = 0; i < 500; i++)
            {
                int val = rng.Range(5, 10);
                Assert.GreaterOrEqual(val, 5);
                Assert.Less(val, 10);
            }
        }

        [Test]
        public void Value_IsBetween0And1()
        {
            var rng = new SeededRandomProvider(456);
            for (int i = 0; i < 500; i++)
            {
                float val = rng.Value;
                Assert.GreaterOrEqual(val, 0f);
                Assert.Less(val, 1f);
            }
        }

        [Test]
        public void Chance_ZeroProbability_AlwaysFalse()
        {
            var rng = new SeededRandomProvider(789);
            for (int i = 0; i < 100; i++)
            {
                Assert.IsFalse(rng.Chance(0f));
            }
        }

        [Test]
        public void Chance_OneProbability_AlwaysTrue()
        {
            var rng = new SeededRandomProvider(101);
            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(rng.Chance(1f));
            }
        }
    }
}
