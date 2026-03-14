using UnityEngine;
using ElementalSiege.Elements;

namespace ElementalSiege.Orbs
{
    /// <summary>
    /// The Stone orb — the tutorial element. Simple, heavy, and powerful.
    /// Its ability is a gravity-boosted power dive that slams into structures
    /// with devastating force. No fancy elemental effects; just raw impact.
    /// </summary>
    public class StoneOrb : OrbBase
    {
        [Header("Stone — Heavy Slam")]

        /// <summary>Gravity scale multiplier applied during the power dive.</summary>
        [SerializeField] private float slamGravityScale = 8f;

        /// <summary>Duration in seconds the boosted gravity remains active.</summary>
        [SerializeField] private float slamDuration = 1.5f;

        /// <summary>Additional damage multiplier applied to impacts during the slam.</summary>
        [SerializeField] private float slamDamageMultiplier = 2f;

        /// <summary>Particle system prefab for the simple burst on impact.</summary>
        [SerializeField] private GameObject impactBurstPrefab;

        private float _originalGravityScale;
        private float _slamTimer;
        private bool _isSlamming;

        protected override void Awake()
        {
            base.Awake();
            _originalGravityScale = Rb.gravityScale;
        }

        protected override void Update()
        {
            base.Update();

            if (_isSlamming)
            {
                _slamTimer -= Time.deltaTime;
                if (_slamTimer <= 0f)
                {
                    EndSlam();
                }
            }
        }

        /// <summary>
        /// Heavy Slam — increases gravity scale for a devastating power dive.
        /// The orb plummets downward with massively increased gravitational pull.
        /// </summary>
        protected override void OnAbilityActivated()
        {
            _isSlamming = true;
            _slamTimer = slamDuration;
            Rb.gravityScale = slamGravityScale;
        }

        /// <summary>
        /// Restores normal gravity when the slam duration expires.
        /// </summary>
        private void EndSlam()
        {
            _isSlamming = false;
            Rb.gravityScale = _originalGravityScale;
        }

        /// <summary>
        /// Applies boosted damage during a slam and spawns a simple particle burst.
        /// </summary>
        protected override void HandleImpact(Collision2D collision)
        {
            // Spawn the simple impact burst
            if (impactBurstPrefab != null && collision.contactCount > 0)
            {
                Vector2 contactPoint = collision.GetContact(0).point;
                var burst = Instantiate(impactBurstPrefab, contactPoint, Quaternion.identity);
                Destroy(burst, 3f);
            }

            // Apply slam damage bonus
            if (_isSlamming && ElementType != null)
            {
                var destructible = collision.gameObject.GetComponent<IDestructible>();
                if (destructible != null)
                {
                    float bonusDamage = ElementType.BaseDamage * (slamDamageMultiplier - 1f);
                    destructible.TakeDamage(bonusDamage, ElementType.Category);
                }
            }

            base.HandleImpact(collision);
        }
    }
}
