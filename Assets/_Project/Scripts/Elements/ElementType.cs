using UnityEngine;

namespace ElementalSiege.Elements
{
    /// <summary>
    /// Defines the category of an element, used for interaction lookups and effect resolution.
    /// </summary>
    public enum ElementCategory
    {
        Stone,
        Fire,
        Ice,
        Lightning,
        Wind,
        Crystal,
        Gravity,
        Void
    }

    /// <summary>
    /// ScriptableObject that defines an elemental type with all associated visual,
    /// audio, and gameplay properties. Each orb references one of these to determine
    /// its behavior, appearance, and interactions.
    /// </summary>
    [CreateAssetMenu(fileName = "NewElement", menuName = "Elemental Siege/Element Type", order = 0)]
    public class ElementType : ScriptableObject
    {
        [Header("Identity")]

        /// <summary>Display name of the element shown in UI.</summary>
        [SerializeField] private string elementName;

        /// <summary>Category used for interaction matrix lookups.</summary>
        [SerializeField] private ElementCategory category;

        /// <summary>Human-readable description of the element's properties and lore.</summary>
        [SerializeField, TextArea(2, 5)] private string description;

        [Header("Visuals")]

        /// <summary>Primary color used for orb tinting, UI highlights, and particle systems.</summary>
        [SerializeField] private Color primaryColor = Color.white;

        /// <summary>Secondary color for gradients, trails, and accent effects.</summary>
        [SerializeField] private Color secondaryColor = Color.gray;

        /// <summary>Icon sprite displayed in the orb selection HUD.</summary>
        [SerializeField] private Sprite icon;

        [Header("Prefabs")]

        /// <summary>Prefab instantiated when this element's orb is spawned.</summary>
        [SerializeField] private GameObject orbPrefab;

        /// <summary>Prefab instantiated at the point of impact for visual feedback.</summary>
        [SerializeField] private GameObject impactEffectPrefab;

        [Header("Audio")]

        /// <summary>Sound played when the orb is launched from the slingshot.</summary>
        [SerializeField] private AudioClip launchSound;

        /// <summary>Sound played when the orb collides with a structure or surface.</summary>
        [SerializeField] private AudioClip impactSound;

        /// <summary>Sound played when the orb's mid-flight ability is activated.</summary>
        [SerializeField] private AudioClip abilitySound;

        [Header("Gameplay")]

        /// <summary>Base damage dealt on direct impact before multipliers.</summary>
        [SerializeField] private float baseDamage = 10f;

        /// <summary>Radius of the element's special ability effect in world units.</summary>
        [SerializeField] private float abilityRadius = 3f;

        // --- Public Properties ---

        public string ElementName => elementName;
        public ElementCategory Category => category;
        public string Description => description;
        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
        public Sprite Icon => icon;
        public GameObject OrbPrefab => orbPrefab;
        public GameObject ImpactEffectPrefab => impactEffectPrefab;
        public AudioClip LaunchSound => launchSound;
        public AudioClip ImpactSound => impactSound;
        public AudioClip AbilitySound => abilitySound;
        public float BaseDamage => baseDamage;
        public float AbilityRadius => abilityRadius;
    }
}
