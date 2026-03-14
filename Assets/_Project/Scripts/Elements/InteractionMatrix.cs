using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.Elements
{
    /// <summary>
    /// ScriptableObject that holds the complete matrix of element interactions.
    /// Acts as a lookup table: given two element categories, returns the interaction
    /// definition (if one exists). Designed to be a single shared asset referenced
    /// by the game manager and combo detection systems.
    /// </summary>
    [CreateAssetMenu(fileName = "InteractionMatrix", menuName = "Elemental Siege/Interaction Matrix", order = 2)]
    public class InteractionMatrix : ScriptableObject
    {
        /// <summary>All registered element interactions in the game.</summary>
        [SerializeField] private List<ElementInteraction> interactions = new List<ElementInteraction>();

        /// <summary>
        /// Internal cache built on first access for O(1) lookups.
        /// Key is a canonical pair of categories (lower enum value first).
        /// </summary>
        private Dictionary<(ElementCategory, ElementCategory), ElementInteraction> _cache;

        /// <summary>Read-only access to the raw interaction list.</summary>
        public IReadOnlyList<ElementInteraction> Interactions => interactions;

        /// <summary>
        /// Returns the interaction definition for the given element pair, or null
        /// if no interaction is defined. Lookup order is commutative: (Fire, Ice)
        /// returns the same result as (Ice, Fire).
        /// </summary>
        /// <param name="a">First element category.</param>
        /// <param name="b">Second element category.</param>
        /// <returns>The matching ElementInteraction, or null if none exists.</returns>
        public ElementInteraction GetInteraction(ElementCategory a, ElementCategory b)
        {
            EnsureCacheBuilt();

            var key = MakeKey(a, b);
            _cache.TryGetValue(key, out var interaction);
            return interaction;
        }

        /// <summary>
        /// Quick boolean check for whether two element categories have a defined combo.
        /// </summary>
        /// <param name="a">First element category.</param>
        /// <param name="b">Second element category.</param>
        /// <returns>True if a combo interaction exists for this pair.</returns>
        public bool HasCombo(ElementCategory a, ElementCategory b)
        {
            return GetInteraction(a, b) != null;
        }

        /// <summary>
        /// Creates a canonical key so that (A, B) and (B, A) map to the same entry.
        /// The element with the lower enum integer value is always first.
        /// </summary>
        private (ElementCategory, ElementCategory) MakeKey(ElementCategory a, ElementCategory b)
        {
            return a <= b ? (a, b) : (b, a);
        }

        /// <summary>
        /// Builds the lookup cache from the serialized interaction list.
        /// Called lazily on first access and can be rebuilt via <see cref="RebuildCache"/>.
        /// </summary>
        private void EnsureCacheBuilt()
        {
            if (_cache != null)
                return;

            RebuildCache();
        }

        /// <summary>
        /// Forces a rebuild of the internal lookup cache. Call this if the
        /// interaction list is modified at runtime.
        /// </summary>
        public void RebuildCache()
        {
            _cache = new Dictionary<(ElementCategory, ElementCategory), ElementInteraction>();

            foreach (var interaction in interactions)
            {
                if (interaction == null || interaction.ElementA == null || interaction.ElementB == null)
                    continue;

                var key = MakeKey(interaction.ElementA.Category, interaction.ElementB.Category);

                if (_cache.ContainsKey(key))
                {
                    Debug.LogWarning(
                        $"[InteractionMatrix] Duplicate interaction for " +
                        $"{key.Item1} + {key.Item2}. Keeping first entry.");
                    continue;
                }

                _cache[key] = interaction;
            }
        }

        private void OnEnable()
        {
            // Force cache rebuild when the asset is loaded or recompiled in the editor.
            _cache = null;
        }
    }
}
