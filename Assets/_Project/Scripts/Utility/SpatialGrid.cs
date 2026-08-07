using UnityEngine;

namespace Overdraft.Utility
{
    /// <summary>
    /// Uniform spatial hash over the arena, rebuilt every FixedUpdate into pre-allocated int
    /// buckets (docs/01 §7). Broad phase only; CircleOverlap does the narrow phase.
    /// <para>
    /// Uses a two-pass counting sort rather than fixed-capacity buckets. A capacity-limited
    /// bucket would silently drop contacts exactly when the game is most crowded — during a
    /// clump — which is the worst possible time to miss a collision.
    /// </para>
    /// </summary>
    public sealed class SpatialGrid
    {
        readonly float invCellSize;
        readonly float originOffset;
        readonly int dimension;
        readonly int cellCount;

        readonly int[] cellStart;    // dimension^2 + 1, prefix sums
        readonly int[] cellCursor;   // dimension^2, scatter cursors
        readonly int[] sortedIds;    // maxItems
        readonly int[] stagedCell;   // maxItems
        readonly int[] stagedId;     // maxItems

        int stagedCount;
        bool built;

        public int Dimension => dimension;
        public int StagedCount => stagedCount;

        public SpatialGrid(float worldRadius, float cellSize, int maxItems)
        {
            invCellSize = 1f / cellSize;

            // One cell of slack on each side so an actor sitting exactly on the boundary
            // still lands inside the grid rather than being clamped into a neighbour.
            originOffset = worldRadius + cellSize;
            dimension = Mathf.CeilToInt(originOffset * 2f * invCellSize);
            cellCount = dimension * dimension;

            cellStart = new int[cellCount + 1];
            cellCursor = new int[cellCount];
            sortedIds = new int[maxItems];
            stagedCell = new int[maxItems];
            stagedId = new int[maxItems];
        }

        public void BeginBuild()
        {
            stagedCount = 0;
            built = false;
            System.Array.Clear(cellStart, 0, cellStart.Length);
        }

        /// <summary>Stages one item. id is whatever index the caller wants back from Query.</summary>
        public void Add(int id, float x, float z)
        {
            if (stagedCount >= stagedId.Length) { return; }

            int cell = CellIndex(x, z);
            stagedId[stagedCount] = id;
            stagedCell[stagedCount] = cell;
            stagedCount++;

            cellStart[cell + 1]++;
        }

        public void EndBuild()
        {
            for (int i = 0; i < cellCount; i++)
            {
                cellStart[i + 1] += cellStart[i];
                cellCursor[i] = cellStart[i];
            }

            for (int i = 0; i < stagedCount; i++)
            {
                int cell = stagedCell[i];
                sortedIds[cellCursor[cell]] = stagedId[i];
                cellCursor[cell]++;
            }

            built = true;
        }

        /// <summary>
        /// Writes the ids of every item in cells overlapping the query circle into results, and
        /// returns how many were written. Results are a superset — the caller still runs
        /// CircleOverlap on each. Never allocates.
        /// </summary>
        public int Query(float x, float z, float radius, int[] results)
        {
            if (!built) { return 0; }

            int minX = ClampAxis(Mathf.FloorToInt((x + originOffset - radius) * invCellSize));
            int maxX = ClampAxis(Mathf.FloorToInt((x + originOffset + radius) * invCellSize));
            int minZ = ClampAxis(Mathf.FloorToInt((z + originOffset - radius) * invCellSize));
            int maxZ = ClampAxis(Mathf.FloorToInt((z + originOffset + radius) * invCellSize));

            int written = 0;
            for (int cz = minZ; cz <= maxZ; cz++)
            {
                int rowBase = cz * dimension;
                for (int cx = minX; cx <= maxX; cx++)
                {
                    int cell = rowBase + cx;
                    int start = cellStart[cell];
                    int end = cellStart[cell + 1];

                    for (int i = start; i < end; i++)
                    {
                        if (written >= results.Length) { return written; }
                        results[written] = sortedIds[i];
                        written++;
                    }
                }
            }

            return written;
        }

        int CellIndex(float x, float z)
        {
            int cx = ClampAxis(Mathf.FloorToInt((x + originOffset) * invCellSize));
            int cz = ClampAxis(Mathf.FloorToInt((z + originOffset) * invCellSize));
            return cz * dimension + cx;
        }

        int ClampAxis(int v)
        {
            if (v < 0) { return 0; }
            if (v >= dimension) { return dimension - 1; }
            return v;
        }
    }
}
