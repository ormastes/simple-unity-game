using NUnit.Framework;
using UnityEngine;

namespace ElementalSiege.Tests.EditMode
{
    [TestFixture]
    public class ElementTypeTests
    {
        private ElementTypeSO[] _allElements;

        [SetUp]
        public void SetUp()
        {
            _allElements = new[]
            {
                ElementTypeSO.Create("Fire", ElementCategory.Fire, 25f, new Color(1f, 0.3f, 0f)),
                ElementTypeSO.Create("Ice", ElementCategory.Ice, 20f, new Color(0.4f, 0.8f, 1f)),
                ElementTypeSO.Create("Wind", ElementCategory.Wind, 15f, new Color(0.6f, 1f, 0.6f)),
                ElementTypeSO.Create("Stone", ElementCategory.Stone, 30f, new Color(0.6f, 0.5f, 0.4f)),
                ElementTypeSO.Create("Lightning", ElementCategory.Lightning, 35f, new Color(1f, 1f, 0.2f))
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
                    $"Element '{element.DisplayName}' should have positive base damage, got {element.BaseDamage}");
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
                    $"Element '{element.DisplayName}' has duplicate category '{element.Category}'");
            }
        }

        [Test]
        public void AllElements_HaveNonEmptyName()
        {
            foreach (var element in _allElements)
            {
                Assert.IsFalse(string.IsNullOrEmpty(element.DisplayName),
                    "Every element must have a non-empty display name");
            }
        }

        [Test]
        public void AllElements_HaveValidColors()
        {
            foreach (var element in _allElements)
            {
                Color c = element.PrimaryColor;

                Assert.GreaterOrEqual(c.r, 0f, $"Element '{element.DisplayName}' color red channel out of range");
                Assert.LessOrEqual(c.r, 1f, $"Element '{element.DisplayName}' color red channel out of range");
                Assert.GreaterOrEqual(c.g, 0f, $"Element '{element.DisplayName}' color green channel out of range");
                Assert.LessOrEqual(c.g, 1f, $"Element '{element.DisplayName}' color green channel out of range");
                Assert.GreaterOrEqual(c.b, 0f, $"Element '{element.DisplayName}' color blue channel out of range");
                Assert.LessOrEqual(c.b, 1f, $"Element '{element.DisplayName}' color blue channel out of range");
                Assert.GreaterOrEqual(c.a, 0f, $"Element '{element.DisplayName}' color alpha channel out of range");
                Assert.LessOrEqual(c.a, 1f, $"Element '{element.DisplayName}' color alpha channel out of range");
            }
        }
    }

    /// <summary>
    /// Stub ScriptableObject for ElementType. Replace with actual asset reference.
    /// </summary>
    public class ElementTypeSO : ScriptableObject
    {
        public string DisplayName;
        public ElementCategory Category;
        public float BaseDamage;
        public Color PrimaryColor;

        public static ElementTypeSO Create(string displayName, ElementCategory category, float baseDamage, Color color)
        {
            var so = ScriptableObject.CreateInstance<ElementTypeSO>();
            so.DisplayName = displayName;
            so.Category = category;
            so.BaseDamage = baseDamage;
            so.PrimaryColor = color;
            return so;
        }
    }
}
