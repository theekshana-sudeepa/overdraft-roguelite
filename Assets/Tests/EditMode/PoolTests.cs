using NUnit.Framework;
using UnityEngine;
using Overdraft.Utility;
using UnityEngine.TestTools.Constraints;   // brings the AllocatingGCMemory() extension into scope
using Is = UnityEngine.TestTools.Constraints.Is;   // disambiguates from NUnit.Framework.Is

namespace Overdraft.Tests
{
    /// <summary>
    /// C3 says zero runtime Instantiate/Destroy after warm-up, and NFR-02 says zero per-frame
    /// allocation. Both live or die on this class.
    /// </summary>
    public sealed class PoolTests
    {
        GameObject prefabGO;
        GameObject parentGO;
        Transform prefab;
        Transform parent;

        [SetUp]
        public void SetUp()
        {
            prefabGO = new GameObject("PoolTestPrefab");
            parentGO = new GameObject("PoolTestParent");
            prefab = prefabGO.transform;
            parent = parentGO.transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (parentGO != null) { Object.DestroyImmediate(parentGO); }
            if (prefabGO != null) { Object.DestroyImmediate(prefabGO); }
        }

        [Test]
        public void Prewarm_CreatesCapacityInstances_AllInactive()
        {
            ObjectPool<Transform> pool = new ObjectPool<Transform>(prefab, parent, 16);

            Assert.AreEqual(16, pool.Capacity);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(16, parent.childCount);

            for (int i = 0; i < 16; i++)
            {
                Assert.IsFalse(pool.ItemAt(i).gameObject.activeSelf);
            }
        }

        [Test]
        public void Get_ActivatesAndTracks()
        {
            ObjectPool<Transform> pool = new ObjectPool<Transform>(prefab, parent, 4);

            Transform item = pool.Get();

            Assert.IsNotNull(item);
            Assert.IsTrue(item.gameObject.activeSelf);
            Assert.AreEqual(1, pool.ActiveCount);
        }

        [Test]
        public void Get_WhenExhausted_ReturnsNull_AndDoesNotGrow()
        {
            ObjectPool<Transform> pool = new ObjectPool<Transform>(prefab, parent, 3);

            Assert.IsNotNull(pool.Get());
            Assert.IsNotNull(pool.Get());
            Assert.IsNotNull(pool.Get());

            Assert.IsNull(pool.Get(), "exhaustion returns null — a growing pool hides spawn bugs");
            Assert.AreEqual(3, pool.Capacity);
            Assert.AreEqual(3, parent.childCount);
        }

        [Test]
        public void Return_Twice_IsSafe()
        {
            ObjectPool<Transform> pool = new ObjectPool<Transform>(prefab, parent, 4);
            Transform item = pool.Get();

            pool.Return(item);
            pool.Return(item);
            pool.Return(item);

            Assert.AreEqual(0, pool.ActiveCount, "a double return must not corrupt the free list");

            for (int i = 0; i < 4; i++)
            {
                Assert.IsNotNull(pool.Get(), "every slot must still be handed out exactly once");
            }
            Assert.IsNull(pool.Get());
        }

        [Test]
        public void Return_ForeignObject_IsIgnored()
        {
            ObjectPool<Transform> pool = new ObjectPool<Transform>(prefab, parent, 2);
            pool.Get();

            GameObject strayGO = new GameObject("Stray");
            try
            {
                pool.Return(strayGO.transform);
                Assert.AreEqual(1, pool.ActiveCount);
            }
            finally
            {
                Object.DestroyImmediate(strayGO);
            }
        }

        [Test]
        public void ReturnAll_ResetsEverything()
        {
            ObjectPool<Transform> pool = new ObjectPool<Transform>(prefab, parent, 8);
            for (int i = 0; i < 8; i++) { pool.Get(); }

            pool.ReturnAll();

            Assert.AreEqual(0, pool.ActiveCount);
            for (int i = 0; i < 8; i++)
            {
                Assert.IsFalse(pool.IsActiveAt(i));
                Assert.IsNotNull(pool.Get());
            }
        }

        [Test]
        public void GetAndReturn_DoNotAllocate()
        {
            ObjectPool<Transform> pool = new ObjectPool<Transform>(prefab, parent, 8);

            // Warm up first — the very first call touches lazily-initialised engine state.
            pool.Return(pool.Get());

            Assert.That(() =>
            {
                Transform item = pool.Get();
                pool.Return(item);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
