using NUnit.Framework;
using SousTension.Sim;

namespace SousTension.Tests
{
    public class SimClockTests
    {
        [Test]
        public void Advance_IncrementsTickDeterministically()
        {
            var clock = new SimClock();
            for (int i = 0; i < 100; i++) clock.Advance();
            Assert.AreEqual(100, clock.Tick);
            Assert.AreEqual(0.1f, SimClock.TickSeconds, 1e-6f);
        }
    }
}
