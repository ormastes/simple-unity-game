using UnityEngine;

namespace ElementalSiege.Launcher
{
    /// <summary>
    /// Renders a dotted trajectory arc while the catapult is in the Aiming state.
    /// Simulates a parabolic path using kinematic equations and displays it via a LineRenderer.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryPreview : MonoBehaviour
    {
        #region Inspector Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("The Catapult component to read aiming data from.")]
        private Catapult _catapult;

        [Header("Trajectory Settings")]
        [SerializeField]
        [Tooltip("Number of sample points along the trajectory arc.")]
        private int _dotCount = 15;

        [SerializeField]
        [Tooltip("Time step between each sample point in seconds. Larger values spread dots farther apart.")]
        private float _timeStep = 0.12f;

        [SerializeField]
        [Tooltip("Minimum time step applied at low power to keep dots visible.")]
        private float _minTimeStep = 0.06f;

        [SerializeField]
        [Tooltip("Maximum time step applied at full power.")]
        private float _maxTimeStep = 0.18f;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Starting width of the trajectory line.")]
        private float _startWidth = 0.12f;

        [SerializeField]
        [Tooltip("Ending width of the trajectory line (tapers off).")]
        private float _endWidth = 0.04f;

        [SerializeField]
        [Tooltip("Alpha value at the launch point.")]
        private float _startAlpha = 0.9f;

        [SerializeField]
        [Tooltip("Alpha value at the farthest predicted point.")]
        private float _endAlpha = 0.1f;

        [SerializeField]
        [Tooltip("Base colour of the trajectory dots.")]
        private Color _baseColor = Color.white;

        #endregion

        #region Private State

        private LineRenderer _lineRenderer;
        private bool _isVisible;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            ConfigureLineRenderer();
            Hide();
        }

        private void OnEnable()
        {
            if (_catapult != null)
            {
                _catapult.OnAiming += HandleAiming;
                _catapult.OnStateChanged += HandleCatapultStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_catapult != null)
            {
                _catapult.OnAiming -= HandleAiming;
                _catapult.OnStateChanged -= HandleCatapultStateChanged;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the trajectory preview. Called automatically when the catapult enters the Aiming state.
        /// </summary>
        public void Show()
        {
            _isVisible = true;
            _lineRenderer.enabled = true;
        }

        /// <summary>
        /// Hides the trajectory preview. Called automatically when the catapult leaves the Aiming state.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _lineRenderer.enabled = false;
        }

        /// <summary>
        /// Manually updates the trajectory with a given launch velocity and origin.
        /// </summary>
        /// <param name="origin">World-space launch origin.</param>
        /// <param name="launchVelocity">Initial velocity vector.</param>
        public void UpdateTrajectory(Vector2 origin, Vector2 launchVelocity)
        {
            if (!_isVisible)
                Show();

            float speed = launchVelocity.magnitude;
            float adaptiveTimeStep = Mathf.Lerp(_minTimeStep, _maxTimeStep, speed / 50f);

            _lineRenderer.positionCount = _dotCount;

            for (int i = 0; i < _dotCount; i++)
            {
                float t = i * adaptiveTimeStep;
                Vector2 point = CalculatePositionAtTime(origin, launchVelocity, t);
                _lineRenderer.SetPosition(i, new Vector3(point.x, point.y, 0f));
            }

            UpdateGradient();
        }

        #endregion

        #region Event Handlers

        private void HandleAiming(Vector2 direction, float normalisedPower)
        {
            if (_catapult == null)
                return;

            Vector2 origin = _catapult.LaunchPosition;
            Vector2 launchVelocity = direction * normalisedPower * _catapult.CalculateLaunchVelocity(
                (Vector3)origin - (Vector3)(direction * normalisedPower)).magnitude;

            // Recalculate properly via the catapult's own method using current drag position.
            Vector3 dragPos = (Vector3)origin - (Vector3)(direction * normalisedPower * 3f);
            launchVelocity = _catapult.CalculateLaunchVelocity(dragPos);

            UpdateTrajectory(origin, launchVelocity);
        }

        private void HandleCatapultStateChanged(Catapult.CatapultState newState)
        {
            if (newState == Catapult.CatapultState.Aiming)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        #endregion

        #region Trajectory Calculation

        /// <summary>
        /// Calculates the world position of a projectile at a given time using kinematic equations.
        /// </summary>
        /// <param name="origin">Launch origin.</param>
        /// <param name="velocity">Initial velocity.</param>
        /// <param name="time">Elapsed time in seconds.</param>
        /// <returns>Predicted world position.</returns>
        private Vector2 CalculatePositionAtTime(Vector2 origin, Vector2 velocity, float time)
        {
            // p = p0 + v*t + 0.5*g*t^2
            Vector2 gravity = Physics2D.gravity;
            return origin + velocity * time + 0.5f * gravity * (time * time);
        }

        #endregion

        #region Visual Configuration

        private void ConfigureLineRenderer()
        {
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.sortingOrder = 5;
            _lineRenderer.textureMode = LineTextureMode.Tile;
            _lineRenderer.widthCurve = AnimationCurve.Linear(0f, _startWidth, 1f, _endWidth);
            _lineRenderer.numCapVertices = 4;
            _lineRenderer.numCornerVertices = 4;

            UpdateGradient();
        }

        private void UpdateGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(_baseColor, 0f),
                    new GradientColorKey(_baseColor, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(_startAlpha, 0f),
                    new GradientAlphaKey(_endAlpha, 1f)
                }
            );
            _lineRenderer.colorGradient = gradient;
        }

        #endregion
    }
}
