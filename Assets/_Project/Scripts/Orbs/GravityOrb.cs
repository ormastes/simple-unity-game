using System.Collections.Generic;
using UnityEngine;
using ElementalSiege.Elements;

namespace ElementalSiege.Orbs
{
    /// <summary>
    /// The Gravity orb — creates a temporary gravity well that pulls nearby objects
    /// toward its center. Excellent for pulling structures apart, redirecting debris,
    /// and creating chain reactions by clumping objects together.
    /// </summary>
    public class GravityOrb : OrbBase
    {
        [Header("Gravity — Gravity Well")]

        /// <summary>Radius of the gravity well's influence in world units.</summary>
        [SerializeField] private float wellRadius = 5f;

        /// <summary>Maximum attractive force applied to objects at the well's edge.</summary>
        [SerializeField] private float wellForce = 300f;

        /// <summary>Duration in seconds the gravity well remains active.</summary>
        [SerializeField] private float wellDuration = 3f;

        /// <summary>
        /// Force curve from center (0) to edge (1). Allows tuning the force profile
        /// so objects accelerate as they approach or feel constant pull.
        /// </summary>
        [SerializeField] private AnimationCurve forceFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);

        [Header("Gravity Effects")]

        /// <summary>Prefab for the gravity well visual (distortion, particle vortex).</summary>
        [SerializeField] private GameObject wellEffectPrefab;

        /// <summary>Prefab for the activation burst visual.</summary>
        [SerializeField] private GameObject activationBurstPrefab;

        private bool _wellActive;
        private float _wellTimer;
        private Vector2 _wellPosition;
        private GameObject _activeWellEffect;

        protected override void Update()
        {
            base.Update();

            if (_wellActive)
            {
                _wellTimer -= Time.deltaTime;

                if (_wellTimer <= 0f)
                {
                    DeactivateWell();
                    return;
                }

                ApplyGravitationalPull();
            }
        }

        /// <summary>
        /// Gravity Well — creates a fixed-point gravitational attractor at the orb's
        /// current position. For the configured duration, all nearby rigidbodies are
        /// pulled toward the well center.
        /// </summary>
        protected override void OnAbilityActivated()
        {
            _wellPosition = transform.position;
            _wellActive = true;
            _wellTimer = wellDuration;

            // Stop the orb's movement so the well stays in place
            Rb.linearVelocity = Vector2.zero;
            Rb.angularVelocity = 0f;
            Rb.bodyType = RigidbodyType2D.Kinematic;

            // Spawn activation burst
            if (activationBurstPrefab != null)
            {
                var burst = Instantiate(activationBurstPrefab, _wellPosition, Quaternion.identity);
                Destroy(burst, 2f);
            }

            // Spawn persistent well effect
            if (wellEffectPrefab != null)
            {
                _activeWellEffect = Instantiate(wellEffectPrefab, _wellPosition, Quaternion.identity);
                _activeWellEffect.transform.localScale = Vector3.one * (wellRadius * 2f);
            }
        }

        /// <summary>
        /// Applies gravitational pull to all rigidbodies within the well radius.
        /// Force is calculated per-frame using the force falloff curve.
        /// </summary>
        private void ApplyGravitationalPull()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(_wellPosition, wellRadius);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject)
                    continue;

                Rigidbody2D hitRb = hit.attachedRigidbody;
                if (hitRb == null || hitRb.bodyType == RigidbodyType2D.Static)
                    continue;

                Vector2 toWell = _wellPosition - (Vector2)hit.transform.position;
                float distance = toWell.magnitude;

                if (distance < 0.1f)
                    continue;

                float normalizedDist = distance / wellRadius;
                float forceMultiplier = forceFalloff.Evaluate(normalizedDist);

                Vector2 force = toWell.normalized * wellForce * forceMultiplier * Time.deltaTime;
                hitRb.AddForce(force, ForceMode2D.Force);
            }
        }

        /// <summary>
        /// Deactivates the gravity well and cleans up the visual effect.
        /// </summary>
        private void DeactivateWell()
        {
            _wellActive = false;

            if (_activeWellEffect != null)
            {
                Destroy(_activeWellEffect);
                _activeWellEffect = null;
            }

            // Re-enable physics so the orb can settle naturally
            Rb.bodyType = RigidbodyType2D.Dynamic;
        }

        private void OnDestroy()
        {
            // Clean up well effect if orb is destroyed while well is active
            if (_activeWellEffect != null)
            {
                Destroy(_activeWellEffect);
            }
        }

        /// <summary>
        /// Draws the gravity well radius in the Scene view for level design.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
            Vector3 center = _wellActive ? (Vector3)(Vector2)_wellPosition : transform.position;
            Gizmos.DrawWireSphere(center, wellRadius);
        }
    }
}
