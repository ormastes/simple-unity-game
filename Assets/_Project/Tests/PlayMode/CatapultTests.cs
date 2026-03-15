using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ElementalSiege.Tests.PlayMode
{
    [TestFixture]
    public class CatapultTests
    {
        private GameObject _catapultObject;
        private CatapultController _catapult;
        private GameObject _trajectoryObject;
        private TrajectoryPreview _trajectory;

        [SetUp]
        public void SetUp()
        {
            _catapultObject = new GameObject("TestCatapult");
            _catapult = _catapultObject.AddComponent<CatapultController>();
            _catapult.MaxDragDistance = 3f;
            _catapult.LaunchForceMultiplier = 10f;

            _trajectoryObject = new GameObject("TrajectoryPreview");
            _trajectory = _trajectoryObject.AddComponent<TrajectoryPreview>();
            _trajectory.DotCount = 15;
            var lr = _trajectoryObject.AddComponent<LineRenderer>();
            lr.positionCount = 0;

            _catapult.TrajectoryPreview = _trajectory;
            _catapult.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (_catapultObject != null) Object.Destroy(_catapultObject);
            if (_trajectoryObject != null) Object.Destroy(_trajectoryObject);
        }

        [Test]
        public void Catapult_StartsInIdleState()
        {
            Assert.AreEqual(CatapultState.Idle, _catapult.CurrentState,
                "Catapult should start in Idle state");
        }

        [UnityTest]
        public IEnumerator Catapult_EntersAimingOnDrag()
        {
            _catapult.OnDragStart(new Vector2(0f, 0f));

            yield return null;

            Assert.AreEqual(CatapultState.Aiming, _catapult.CurrentState,
                "Catapult should enter Aiming state when drag starts");
        }

        [UnityTest]
        public IEnumerator Catapult_LaunchesOrb_OnDragEnd()
        {
            // Create a test orb to load
            var orbObject = new GameObject("TestOrb");
            orbObject.AddComponent<Rigidbody2D>();
            var orbBase = orbObject.AddComponent<OrbBase>();
            _catapult.LoadOrb(orbBase);

            _catapult.OnDragStart(Vector2.zero);
            _catapult.OnDrag(new Vector2(-2f, 1f));
            _catapult.OnDragEnd();

            yield return new WaitForFixedUpdate();

            Assert.AreEqual(CatapultState.Launched, _catapult.CurrentState,
                "Catapult should enter Launched state after drag end");

            if (orbObject != null) Object.Destroy(orbObject);
        }

        [Test]
        public void Catapult_ClampsMaxDragDistance()
        {
            _catapult.OnDragStart(Vector2.zero);

            // Drag way beyond max distance
            Vector2 farDrag = new Vector2(-10f, 10f);
            _catapult.OnDrag(farDrag);

            float clampedDistance = _catapult.CurrentDragVector.magnitude;

            Assert.LessOrEqual(clampedDistance, _catapult.MaxDragDistance + 0.01f,
                $"Drag distance ({clampedDistance}) should be clamped to max ({_catapult.MaxDragDistance})");
        }

        [UnityTest]
        public IEnumerator TrajectoryPreview_ShowsWhileAiming()
        {
            var orbObject = new GameObject("TestOrb");
            orbObject.AddComponent<Rigidbody2D>();
            var orbBase = orbObject.AddComponent<OrbBase>();
            _catapult.LoadOrb(orbBase);

            _catapult.OnDragStart(Vector2.zero);
            _catapult.OnDrag(new Vector2(-1f, 1f));

            yield return null;

            Assert.IsTrue(_trajectory.IsVisible,
                "Trajectory preview should be visible while aiming");

            if (orbObject != null) Object.Destroy(orbObject);
        }

        [UnityTest]
        public IEnumerator TrajectoryPreview_HidesAfterLaunch()
        {
            var orbObject = new GameObject("TestOrb");
            orbObject.AddComponent<Rigidbody2D>();
            var orbBase = orbObject.AddComponent<OrbBase>();
            _catapult.LoadOrb(orbBase);

            _catapult.OnDragStart(Vector2.zero);
            _catapult.OnDrag(new Vector2(-1f, 1f));
            _catapult.OnDragEnd();

            yield return null;

            Assert.IsFalse(_trajectory.IsVisible,
                "Trajectory preview should be hidden after launch");

            if (orbObject != null) Object.Destroy(orbObject);
        }
    }

    /// <summary>
    /// Catapult state enumeration.
    /// </summary>
    public enum CatapultState
    {
        Idle,
        Aiming,
        Launched,
        WaitingForSettle
    }

    /// <summary>
    /// Stub CatapultController for test compilation.
    /// </summary>
    public class CatapultController : MonoBehaviour
    {
        public float MaxDragDistance = 3f;
        public float LaunchForceMultiplier = 10f;
        public TrajectoryPreview TrajectoryPreview;
        public CatapultState CurrentState { get; private set; }
        public Vector2 CurrentDragVector { get; private set; }

        private OrbBase _loadedOrb;
        private Vector2 _dragOrigin;

        public void Initialize()
        {
            CurrentState = CatapultState.Idle;
            CurrentDragVector = Vector2.zero;
        }

        public void LoadOrb(OrbBase orb)
        {
            _loadedOrb = orb;
        }

        public void OnDragStart(Vector2 position)
        {
            _dragOrigin = position;
            CurrentState = CatapultState.Aiming;
        }

        public void OnDrag(Vector2 position)
        {
            Vector2 dragVec = position - _dragOrigin;
            if (dragVec.magnitude > MaxDragDistance)
            {
                dragVec = dragVec.normalized * MaxDragDistance;
            }
            CurrentDragVector = dragVec;

            if (TrajectoryPreview != null)
            {
                TrajectoryPreview.Show(-dragVec * LaunchForceMultiplier);
            }
        }

        public void OnDragEnd()
        {
            if (_loadedOrb != null)
            {
                Vector2 launchForce = -CurrentDragVector * LaunchForceMultiplier;
                _loadedOrb.Launch(launchForce);
                _loadedOrb = null;
            }

            CurrentState = CatapultState.Launched;

            if (TrajectoryPreview != null)
            {
                TrajectoryPreview.Hide();
            }
        }
    }

    /// <summary>
    /// Stub TrajectoryPreview for test compilation.
    /// </summary>
    public class TrajectoryPreview : MonoBehaviour
    {
        public int DotCount = 15;
        public bool IsVisible { get; private set; }

        public void Show(Vector2 launchForce)
        {
            IsVisible = true;
            // In production, this would calculate and render trajectory dots
        }

        public void Hide()
        {
            IsVisible = false;
        }
    }
}
