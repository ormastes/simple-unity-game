using NUnit.Framework;
using UnityEngine;
using ElementalSiege.Elements;

namespace ElementalSiege.Tests.EditMode
{
    /// <summary>
    /// Tests for InteractionMatrix using the real ElementalSiege.Elements assembly.
    /// InteractionMatrix is a ScriptableObject, so we use CreateInstance.
    /// </summary>
    [TestFixture]
    public class InteractionMatrixTests
    {
        private InteractionMatrix _matrix;

        [SetUp]
        public void SetUp()
        {
            _matrix = ScriptableObject.CreateInstance<InteractionMatrix>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_matrix != null)
            {
                Object.DestroyImmediate(_matrix);
            }
        }

        [Test]
        public void GetInteraction_ReturnsNull_ForSameElement()
        {
            // An empty matrix should return null for any pairing
            ElementInteraction interaction = _matrix.GetInteraction(ElementCategory.Stone, ElementCategory.Stone);

            Assert.IsNull(interaction, "Same element pairing should return null");
        }

        [Test]
        public void HasCombo_ReturnsFalse_ForSameElement()
        {
            bool hasCombo = _matrix.HasCombo(ElementCategory.Fire, ElementCategory.Fire);

            Assert.IsFalse(hasCombo,
                "Same element should not produce a combo");
        }

        [Test]
        public void GetInteraction_IsCommutative()
        {
            // On an empty matrix, both orderings should return null (commutative)
            ElementInteraction comboAB = _matrix.GetInteraction(ElementCategory.Fire, ElementCategory.Ice);
            ElementInteraction comboBA = _matrix.GetInteraction(ElementCategory.Ice, ElementCategory.Fire);

            Assert.AreEqual(comboAB, comboBA,
                "Element interactions should be commutative (order should not matter)");
        }

        [Test]
        public void HasCombo_ReturnsFalse_WhenNoInteractionsRegistered()
        {
            bool hasCombo = _matrix.HasCombo(ElementCategory.Fire, ElementCategory.Ice);

            Assert.IsFalse(hasCombo,
                "Empty InteractionMatrix should report no combos");
        }

        [Test]
        public void Interactions_IsNotNull_OnNewInstance()
        {
            Assert.IsNotNull(_matrix.Interactions,
                "Interactions list should not be null on a new InteractionMatrix");
        }

        [Test]
        public void RebuildCache_DoesNotThrow_OnEmptyMatrix()
        {
            Assert.DoesNotThrow(() => _matrix.RebuildCache(),
                "RebuildCache should not throw on an empty interaction matrix");
        }

        [Test]
        public void GetInteraction_ReturnsNull_ForUndefinedPair()
        {
            ElementInteraction interaction = _matrix.GetInteraction(ElementCategory.Wind, ElementCategory.Crystal);

            Assert.IsNull(interaction,
                "Undefined element pairing should return null");
        }

        [Test]
        public void ElementInteraction_ScriptableObject_CanBeCreated()
        {
            var interaction = ScriptableObject.CreateInstance<ElementInteraction>();

            Assert.IsNotNull(interaction,
                "ElementInteraction ScriptableObject should be creatable");
            Assert.Greater(interaction.DamageMultiplier, 0f,
                "Default DamageMultiplier should be positive (defaults to 1.5)");

            Object.DestroyImmediate(interaction);
        }
    }
}
