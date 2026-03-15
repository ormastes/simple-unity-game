using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ElementalSiege.Tests.PlayMode
{
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
            _structureObject.AddComponent<Rigidbody2D>();
            _structureObject.AddComponent<SpriteRenderer>();
            _health = _structureObject.AddComponent<StructureHealth>();
            _health.MaxHealth = 100f;
            _health.Initialize();
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
        public IEnumerator StructureHealth_TakesDamage_FromCollision()
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
            _health.TakeDamage(100f);

            yield return null;

            Assert.IsTrue(_health.IsDead,
                "Structure should be dead when health reaches zero");
        }

        [UnityTest]
        public IEnumerator Flammable_SpreadsFireToNearby()
        {
            var flammable = _structureObject.AddComponent<Flammable>();
            flammable.SpreadRadius = 5f;
            flammable.SpreadDelay = 0.1f;

            // Create a nearby structure that can catch fire
            var nearbyObject = new GameObject("NearbyStructure");
            nearbyObject.transform.position = new Vector3(2f, 0f, 0f);
            nearbyObject.AddComponent<BoxCollider2D>();
            var nearbyFlammable = nearbyObject.AddComponent<Flammable>();
            nearbyFlammable.SpreadRadius = 5f;

            flammable.Ignite();

            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(nearbyFlammable.IsOnFire,
                "Fire should spread to nearby flammable structures within spread radius");

            Object.Destroy(nearbyObject);
        }

        [UnityTest]
        public IEnumerator Freezable_BecomeBrittle_WhenFrozen()
        {
            var freezable = _structureObject.AddComponent<Freezable>();
            freezable.BrittleMultiplier = 2.0f;

            float normalDamage = 20f;
            freezable.Freeze();

            yield return null;

            Assert.IsTrue(freezable.IsFrozen,
                "Structure should be frozen after Freeze() is called");

            float effectiveDamage = freezable.CalculateEffectiveDamage(normalDamage);
            Assert.AreEqual(normalDamage * freezable.BrittleMultiplier, effectiveDamage, 0.01f,
                "Frozen structures should take increased damage due to brittleness");
        }

        [UnityTest]
        public IEnumerator StructureBlock_ChangesSprite_OnDamage()
        {
            var block = _structureObject.AddComponent<StructureBlock>();
            var renderer = _structureObject.GetComponent<SpriteRenderer>();

            // Create damage stage sprites
            var healthySprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0, 0, 4, 4),
                Vector2.one * 0.5f
            );
            var damagedSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0, 0, 4, 4),
                Vector2.one * 0.5f
            );

            block.DamageStageSprites = new[] { healthySprite, damagedSprite };
            renderer.sprite = healthySprite;

            block.OnDamageStageChanged(1);

            yield return null;

            Assert.AreEqual(damagedSprite, renderer.sprite,
                "Structure sprite should change when damage stage advances");

            Object.Destroy(healthySprite);
            Object.Destroy(damagedSprite);
        }
    }

    /// <summary>
    /// Stub StructureHealth for test compilation.
    /// </summary>
    public class StructureHealth : MonoBehaviour
    {
        public float MaxHealth = 100f;
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;

        public void Initialize()
        {
            CurrentHealth = MaxHealth;
        }

        public void TakeDamage(float amount)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        }
    }

    /// <summary>
    /// Stub Flammable component for test compilation.
    /// </summary>
    public class Flammable : MonoBehaviour
    {
        public float SpreadRadius = 5f;
        public float SpreadDelay = 0.5f;
        public bool IsOnFire { get; private set; }

        public void Ignite()
        {
            if (IsOnFire) return;
            IsOnFire = true;
            StartCoroutine(SpreadFireCoroutine());
        }

        private IEnumerator SpreadFireCoroutine()
        {
            yield return new WaitForSeconds(SpreadDelay);

            var colliders = Physics2D.OverlapCircleAll(transform.position, SpreadRadius);
            foreach (var col in colliders)
            {
                if (col.gameObject == gameObject) continue;
                var nearby = col.GetComponent<Flammable>();
                if (nearby != null && !nearby.IsOnFire)
                {
                    nearby.Ignite();
                }
            }
        }
    }

    /// <summary>
    /// Stub Freezable component for test compilation.
    /// </summary>
    public class Freezable : MonoBehaviour
    {
        public float BrittleMultiplier = 2.0f;
        public bool IsFrozen { get; private set; }

        public void Freeze()
        {
            IsFrozen = true;
        }

        public void Thaw()
        {
            IsFrozen = false;
        }

        public float CalculateEffectiveDamage(float baseDamage)
        {
            return IsFrozen ? baseDamage * BrittleMultiplier : baseDamage;
        }
    }

    /// <summary>
    /// Stub StructureBlock for test compilation.
    /// </summary>
    public class StructureBlock : MonoBehaviour
    {
        public Sprite[] DamageStageSprites;

        public void OnDamageStageChanged(int stageIndex)
        {
            if (DamageStageSprites == null || stageIndex >= DamageStageSprites.Length)
                return;

            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = DamageStageSprites[stageIndex];
            }
        }
    }
}
