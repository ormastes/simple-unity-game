using NUnit.Framework;

namespace ElementalSiege.Tests.EditMode
{
    [TestFixture]
    public class InteractionMatrixTests
    {
        private InteractionMatrix _matrix;

        [SetUp]
        public void SetUp()
        {
            _matrix = new InteractionMatrix();
        }

        [Test]
        public void GetInteraction_ReturnsCorrectCombo_FireAndIce()
        {
            ElementCombo combo = _matrix.GetInteraction(ElementCategory.Fire, ElementCategory.Ice);

            Assert.IsNotNull(combo, "Fire + Ice should produce a valid combo");
            Assert.AreEqual("Steam Explosion", combo.Name);
            Assert.Greater(combo.DamageMultiplier, 1.0f,
                "Fire + Ice combo should have a damage multiplier greater than 1");
        }

        [Test]
        public void GetInteraction_IsCommutative_IceAndFire()
        {
            ElementCombo comboAB = _matrix.GetInteraction(ElementCategory.Fire, ElementCategory.Ice);
            ElementCombo comboBA = _matrix.GetInteraction(ElementCategory.Ice, ElementCategory.Fire);

            Assert.IsNotNull(comboAB);
            Assert.IsNotNull(comboBA);
            Assert.AreEqual(comboAB.Name, comboBA.Name,
                "Element interactions should be commutative (order should not matter)");
            Assert.AreEqual(comboAB.DamageMultiplier, comboBA.DamageMultiplier,
                "Damage multiplier should be the same regardless of element order");
        }

        [Test]
        public void HasCombo_ReturnsTrue_ForValidCombo()
        {
            bool hasCombo = _matrix.HasCombo(ElementCategory.Fire, ElementCategory.Ice);

            Assert.IsTrue(hasCombo, "Fire and Ice should have a valid combo");
        }

        [Test]
        public void HasCombo_ReturnsFalse_ForSameElement()
        {
            bool hasCombo = _matrix.HasCombo(ElementCategory.Fire, ElementCategory.Fire);

            Assert.IsFalse(hasCombo,
                "Same element should not produce a combo");
        }

        [Test]
        public void GetInteraction_ReturnsNull_ForNoCombo()
        {
            ElementCombo combo = _matrix.GetInteraction(ElementCategory.Stone, ElementCategory.Stone);

            Assert.IsNull(combo, "Same element pairing should return null");
        }

        [Test]
        public void AllInteractions_HavePositiveMultiplier()
        {
            var allCombos = _matrix.GetAllCombos();

            foreach (var combo in allCombos)
            {
                Assert.Greater(combo.DamageMultiplier, 0f,
                    $"Combo '{combo.Name}' should have a positive damage multiplier, got {combo.DamageMultiplier}");
            }
        }
    }

    /// <summary>
    /// Element categories used across the game.
    /// </summary>
    public enum ElementCategory
    {
        Fire,
        Ice,
        Wind,
        Stone,
        Lightning
    }

    /// <summary>
    /// Represents an elemental combo result.
    /// </summary>
    public class ElementCombo
    {
        public string Name;
        public float DamageMultiplier;
        public ElementCategory ResultElement;

        public ElementCombo(string name, float damageMultiplier, ElementCategory resultElement)
        {
            Name = name;
            DamageMultiplier = damageMultiplier;
            ResultElement = resultElement;
        }
    }

    /// <summary>
    /// Stub InteractionMatrix for test compilation.
    /// </summary>
    public class InteractionMatrix
    {
        private readonly System.Collections.Generic.Dictionary<(ElementCategory, ElementCategory), ElementCombo> _combos;

        public InteractionMatrix()
        {
            _combos = new System.Collections.Generic.Dictionary<(ElementCategory, ElementCategory), ElementCombo>();
            RegisterCombo(ElementCategory.Fire, ElementCategory.Ice, new ElementCombo("Steam Explosion", 2.0f, ElementCategory.Wind));
            RegisterCombo(ElementCategory.Fire, ElementCategory.Wind, new ElementCombo("Firestorm", 1.8f, ElementCategory.Fire));
            RegisterCombo(ElementCategory.Ice, ElementCategory.Wind, new ElementCombo("Blizzard", 1.5f, ElementCategory.Ice));
            RegisterCombo(ElementCategory.Fire, ElementCategory.Stone, new ElementCombo("Magma Burst", 1.7f, ElementCategory.Stone));
            RegisterCombo(ElementCategory.Ice, ElementCategory.Stone, new ElementCombo("Shatter", 2.2f, ElementCategory.Stone));
            RegisterCombo(ElementCategory.Wind, ElementCategory.Stone, new ElementCombo("Sandstorm", 1.4f, ElementCategory.Wind));
            RegisterCombo(ElementCategory.Lightning, ElementCategory.Fire, new ElementCombo("Plasma", 2.5f, ElementCategory.Lightning));
            RegisterCombo(ElementCategory.Lightning, ElementCategory.Ice, new ElementCombo("Freeze Shock", 1.9f, ElementCategory.Lightning));
            RegisterCombo(ElementCategory.Lightning, ElementCategory.Wind, new ElementCombo("Thunderstorm", 2.1f, ElementCategory.Lightning));
            RegisterCombo(ElementCategory.Lightning, ElementCategory.Stone, new ElementCombo("Seismic Pulse", 1.6f, ElementCategory.Stone));
        }

        private void RegisterCombo(ElementCategory a, ElementCategory b, ElementCombo combo)
        {
            _combos[(a, b)] = combo;
            _combos[(b, a)] = combo;
        }

        public ElementCombo GetInteraction(ElementCategory a, ElementCategory b)
        {
            if (a == b) return null;
            return _combos.TryGetValue((a, b), out var combo) ? combo : null;
        }

        public bool HasCombo(ElementCategory a, ElementCategory b)
        {
            return GetInteraction(a, b) != null;
        }

        public System.Collections.Generic.List<ElementCombo> GetAllCombos()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            var result = new System.Collections.Generic.List<ElementCombo>();
            foreach (var kvp in _combos)
            {
                if (seen.Add(kvp.Value.Name))
                {
                    result.Add(kvp.Value);
                }
            }
            return result;
        }
    }
}
