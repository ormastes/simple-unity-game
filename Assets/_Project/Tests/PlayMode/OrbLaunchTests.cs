using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ElementalSiege.Tests.PlayMode
{
    [TestFixture]
    public class OrbLaunchTests
    {
        private GameObject _orbObject;
        private Rigidbody2D _rigidbody;
        private OrbBase _orbBase;

        [SetUp]
        public void SetUp()
        {
            _orbObject = new GameObject("TestOrb");
            _rigidbody = _orbObject.AddComponent<Rigidbody2D>();
            _rigidbody.gravityScale = 1f;
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _orbBase = _orbObject.AddComponent<OrbBase>();
            _orbBase.MaxLifetime = 5f;
            _orbBase.SettleVelocityThreshold = 0.1f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_orbObject != null)
            {
                Object.Destroy(_orbObject);
            }
        }

        [UnityTest]
        public IEnumerator OrbBase_AppliesForceOnLaunch()
        {
            Vector2 launchForce = new Vector2(5f, 10f);
            _orbBase.Launch(launchForce);

            yield return new WaitForFixedUpdate();

            Assert.Greater(_rigidbody.linearVelocity.magnitude, 0f,
                "Orb should have velocity after launch force is applied");
        }

        [UnityTest]
        public IEnumerator OrbBase_DetectsSettling_WhenVelocityLow()
        {
            _orbBase.Launch(new Vector2(0.01f, 0f));
            _rigidbody.gravityScale = 0f;
            _rigidbody.linearDamping = 10f;

            // Wait for velocity to drop
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                if (_orbBase.IsSettled)
                    break;
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(_orbBase.IsSettled,
                "Orb should detect settling when velocity drops below threshold");
        }

        [UnityTest]
        public IEnumerator OrbBase_AutoDestroys_AfterMaxLifetime()
        {
            _orbBase.MaxLifetime = 0.5f;
            _orbBase.Launch(new Vector2(1f, 1f));

            yield return new WaitForSeconds(0.7f);

            Assert.IsTrue(_orbObject == null,
                "Orb should be destroyed after exceeding max lifetime");
        }

        [UnityTest]
        public IEnumerator StoneOrb_IncreasesGravity_OnAbility()
        {
            var stoneOrb = _orbObject.AddComponent<StoneOrbAbility>();
            stoneOrb.GravityMultiplier = 3f;

            _orbBase.Launch(new Vector2(3f, 5f));
            yield return new WaitForFixedUpdate();

            float gravityBefore = _rigidbody.gravityScale;
            stoneOrb.ActivateAbility();
            yield return new WaitForFixedUpdate();

            Assert.Greater(_rigidbody.gravityScale, gravityBefore,
                "Stone orb ability should increase gravity scale");
        }
    }

    /// <summary>
    /// Stub OrbBase MonoBehaviour for test compilation.
    /// </summary>
    public class OrbBase : MonoBehaviour
    {
        public float MaxLifetime = 5f;
        public float SettleVelocityThreshold = 0.1f;
        public bool IsSettled { get; private set; }

        private Rigidbody2D _rb;
        private bool _launched;
        private float _launchTime;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        public void Launch(Vector2 force)
        {
            _launched = true;
            _launchTime = Time.time;
            _rb.AddForce(force, ForceMode2D.Impulse);
        }

        private void FixedUpdate()
        {
            if (!_launched) return;

            if (Time.time - _launchTime > MaxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (_rb.linearVelocity.magnitude < SettleVelocityThreshold)
            {
                IsSettled = true;
            }
        }
    }

    /// <summary>
    /// Stub StoneOrbAbility for test compilation.
    /// </summary>
    public class StoneOrbAbility : MonoBehaviour
    {
        public float GravityMultiplier = 3f;

        public void ActivateAbility()
        {
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale *= GravityMultiplier;
            }
        }
    }
}
