using UnityEngine;
using Overdraft.Utility;

namespace Overdraft.Enemies
{
    /// <summary>
    /// DRIFTER (FR-301): walks straight at the player at constant speed. 1 HP, value 10.
    /// <para>
    /// No pathfinding, no avoidance, no flocking. The only steering beyond "toward the player"
    /// is a cheap separation push applied by EnemyManager, which exists purely so a clump reads
    /// as a crowd instead of as one enemy — not as intelligence.
    /// </para>
    /// </summary>
    public sealed class DrifterAI : EnemyBase
    {
        public override void TickAI(float deltaTime, in Vector3 playerPosition)
        {
            Vector3 position = transform.position;
            Vector3 toPlayer = MathX.NormalizeXZ(playerPosition - position);
            if (toPlayer == Vector3.zero) { return; }

            float speed = definition.moveSpeed * speedMultiplier;
            transform.position = position + toPlayer * (speed * deltaTime);

            if (visual != null)
            {
                visual.rotation = Quaternion.Euler(0f, MathX.AngleFromForwardXZ(toPlayer), 0f);
            }
        }
    }
}
