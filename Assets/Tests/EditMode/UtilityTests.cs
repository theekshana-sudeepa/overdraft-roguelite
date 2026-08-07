using NUnit.Framework;
using UnityEngine;
using Overdraft.UI;
using Overdraft.Utility;

namespace Overdraft.Tests
{
    /// <summary>Broad-phase, collision and the zero-allocation HUD formatter.</summary>
    public sealed class UtilityTests
    {
        [Test]
        public void CircleOverlap_MatchesAnalyticDistance()
        {
            Vector3 a = new Vector3(0f, 0f, 0f);
            Vector3 b = new Vector3(1.0f, 0f, 0f);

            Assert.IsTrue(CircleOverlap.Overlaps(a, 0.6f, b, 0.5f), "1.1 total radius over 1.0 distance");
            Assert.IsFalse(CircleOverlap.Overlaps(a, 0.4f, b, 0.5f), "0.9 total radius over 1.0 distance");
        }

        [Test]
        public void CircleOverlap_IgnoresY()
        {
            Vector3 a = new Vector3(0f, 0f, 0f);
            Vector3 b = new Vector3(0f, 100f, 0f);

            Assert.IsTrue(CircleOverlap.Overlaps(a, 0.1f, b, 0.1f), "collision is XZ-only by design (C5)");
        }

        [Test]
        public void SpatialGrid_FindsNeighboursWithinRadius()
        {
            SpatialGrid grid = new SpatialGrid(18f, 2f, 64);

            grid.BeginBuild();
            grid.Add(0, 0f, 0f);
            grid.Add(1, 0.5f, 0.5f);
            grid.Add(2, 10f, 10f);
            grid.EndBuild();

            int[] results = new int[64];
            int found = grid.Query(0f, 0f, 1f, results);

            bool sawZero = false;
            bool sawOne = false;
            bool sawTwo = false;

            for (int i = 0; i < found; i++)
            {
                if (results[i] == 0) { sawZero = true; }
                if (results[i] == 1) { sawOne = true; }
                if (results[i] == 2) { sawTwo = true; }
            }

            Assert.IsTrue(sawZero);
            Assert.IsTrue(sawOne);
            Assert.IsFalse(sawTwo, "a query must not return items from a distant cell");
        }

        [Test]
        public void SpatialGrid_HandlesEveryItemInOneCell()
        {
            // The clump case. A fixed-capacity bucket implementation would silently drop
            // contacts here, which is exactly when the player is most likely to be hit.
            const int count = 200;
            SpatialGrid grid = new SpatialGrid(18f, 2f, count);

            grid.BeginBuild();
            for (int i = 0; i < count; i++) { grid.Add(i, 0.1f, 0.1f); }
            grid.EndBuild();

            int[] results = new int[count];
            int found = grid.Query(0f, 0f, 0.5f, results);

            Assert.AreEqual(count, found, "every stacked item must still be reported");
        }

        [Test]
        public void SpatialGrid_ClampsOutOfBoundsPositions()
        {
            SpatialGrid grid = new SpatialGrid(18f, 2f, 8);

            grid.BeginBuild();
            grid.Add(0, 9999f, -9999f);
            grid.EndBuild();

            int[] results = new int[8];
            Assert.DoesNotThrow(() => grid.Query(0f, 0f, 1f, results));
        }

        [Test]
        public void MathX_ClampToCircle_PreservesInsidePoints()
        {
            Vector3 inside = new Vector3(1f, 0f, 1f);
            Assert.AreEqual(inside, MathX.ClampToCircleXZ(inside, 18f));

            Vector3 outside = new Vector3(100f, 0f, 0f);
            Vector3 clamped = MathX.ClampToCircleXZ(outside, 18f);
            Assert.AreEqual(18f, new Vector2(clamped.x, clamped.z).magnitude, 1e-3f);
        }

        [Test]
        public void DeterministicRandom_SameSeedProducesSameSequence()
        {
            DeterministicRandom a = new DeterministicRandom(1234);
            DeterministicRandom b = new DeterministicRandom(1234);

            for (int i = 0; i < 500; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
            }
        }

        [Test]
        public void DeterministicRandom_ZeroSeedDoesNotCollapse()
        {
            DeterministicRandom rng = new DeterministicRandom(0);

            uint first = rng.NextUInt();
            uint second = rng.NextUInt();

            Assert.AreNotEqual(0u, first, "zero is an absorbing state for raw xorshift");
            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void FastNumberText_FormatsWithSeparators()
        {
            char[] buffer = new char[32];

            Assert.AreEqual("0", Render(buffer, 0L, true));
            Assert.AreEqual("7", Render(buffer, 7L, true));
            Assert.AreEqual("999", Render(buffer, 999L, true));
            Assert.AreEqual("1,000", Render(buffer, 1000L, true));
            Assert.AreEqual("124,900", Render(buffer, 124900L, true));
            Assert.AreEqual("9,999,999,999", Render(buffer, 9_999_999_999L, true));
            Assert.AreEqual("-1,234", Render(buffer, -1234L, true));
        }

        [Test]
        public void FastNumberText_FormatsWithoutSeparators()
        {
            char[] buffer = new char[32];
            Assert.AreEqual("124900", Render(buffer, 124900L, false));
        }

        [Test]
        public void FastNumberText_HandlesLongExtremes()
        {
            char[] buffer = new char[32];

            Assert.AreEqual(long.MaxValue.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                Render(buffer, long.MaxValue, true));

            Assert.AreEqual("-9223372036854775808", Render(buffer, long.MinValue, true));
        }

        static string Render(char[] buffer, long value, bool separators)
        {
            int length = FastNumberText.Format(value, buffer, separators);
            return new string(buffer, 0, length);
        }
    }
}
