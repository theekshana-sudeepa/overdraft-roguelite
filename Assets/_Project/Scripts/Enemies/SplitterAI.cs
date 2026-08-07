using UnityEngine;
using Overdraft.Utility;

namespace Overdraft.Enemies
{
    /// <summary>
    /// SPLITTER (FR-303): approaches slowly, telegraphs with a visible windup, then charges the
    /// player in a straight line. On death, spawns Drifters scattered around its death position.
    /// <para>
    /// Split Drifters never split again — that guarantee comes from DrifterAI.OnDeath being the
    /// unmodified no-op default on EnemyBase, not from any flag carried on the instance.
    /// </para>
    /// </summary>
    public sealed class SplitterAI : EnemyBase
    {
        enum State { Approach, Windup, Charging }

        State state;
        float stateTimer;
        float telegraphElapsed;
        Vector3 chargeDirection;

        protected override void OnSpawned()
        {
            state = State.Approach;
            stateTimer = definition.chargeCooldown;
            telegraphElapsed = 0f;
        }

        public override void TickAI(float deltaTime, in Vector3 playerPosition)
        {
            switch (state)
            {
                case State.Approach:
                    TickApproach(deltaTime, playerPosition);
                    break;
                case State.Windup:
                    TickWindup(deltaTime, playerPosition);
                    break;
                case State.Charging:
                    TickCharging(deltaTime);
                    break;
            }
        }

        internal override void OnDeath(in Vector3 position)
        {
            if (Owner == null) { return; }

            int count = Mathf.Max(0, definition.splitCount);
            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f / Mathf.Max(1, count)) * i;
                Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * definition.splitScatterRadius;

                // Pool exhaustion returns null — skipped silently, never grows the pool
                // (docs/01 §6, QA case 40).
                Owner.Spawn(Generation.EnemyType.Drifter, position + offset, false);
            }
        }

        void TickApproach(float deltaTime, in Vector3 playerPosition)
        {
            Vector3 position = transform.position;
            Vector3 toPlayer = MathX.NormalizeXZ(playerPosition - position);
            float speed = definition.approachSpeed * speedMultiplier;
            transform.position = position + toPlayer * (speed * deltaTime);
            FaceDirection(toPlayer);

            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                state = State.Windup;
                stateTimer = definition.windupDuration;
                telegraphElapsed = 0f;
            }
        }

        void TickWindup(float deltaTime, in Vector3 playerPosition)
        {
            telegraphElapsed += deltaTime;
            float t = definition.windupDuration > 0f ? Mathf.Clamp01(telegraphElapsed / definition.windupDuration) : 1f;

            // Visible telegraph: a scale pulse plus a brightening flash, so the attack is
            // readable on first encounter without needing audio (M2-T02 AC).
            if (visual != null)
            {
                float pulse = 1f + Mathf.Sin(t * Mathf.PI * 6f) * 0.15f * t;
                visual.localScale = Vector3.one * (visualScale * pulse);
            }
            SetEmissionMultiplier(1f + t * 1.5f);

            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                chargeDirection = MathX.NormalizeXZ(playerPosition - transform.position);
                if (chargeDirection == Vector3.zero) { chargeDirection = Vector3.forward; }

                state = State.Charging;
                stateTimer = definition.chargeDuration;

                if (visual != null) { visual.localScale = Vector3.one * visualScale; }
                SetEmissionMultiplier(1f);
            }
        }

        void TickCharging(float deltaTime)
        {
            float speed = definition.moveSpeed * speedMultiplier;   // moveSpeed is the CHARGE speed
            transform.position += chargeDirection * (speed * deltaTime);
            FaceDirection(chargeDirection);

            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                state = State.Approach;
                stateTimer = definition.chargeCooldown;
            }
        }

        void FaceDirection(in Vector3 direction)
        {
            if (visual == null || direction == Vector3.zero) { return; }
            visual.rotation = Quaternion.Euler(0f, MathX.AngleFromForwardXZ(direction), 0f);
        }
    }
}
