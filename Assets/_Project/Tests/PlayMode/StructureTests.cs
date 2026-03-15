using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ElementalSiege.Structures;

namespace ElementalSiege.Tests.PlayMode
{
    /// <summary>
    /// Tests for structure components using the real ElementalSiege.Structures assembly.
    /// StructureHealth is a MonoBehaviour that initializes in Awake() automatically.
    /// MaxHealth is a read-only property; the component reads it from StructureBlock
    /// or defaults to 100.
    /// </summary>
    [TestFixture]
    public class StructureTests
    {
        private GameObject _structureObject;
        private StructureHealth _health;

        [SetUp]
        public void SetUp()
        {
            _structureObject = new GameObject("TestStructure");
            _structureObject.AddComponent<BoxCollider2D>();
            // StructureHealth requires Rigidbody2D
            _structureObject.AddComponent<Rigidbody2D>();
            _structureObject.AddComponent<SpriteRenderer>();
            // StructureHealth initializes in Awake() with default maxHealth of 100
            _health = _structureObject.AddComponent<StructureHealth>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_structureObject != null)
            {
                Object.Destroy(_structureObject);
            }
        }

        [UnityTest]
        public IEnumerator StructureHealth_TakesDamage()
        {
            float healthBefore = _health.CurrentHealth;
            _health.TakeDamage(25f);

            yield return null;

            Assert.Less(_health.CurrentHealth, healthBefore,
                "Structure health should decrease after taking damage");
            Assert.AreEqual(75f, _health.CurrentHealth, 0.01f,
                "Structure should have 75 health after taking 25 damage from 100");
        }

        [UnityTest]
        public IEnumerator StructureHealth_Dies_WhenHealthZero()
        {
            _health.TakeDamage(_health.MaxHealth);

            yield return null;

            Assert.IsTrue(_health.IsDead,
                "Structure should be dead when health reaches zero");
        }

        [UnityTest]
        public IEnumerator StructureHealth_CannotTakeDamage_WhenDead()
        {
            _health.TakeDamage(_health.MaxHealth);
            yield return null;

            float healthAfterDeath = _health.CurrentHealth;
            _health.TakeDamage(50f);
            yield return null;

            Assert.AreEqual(healthAfterDeath, _health.CurrentHealth, 0.01f,
                "Dead structure should not take further damage");
        }

        [UnityTest]
        public IEnumerator StructureHealth_Heal_RestoresHealth()
        {
            _health.TakeDamage(50f);
            yield return null;

            _health.Heal(25f);
            yield return null;

            Assert.AreEqual(75f, _health.CurrentHealth, 0.01f,
                "Healing 25 after taking 50 damage should result in 75 health");
        }

        [UnityTest]
        public IEnumerator StructureHealth_ResetHealth_RestoresToMax()
        {
            _health.TakeDamage(80f);
            yield return null;

            _health.ResetHealth();
            yield return null;

            Assert.AreEqual(_health.MaxHealth, _health.CurrentHealth, 0.01f,
                "ResetHealth should restore to max health");
            Assert.IsFalse(_health.IsDead,
                "ResetHealth should clear dead state");
        }

        [UnityTest]
        public IEnumerator StructureHealth_Invulnerable_TakesNoDamage()
        {
            _health.IsInvulnerable = true;
            float initialHealth = _health.CurrentHealth;

            _health.TakeDamage(50f);
            yield return null;

            Assert.AreEqual(initialHealth, _health.CurrentHealth, 0.01f,
                "Invulnerable structure should not take damage");
        }

        [UnityTest]
        public IEnumerator Flammable_IgniteSetsOnFire()
        {
            // Flammable requires StructureHealth (which we already have)
            var flammable = _structureObject.AddComponent<Flammable>();

            yield return null; // let Awake run

            flammable.Ignite();

            yield return null;

            Assert.IsTrue(flammable.IsOnFire,
                "Structure should be on fire after Ignite()");
        }

        [UnityTest]
        public IEnumerator Flammable_Extinguish_StopsFire()
        {
            var flammable = _structureObject.AddComponent<Flammable>();

            yield return null;

            flammable.Ignite();
            yield return null;

            flammable.Extinguish();
            yield return null;

            Assert.IsFalse(flammable.IsOnFire,
                "Structure should not be on fire after Extinguish()");
        }

        [UnityTest]
        public IEnumerator Freezable_FreezeSetsIsFrozen()
        {
            // Freezable requires SpriteRenderer (which we already have)
            var freezable = _structureObject.AddComponent<Freezable>();

            yield return null; // let Awake run

            freezable.Freeze();

            yield return null;

            Assert.IsTrue(freezable.IsFrozen,
                "Structure should be frozen after Freeze() is called");
        }

        [UnityTest]
        public IEnumerator Freezable_Thaw_ClearsFrozen()
        {
            var freezable = _structureObject.AddComponent<Freezable>();

            yield return null;

            freezable.Freeze();
            yield return null;

            freezable.Thaw();
            yield return null;

            Assert.IsFalse(freezable.IsFrozen,
                "Structure should not be frozen after Thaw()");
        }

        [UnityTest]
        public IEnumerator StructureBlock_HasValidBaseHealth()
        {
            // StructureBlock requires Rigidbody2D, Collider2D, SpriteRenderer (all present)
            var block = _structureObject.AddComponent<StructureBlock>();

            yield return null;

            Assert.Greater(block.BaseHealth, 0f,
                "StructureBlock should have positive base health");
        }

        [UnityTest]
        public IEnumerator StructureBlock_HasValidScoreValue()
        {
            var block = _structureObject.AddComponent<StructureBlock>();

            yield return null;

            Assert.Greater(block.ScoreValue, 0,
                "StructureBlock should have a positive score value");
        }
    }
}
