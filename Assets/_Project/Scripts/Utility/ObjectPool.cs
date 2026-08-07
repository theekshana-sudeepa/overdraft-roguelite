using System.Collections.Generic;
using UnityEngine;

namespace Overdraft.Utility
{
    /// <summary>
    /// Fixed-capacity component pool (docs/01 §6). Everything in the game comes from one of
    /// these — there is zero runtime Instantiate/Destroy after warm-up (C3).
    /// <para>
    /// Get() returns null when the pool is exhausted and the pool does NOT grow. That is
    /// deliberate: a silently growing pool hides a spawn bug until it becomes a framerate bug.
    /// Every caller must null-check.
    /// </para>
    /// </summary>
    public sealed class ObjectPool<T> where T : Component
    {
        readonly T[] items;
        readonly int[] freeStack;
        readonly bool[] isActive;

        // instanceID -> pool index. Dictionary<int,int> lookups do not allocate, and this keeps
        // the pool usable with any Component without forcing a marker script onto the prefab.
        readonly Dictionary<int, int> indexOf;

        int freeCount;

        public int Capacity => items.Length;
        public int ActiveCount => items.Length - freeCount;

        public ObjectPool(T prefab, Transform parent, int capacity)
        {
            if (prefab == null) { throw new System.ArgumentNullException(nameof(prefab)); }
            if (capacity <= 0) { throw new System.ArgumentOutOfRangeException(nameof(capacity)); }

            items = new T[capacity];
            freeStack = new int[capacity];
            isActive = new bool[capacity];
            indexOf = new Dictionary<int, int>(capacity);

            for (int i = 0; i < capacity; i++)
            {
                T instance = Object.Instantiate(prefab, parent);
                instance.gameObject.SetActive(false);
                instance.name = prefab.name + "_" + i;

                items[i] = instance;
                indexOf[instance.GetInstanceID()] = i;

                // Fill the stack in reverse so the first Get() hands back index 0, which makes
                // pool state readable in the hierarchy while debugging.
                freeStack[i] = capacity - 1 - i;
                isActive[i] = false;
            }

            freeCount = capacity;
        }

        /// <summary>Activates and returns a pooled instance, or null if the pool is exhausted.</summary>
        public T Get()
        {
            if (freeCount == 0) { return null; }

            freeCount--;
            int index = freeStack[freeCount];
            isActive[index] = true;

            T item = items[index];
            item.gameObject.SetActive(true);
            return item;
        }

        /// <summary>Deactivates and returns an instance. Safe against double-return and foreign objects.</summary>
        public void Return(T item)
        {
            if (item == null) { return; }
            if (!indexOf.TryGetValue(item.GetInstanceID(), out int index)) { return; }
            if (!isActive[index]) { return; }

            isActive[index] = false;
            item.gameObject.SetActive(false);

            freeStack[freeCount] = index;
            freeCount++;
        }

        /// <summary>Returns everything. Used on run restart so a retry costs no allocations.</summary>
        public void ReturnAll()
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (!isActive[i]) { continue; }
                isActive[i] = false;
                items[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < items.Length; i++)
            {
                freeStack[i] = items.Length - 1 - i;
            }
            freeCount = items.Length;
        }

        /// <summary>
        /// Direct indexed access for the fixed-step update loop. Callers iterate 0..Capacity and
        /// skip inactive slots — cheaper and more cache-friendly than keeping a live list.
        /// </summary>
        public T ItemAt(int index) => items[index];

        public bool IsActiveAt(int index) => isActive[index];
    }
}
