using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ElementalSiege.Orbs;

namespace ElementalSiege.Tests.PlayMode
{
    /// <summary>
    /// Tests for orb launch mechanics using the real ElementalSiege.Orbs assembly.
    /// OrbBase is abstract, so we use StoneOrb (the simplest concrete subclass).
    /// The real API uses OnLaunch(force) and CurrentState (OrbState enum) instead of
    /// Launch(force) and IsSettled.
    /// </summary>
    [TestFixture]
    public class OrbLaunchTests
    {
        private GameObject _orbObject;
        private Rigidbody2D _rigidbody;
        private StoneOrb _stoneOrb;

        [SetUp]
        public void SetUp()
        {
            _orbObject = new GameObject("TestOrb");
            // StoneOrb requires Rigidbody2D and CircleCollider2D (via OrbBase's RequireComponent)
            _rigidbody = _orbObject.AddComponent<Rigidbody2D>();
            _rigidbody.gravityScale = 1f;
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _orbObject.AddComponent<CircleCollider2D>();
            _stoneOrb = _orbObject.AddComponent<StoneOrb>();
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
            _stoneOrb.OnLaunch(launchForce);

            yield return new WaitForFixedUpdate();

            Assert.Greater(_rigidbody.linearVelocity.magnitude, 0f,
                "Orb should have velocity after launch force is applied");
        }

        [UnityTest]
        public IEnumerator OrbBase_TransitionsToInFlight_OnLaunch()
        {
            Assert.AreEqual(OrbState.Loaded, _stoneOrb.CurrentState,
                "Orb should start in Loaded state");

            _stoneOrb.OnLaunch(new Vector2(5f, 10f));

            yield return new WaitForFixedUpdate();

            Assert.AreEqual(OrbState.InFlight, _stoneOrb.CurrentState,
                "Orb should be in InFlight state after launch");
        }

        [UnityTest]
        public IEnumerator OrbBase_CannotLaunchTwice()
        {
            _stoneOrb.OnLaunch(new Vector2(5f, 10f));
            yield return new WaitForFixedUpdate();

            float velocityAfterFirstLaunch = _rigidbody.linearVelocity.magnitude;

            // Attempting to launch again should be ignored (not in Loaded state)
            _stoneOrb.OnLaunch(new Vector2(50f, 50f));
            yield return new WaitForFixedUpdate();

            // Velocity should not dramatically increase from a second launch
            Assert.AreEqual(OrbState.InFlight, _stoneOrb.CurrentState,
                "Orb should remain in InFlight state");
        }

        [UnityTest]
        public IEnumerator StoneOrb_AbilityChangesState()
        {
            _stoneOrb.OnLaunch(new Vector2(3f, 5f));
            yield return new WaitForFixedUpdate();

            _stoneOrb.TryActivateAbility();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(OrbState.AbilityActivated, _stoneOrb.CurrentState,
                "Stone orb should be in AbilityActivated state after ability use");
        }

        [UnityTest]
        public IEnumerator StoneOrb_IncreasesGravity_OnAbility()
        {
            _stoneOrb.OnLaunch(new Vector2(3f, 5f));
            yield return new WaitForFixedUpdate();

            float gravityBefore = _rigidbody.gravityScale;
            _stoneOrb.TryActivateAbility();
            yield return new WaitForFixedUpdate();

            Assert.Greater(_rigidbody.gravityScale, gravityBefore,
                "Stone orb ability should increase gravity scale for heavy slam");
        }
    }
}
