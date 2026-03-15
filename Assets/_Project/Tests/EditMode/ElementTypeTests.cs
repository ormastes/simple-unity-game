using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ElementalSiege.Elements;

namespace ElementalSiege.Tests.EditMode
{
    /// <summary>
    /// Tests for ElementType using the real ElementalSiege.Elements assembly.
    /// ElementType is a ScriptableObject with private serialized fields,
    /// so we use reflection to set values after CreateInstance.
    /// </summary>
    [TestFixture]
    public class ElementTypeTests
    {
        private ElementType[] _allElements;

        /// <summary>
        /// Helper to create an ElementType instance with specified values via reflection,
        /// since the real class uses private [SerializeField] fields with no public setters.
        /// </summary>
        private static ElementType CreateElementType(string name, ElementCategory category, float baseDamage, Color color)
        {
            var element = ScriptableObject.CreateInstance<ElementType>();
            var type = typeof(ElementType);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            type.GetField("elementName", flags)?.SetValue(element, name);
            type.GetField("category", flags)?.SetValue(element, category);
            type.GetField("baseDamage", flags)?.SetValue(element, baseDamage);
            type.GetField("primaryColor", flags)?.SetValue(element, color);

            return element;
        }

        [SetUp]
        public void SetUp()
        {
            _allElements = new[]
            {
                CreateElementType("Fire", ElementCategory.Fire, 25f, new Color(1f, 0.3f, 0f)),
                CreateElementType("Ice", ElementCategory.Ice, 20f, new Color(0.4f, 0.8f, 1f)),
                CreateElementType("Wind", ElementCategory.Wind, 15f, new Color(0.6f, 1f, 0.6f)),
                CreateElementType("Stone", ElementCategory.Stone, 30f, new Color(0.6f, 0.5f, 0.4f)),
                CreateElementType("Lightning", ElementCategory.Lightning, 35f, new Color(1f, 1f, 0.2f))
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var element in _allElements)
            {
                Object.DestroyImmediate(element);
            }
        }

        [Test]
        public void AllElements_HavePositiveBaseDamage()
        {
            foreach (var element in _allElements)
            {
                Assert.Greater(element.BaseDamage, 0f,
                    $"Element '{element.ElementName}' should have positive base damage, got {element.BaseDamage}");
            }
        }

        [Test]
        public void AllElements_HaveUniqueCategory()
        {
            var categories = new System.Collections.Generic.HashSet<ElementCategory>();

            foreach (var element in _allElements)
            {
                bool isUnique = categories.Add(element.Category);
                Assert.IsTrue(isUnique,
                    $"Element '{element.ElementName}' has duplicate category '{element.Category}'");
            }
        }

        [Test]
        public void AllElements_HaveNonEmptyName()
        {
            foreach (var element in _allElements)
            {
                Assert.IsFalse(string.IsNullOrEmpty(element.ElementName),
                    "Every element must have a non-empty element name");
            }
        }

        [Test]
        public void AllElements_HaveValidColors()
        {
            foreach (var element in _allElements)
            {
                Color c = element.PrimaryColor;

                Assert.GreaterOrEqual(c.r, 0f, $"Element '{element.ElementName}' color red channel out of range");
                Assert.LessOrEqual(c.r, 1f, $"Element '{element.ElementName}' color red channel out of range");
                Assert.GreaterOrEqual(c.g, 0f, $"Element '{element.ElementName}' color green channel out of range");
                Assert.LessOrEqual(c.g, 1f, $"Element '{element.ElementName}' color green channel out of range");
                Assert.GreaterOrEqual(c.b, 0f, $"Element '{element.ElementName}' color blue channel out of range");
                Assert.LessOrEqual(c.b, 1f, $"Element '{element.ElementName}' color blue channel out of range");
                Assert.GreaterOrEqual(c.a, 0f, $"Element '{element.ElementName}' color alpha channel out of range");
                Assert.LessOrEqual(c.a, 1f, $"Element '{element.ElementName}' color alpha channel out of range");
            }
        }
    }
}
