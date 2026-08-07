using UnityEngine;
using Overdraft.Core;

namespace Overdraft.Enemies
{
    /// <summary>
    /// Shared enemy state and lifecycle. This is the ONE class in the project expected to be
    /// inherited from (CLAUDE.md §5) — everything else is sealed.
    /// </summary>
    public abstract class EnemyBase : MonoBehaviour
    {
        [SerializeField] protected Transform visual;
        [SerializeField] protected MeshRenderer meshRenderer;

        static MaterialPropertyBlock propertyBlock;

        protected EnemyDefinition definition;
        protected TuningConfig tuning;
        protected float speedMultiplier = 1f;
        protected float visualScale = 1f;
        protected Color baseColor = Color.white;

        int hp;

        public bool IsElite { get; private set; }
        public bool IsAlive => hp > 0;
        public float HitboxRadius { get; private set; }
        public int ContactDamage => definition.contactDamage;
        public Generation.EnemyType Type => definition.type;

        /// <summary>
        /// The manager that owns this instance's pool. Set once, right after Spawn(), so
        /// subclasses (Spitter's puddle, Splitter's split) can call back into shared pools
        /// without a static lookup or a new service-locator entry.
        /// </summary>
        public EnemyManager Owner { get; internal set; }

        /// <summary>
        /// Prepares a pooled instance for use. Never allocates: colour goes through a shared
        /// MaterialPropertyBlock so all enemies keep sharing one material (NFR-04).
        /// </summary>
        public void Spawn(EnemyDefinition def, TuningConfig config, Vector3 position, bool elite, float riskSpeedMultiplier)
        {
            definition = def;
            tuning = config;
            IsElite = elite;

            float hpScale = elite ? config.Elite.hpMultiplier : 1f;
            visualScale = elite ? config.Elite.scaleMultiplier : 1f;

            hp = Mathf.Max(1, Mathf.RoundToInt(def.maxHP * hpScale));
            HitboxRadius = def.hitboxRadius * visualScale;
            speedMultiplier = riskSpeedMultiplier * (elite ? config.Elite.speedMultiplier : 1f);

            transform.position = new Vector3(position.x, 0f, position.z);
            if (visual != null)
            {
                visual.localScale = Vector3.one * visualScale;
                visual.localRotation = Quaternion.identity;
            }

            baseColor = elite ? Palette.Intensity(def.bodyColor, config.Elite.emissionBoost) : def.bodyColor;
            ApplyColor(baseColor);
            OnSpawned();
        }

        /// <summary>Per-archetype behaviour. Runs on the fixed step only.</summary>
        public abstract void TickAI(float deltaTime, in Vector3 playerPosition);

        protected virtual void OnSpawned() { }

        /// <summary>
        /// Called once, by EnemyManager, right before this instance returns to its pool.
        /// Splitter spawns its children here; Spitter spawns its puddle here. Default is a
        /// no-op — this is also what guarantees a Splitter's own split-children (Drifters)
        /// never themselves split (FR-303).
        /// </summary>
        internal virtual void OnDeath(in Vector3 position) { }

        /// <summary>
        /// Applies damage. Returns true if this hit killed the enemy, in which case the caller
        /// (EnemyManager) raises EnemyKilled and returns the instance to its pool.
        /// </summary>
        public bool TakeDamage(int amount)
        {
            if (hp <= 0) { return false; }

            hp -= amount;
            if (hp > 0) { return false; }

            hp = 0;
            return true;
        }

        /// <summary>The CHARGE value of this kill before the multiplier is applied (FR-201).</summary>
        public int ScoreValue
        {
            get
            {
                float scale = IsElite ? tuning.Elite.valueMultiplier : 1f;
                return Mathf.RoundToInt(definition.scoreValue * scale);
            }
        }

        /// <summary>
        /// Re-applies the base colour at a brightness multiplier, for telegraph flashes
        /// (e.g. Splitter's windup). 1.0 restores the resting colour exactly.
        /// </summary>
        protected void SetEmissionMultiplier(float multiplier)
        {
            ApplyColor(Palette.Intensity(baseColor, multiplier));
        }

        void ApplyColor(Color color)
        {
            if (meshRenderer == null) { return; }
            if (propertyBlock == null) { propertyBlock = new MaterialPropertyBlock(); }

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ShaderIds.BaseColor, color);
            propertyBlock.SetFloat(ShaderIds.OutlineWidth, IsElite ? tuning.Elite.outlineWidth : 0f);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
