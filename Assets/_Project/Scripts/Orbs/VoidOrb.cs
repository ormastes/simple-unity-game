using UnityEngine;
using ElementalSiege.Elements;

namespace ElementalSiege.Orbs
{
    /// <summary>
    /// The Void orb — the mastery element for World 8. Absorbs the elemental
    /// properties of the last thing it touched and can use that absorbed element's
    /// ability. If no element is absorbed, creates a destructive micro black hole
    /// that annihilates everything in a tiny radius.
    /// </summary>
    public class VoidOrb : OrbBase
    {
        [Header("Void — Element Absorb")]

        /// <summary>
        /// Radius of the black hole destruction zone when no element is absorbed.
        /// Intentionally small — it's a precision tool, not area denial.
        /// </summary>
        [SerializeField] private float blackHoleRadius = 1.2f;

        /// <summary>Duration in seconds the black hole persists before collapsing.</summary>
        [SerializeField] private float blackHoleDuration = 1.5f;

        /// <summary>Damage dealt to all objects within the black hole radius.</summary>
        [SerializeField] private float blackHoleDamage = 100f;

        /// <summary>Inward pull force of the black hole on nearby objects.</summary>
        [SerializeField] private float blackHolePullForce = 600f;

        [Header("Absorb Visuals")]

        /// <summary>Prefab for the absorption visual when touching an elemental object.</summary>
        [SerializeField] private GameObject absorbEffectPrefab;

        /// <summary>Prefab for the black hole visual effect.</summary>
        [SerializeField] private GameObject blackHoleEffectPrefab;

        /// <summary>SpriteRenderer used to tint the orb to the absorbed element's color.</summary>
        [SerializeField] private SpriteRenderer orbSpriteRenderer;

        /// <summary>
        /// Registry of orb prefabs by element category, used to instantiate
        /// the absorbed element's orb for delegated ability activation.
        /// </summary>
        [SerializeField] private ElementType[] absorbableElements;

        // --- Runtime State ---

        private ElementCategory? _absorbedElement;
        private ElementType _absorbedElementType;
        private bool _hasAbsorbed;

        /// <summary>The currently absorbed element category, or null if none.</summary>
        public ElementCategory? AbsorbedElement => _absorbedElement;

        /// <summary>
        /// Element Absorb — if an element has been absorbed, activates that element's
        /// ability. Otherwise, creates a micro black hole that destroys everything
        /// in a tiny radius.
        /// </summary>
        protected override void OnAbilityActivated()
        {
            if (_hasAbsorbed && _absorbedElementType != null)
            {
                ActivateAbsorbedAbility();
            }
            else
            {
                ActivateBlackHole();
            }
        }

        /// <summary>
        /// Activates the ability of the absorbed element by spawning a temporary
        /// orb of that element type at the current position.
        /// </summary>
        private void ActivateAbsorbedAbility()
        {
            if (_absorbedElementType == null || _absorbedElementType.OrbPrefab == null)
            {
                // Fallback to black hole if we can't spawn the absorbed orb
                ActivateBlackHole();
                return;
            }

            Vector2 position = transform.position;

            // Spawn the absorbed element's orb at our position
            GameObject absorbedOrb = Instantiate(
                _absorbedElementType.OrbPrefab,
                position,
                Quaternion.identity
            );

            // Activate the spawned orb's ability immediately
            var orbBase = absorbedOrb.GetComponent<OrbBase>();
            if (orbBase != null)
            {
                // Launch it with our current velocity so it continues traveling
                orbBase.OnLaunch(Rb.linearVelocity * 0.5f);
                orbBase.TryActivateAbility();
            }

            // Show absorb release visual
            if (absorbEffectPrefab != null)
            {
                var effect = Instantiate(absorbEffectPrefab, position, Quaternion.identity);
                Destroy(effect, 2f);
            }
        }

        /// <summary>
        /// Creates a micro black hole that pulls in and destroys everything within
        /// a small radius. Used when no element has been absorbed.
        /// </summary>
        private void ActivateBlackHole()
        {
            Vector2 center = transform.position;

            // Spawn black hole visual
            GameObject blackHoleObj = null;
            if (blackHoleEffectPrefab != null)
            {
                blackHoleObj = Instantiate(blackHoleEffectPrefab, center, Quaternion.identity);
            }
            else
            {
                blackHoleObj = new GameObject("BlackHole_Temp");
                blackHoleObj.transform.position = center;
            }

            // Attach the black hole behavior
            var blackHole = blackHoleObj.AddComponent<BlackHoleBehavior>();
            blackHole.Initialize(blackHoleRadius, blackHoleDuration, blackHoleDamage, blackHolePullForce);
        }

