using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ElementalSiege.Launcher;
using ElementalSiege.Orbs;

namespace ElementalSiege.Tests.PlayMode
{
    /// <summary>
    /// Tests for the Catapult and TrajectoryPreview using the real ElementalSiege.Launcher assembly.
    /// The real class is Catapult (not CatapultController), with a nested CatapultState enum.
    /// State is accessed via the State property. The catapult uses an InputManager for drag events,
    /// so these tests verify component creation, orb loading, and trajectory preview behavior.
    /// </summary>
    [TestFixture]
    public class CatapultTests
    {
        private GameObject _catapultObject;
        private Catapult _catapult;
        private GameObject _trajectoryObject;
        private TrajectoryPreview _trajectory;
        private GameObject _launchPointObject;

        [SetUp]
        public void SetUp()
        {
            // Create the catapult with a launch point transform
            _catapultObject = new GameObject("TestCatapult");
            _catapult = _catapultObject.AddComponent<Catapult>();

            _launchPointObject = new GameObject("LaunchPoint");
            _launchPointObject.transform.position = Vector3.zero;

            // TrajectoryPreview requires LineRenderer
            _trajectoryObject = new GameObject("TrajectoryPreview");
            _trajectoryObject.AddComponent<LineRenderer>();
            _trajectory = _trajectoryObject.AddComponent<TrajectoryPreview>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_catapultObject != null) Object.Destroy(_catapultObject);
            if (_trajectoryObject != null) Object.Destroy(_trajectoryObject);
            if (_launchPointObject != null) Object.Destroy(_launchPointObject);
        }

        [Test]
        public void Catapult_StartsInWaitingForOrbState()
        {
            // Real catapult initializes to WaitingForOrb in its field initializer
            Assert.AreEqual(Catapult.CatapultState.WaitingForOrb, _catapult.State,
                "Catapult should start in WaitingForOrb state");
        }

        [UnityTest]
        public IEnumerator Catapult_LoadOrb_TransitionsToIdle()
        {
            // Create a test orb (StoneOrb is a concrete OrbBase subclass)
            var orbObject = new GameObject("TestOrb");
            orbObject.AddComponent<Rigidbody2D>();
            orbObject.AddComponent<CircleCollider2D>();
            var orb = orbObject.AddComponent<StoneOrb>();

            _catapult.LoadOrb(orb);

            yield return null;

            Assert.AreEqual(Catapult.CatapultState.Idle, _catapult.State,
                "Catapult should enter Idle state after loading an orb");

            Object.Destroy(orbObject);
        }

        [Test]
        public void Catapult_CalculateLaunchVelocity_ReturnsNonZero()
        {
            Vector3 dragPosition = new Vector3(-2f, 1f, 0f);
            Vector2 velocity = _catapult.CalculateLaunchVelocity(dragPosition);

            Assert.Greater(velocity.magnitude, 0f,
                "Launch velocity should be non-zero for a non-zero drag offset");
        }

        [Test]
        public void Catapult_SetWaitingForOrb_SetsCorrectState()
        {
            _catapult.SetWaitingForOrb();

            Assert.AreEqual(Catapult.CatapultState.WaitingForOrb, _catapult.State,
                "SetWaitingForOrb should set state to WaitingForOrb");
        }

        [Test]
        public void TrajectoryPreview_StartsHidden()
        {
            // TrajectoryPreview calls Hide() in its Awake, so after Awake it should not be visible
            // Note: Awake runs on AddComponent in tests
            var lr = _trajectoryObject.GetComponent<LineRenderer>();
            Assert.IsFalse(lr.enabled,
                "TrajectoryPreview LineRenderer should start disabled (hidden)");
        }

        [UnityTest]
        public IEnumerator TrajectoryPreview_Show_EnablesRenderer()
        {
            _trajectory.Show();

            yield return null;

            var lr = _trajectoryObject.GetComponent<LineRenderer>();
            Assert.IsTrue(lr.enabled,
                "TrajectoryPreview LineRenderer should be enabled after Show()");
        }

        [UnityTest]
        public IEnumerator TrajectoryPreview_Hide_DisablesRenderer()
        {
            _trajectory.Show();
            yield return null;

            _trajectory.Hide();
            yield return null;

            var lr = _trajectoryObject.GetComponent<LineRenderer>();
            Assert.IsFalse(lr.enabled,
                "TrajectoryPreview LineRenderer should be disabled after Hide()");
        }
    }
}
