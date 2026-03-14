using UnityEngine;

namespace ElementalSiege.Elements
{
    /// <summary>
    /// ScriptableObject defining the result of two elements interacting.
    /// When elementA's orb collides with or overlaps elementB's residual effect,
    /// a combo is triggered with the specified visual, audio, and damage properties.
    /// </summary>
    [CreateAssetMenu(fileName = "NewInteraction", menuName = "Elemental Siege/Element Interaction", order = 1)]
    public class ElementInteraction : ScriptableObject
    {
        [Header("Input Elements")]

        /// <summary>First element involved in the interaction.</summary>
        [SerializeField] private ElementType elementA;

        /// <summary>Second element involved in the interaction.</summary>
        [SerializeField] private ElementType elementB;

        [Header("Result")]

        /// <summary>
        /// The resulting effect category (e.g., Fire + Ice = Steam).
        /// This drives what environmental effect is spawned.
        /// </summary>
        [SerializeField] private ElementCategory resultEffect;

        /// <summary>Display name of the combo shown in the combo popup UI.</summary>
        [SerializeField] private string comboName;

        /// <summary>Description of what the combo does, shown in the combo almanac.</summary>
        [SerializeField, TextArea(2, 4)] private string comboDescription;

        [Header("Gameplay")]

        /// <summary>
        /// Multiplier applied to the combined base damage of both elements.
        /// Values greater than 1.0 indicate a synergistic combo.
        /// </summary>
        [SerializeField] private float damageMultiplier = 1.5f;

        [Header("Effects")]

        /// <summary>Prefab spawned at the interaction point for the combo visual.</summary>
        [SerializeField] private GameObject comboEffectPrefab;

        /// <summary>Sound played when the combo triggers.</summary>
        [SerializeField] private AudioClip comboSound;

        // --- Public Properties ---

        public ElementType ElementA => elementA;
        public ElementType ElementB => elementB;
        public ElementCategory ResultEffect => resultEffect;
        public string ComboName => comboName;
        public string ComboDescription => comboDescription;
        public float DamageMultiplier => damageMultiplier;
        public GameObject ComboEffectPrefab => comboEffectPrefab;
        public AudioClip ComboSound => comboSound;
    }
}