        /// <summary>
        /// On collision, attempts to absorb the elemental properties of the struck object.
        /// Only absorbs once — the first elemental object touched determines the absorbed element.
        /// </summary>
        protected override void HandleImpact(Collision2D collision)
        {
            base.HandleImpact(collision);

            if (!_hasAbsorbed)
            {
                TryAbsorbElement(collision.gameObject);
            }
        }

        /// <summary>
        /// Checks the target for elemental properties and absorbs them if found.
        /// </summary>
        /// <param name="target">The GameObject to check for elemental properties.</param>
        private void TryAbsorbElement(GameObject target)
        {
            // Check if the target has an OrbBase with an element type
            var targetOrb = target.GetComponent<OrbBase>();
            if (targetOrb != null && targetOrb.ElementType != null)
            {
                AbsorbFromElementType(targetOrb.ElementType);
                return;
            }

            // Check if the target has an IElemental marker
            var elemental = target.GetComponent<IElemental>();
            if (elemental != null)
            {
                // Look up the ElementType from our absorbable elements list
                foreach (var element in absorbableElements)
                {
                    if (element != null && element.Category == elemental.ElementCategory)
                    {
                        AbsorbFromElementType(element);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Completes the absorption of an element type, updating visuals and state.
        /// </summary>
        /// <param name="elementType">The element type to absorb.</param>
        private void AbsorbFromElementType(ElementType elementType)
        {
            _hasAbsorbed = true;
            _absorbedElement = elementType.Category;
            _absorbedElementType = elementType;

            // Tint the orb to show what element was absorbed
            if (orbSpriteRenderer != null)
            {
                orbSpriteRenderer.color = Color.Lerp(
                    orbSpriteRenderer.color,
                    elementType.PrimaryColor,
                    0.6f
                );
            }

            // Spawn absorb visual
            if (absorbEffectPrefab != null)
            {
                var effect = Instantiate(absorbEffectPrefab, transform.position, Quaternion.identity);
                effect.transform.SetParent(transform);
                Destroy(effect, 1.5f);
            }
        }
    }

    /// <summary>
    /// Interface for objects that have an elemental affinity, allowing the
    /// Void orb to absorb their element on contact.
    /// </summary>
    public interface IElemental
    {
        /// <summary>The element category of this object.</summary>
        ElementCategory ElementCategory { get; }
    }

    /// <summary>
    /// MonoBehaviour that implements the micro black hole effect.
    /// Pulls in and destroys all objects within its radius over its duration.
    /// </summary>
    internal class BlackHoleBehavior : MonoBehaviour
    {
        private float _radius;
        private float _duration;
        private float _damage;
        private float _pullForce;
        private float _timer;
        private bool _initialized;

        /// <summary>
        /// Initializes the black hole with its gameplay parameters.
        /// </summary>
        /// <param name="radius">Destruction radius in world units.</param>
        /// <param name="duration">Lifetime in seconds.</param>
        /// <param name="damage">Damage dealt to objects within radius.</param>
        /// <param name="pullForce">Inward force applied to nearby objects.</param>
        public void Initialize(float radius, float duration, float damage, float pullForce)
        {
            _radius = radius;
            _duration = duration;
            _damage = damage;
            _pullForce = pullForce;
            _timer = 0f;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
                return;

            _timer += Time.deltaTime;

            if (_timer >= _duration)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 center = transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, _radius);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject)
                    continue;

                // Pull objects inward
                Rigidbody2D hitRb = hit.attachedRigidbody;
                if (hitRb != null && hitRb.bodyType != RigidbodyType2D.Static)
                {
                    Vector2 toCenter = center - (Vector2)hit.transform.position;
                    hitRb.AddForce(toCenter.normalized * _pullForce * Time.deltaTime, ForceMode2D.Force);
                }

                // Destroy objects that reach the core
                float dist = Vector2.Distance(center, hit.transform.position);
                if (dist < _radius * 0.3f)
                {
                    var destructible = hit.GetComponent<IDestructible>();
                    if (destructible != null)
                    {
                        destructible.TakeDamage(_damage, ElementCategory.Void);
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _radius);
            Gizmos.color = new Color(0f, 0f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, _radius * 0.3f);
        }
    }
}
