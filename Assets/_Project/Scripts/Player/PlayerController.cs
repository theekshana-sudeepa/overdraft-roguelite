using UnityEngine;
using Overdraft.Core;
using Overdraft.Economy;
using Overdraft.Utility;

namespace Overdraft.Player
{
    /// <summary>
    /// Movement, aim and facing (FR-101, FR-102). Ticked explicitly by RunController rather than
    /// from its own FixedUpdate, so the whole simulation has one deterministic order (C8).
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;
        [SerializeField] PlayerInputRouter input;
        [SerializeField] BankSystem bank;
        [SerializeField] DashModule dash;
        [SerializeField] Transform visual;

        Vector3 velocity;
        Vector3 aimDirection = Vector3.forward;
        float arenaRadius;

        public Vector3 Position => transform.position;
        public Vector3 AimDirection => aimDirection;
        public Vector3 Velocity => velocity;
        public float HitboxRadius => tuning.Player.hitboxRadius;

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }
            arenaRadius = tuning.Arena.floorRadius;
        }

        public void SetArenaRadius(float radius)
        {
            arenaRadius = radius;
        }

        public void Tick(float deltaTime)
        {
            aimDirection = input.AimDirection;

            // Facing is visual only, so it is safe to drive from the fixed step; at 900 deg/s it
            // clears the 720 deg/s floor in FR-102 with room for a slower value during tuning.
            if (visual != null)
            {
                float targetAngle = MathX.AngleFromForwardXZ(aimDirection);
                float currentAngle = visual.eulerAngles.y;
                float step = tuning.Player.facingTurnSpeed * deltaTime;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, step);
                visual.rotation = Quaternion.Euler(0f, newAngle, 0f);
            }

            if (dash.IsDashing)
            {
                // The dash owns position for its whole duration; it is a fixed distance, not an
                // impulse, so blending it with input velocity would make the distance variable.
                Vector3 dashed = dash.Advance(deltaTime, transform.position);
                transform.position = MathX.ClampToCircleXZ(dashed, arenaRadius);
                return;
            }

            if (bank.IsBanking)
            {
                // FR-206: input is read and discarded. Completely immobile — not "slowed",
                // because the whole tension is that the player has genuinely given up movement.
                velocity = Vector3.zero;
                return;
            }

            Vector2 raw = input.Move;
            Vector3 desired = new Vector3(raw.x, 0f, raw.y) * tuning.Player.moveSpeed;

            float rate = desired.sqrMagnitude > 0f
                ? tuning.Player.acceleration
                : tuning.Player.deceleration;

            velocity = Vector3.MoveTowards(velocity, desired, rate * deltaTime);

            Vector3 next = transform.position + velocity * deltaTime;
            next.y = 0f;

            Vector3 clamped = MathX.ClampToCircleXZ(next, arenaRadius);
            if (clamped != next)
            {
                // Kill the outward component instead of the whole vector, so sliding along the
                // boundary stays smooth rather than sticking.
                Vector3 outward = MathX.NormalizeXZ(clamped);
                float into = Vector3.Dot(velocity, outward);
                if (into > 0f) { velocity -= outward * into; }
            }

            transform.position = clamped;
        }

        public void ResetForRun()
        {
            transform.position = Vector3.zero;
            velocity = Vector3.zero;
            aimDirection = Vector3.forward;
            if (visual != null) { visual.rotation = Quaternion.identity; }
        }
    }
}
