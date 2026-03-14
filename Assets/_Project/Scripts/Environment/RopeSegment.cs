using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.Environment
{
    /// <summary>
    /// Physics rope built from a chain of small segments connected by DistanceJoint2D.
    /// Configurable segment count, length, and strength. Can be burned by fire
    /// (joints removed sequentially) or frozen (becomes rigid). Used for hanging
    /// structures, pendulums, and bridges.
    /// </summary>
    [DisallowMultipleComponent]
    public class RopeSegment : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Rope Configuration")]

        /// <summary>Number of segments in the rope chain.</summary>
        [SerializeField]
        [Tooltip("Total segments in the rope. More segments = smoother but more expensive.")]
        [Range(2, 50)]
        private int segmentCount = 10;

        /// <summary>Total length of the rope in world units.</summary>
        [SerializeField]
        [Tooltip("Total rope length. Divided evenly among segments.")]
        [Min(0.5f)]
        private float ropeLength = 5f;

        /// <summary>Break force of each joint connecting segments.</summary>
        [SerializeField]
        [Tooltip("Break force for each DistanceJoint2D. Infinity = unbreakable.")]
        [Min(0f)]
        private float jointBreakForce = 500f;

        /// <summary>Mass of each individual rope segment.</summary>
        [SerializeField]
        [Tooltip("Mass per segment. Lower = lighter, more responsive rope.")]
        [Min(0.01f)]
        private float segmentMass = 0.1f;

        /// <summary>Prefab used for each rope segment. If null, uses default circle sprite.</summary>
        [SerializeField]
        [Tooltip("Optional prefab for rope segments. Leave null for default circles.")]
        private GameObject segmentPrefab;

        [Header("Anchor Points")]

        /// <summary>Transform for the top anchor point. If null, uses this transform.</summary>
        [SerializeField]
        [Tooltip("Start point of the rope. Defaults to this GameObject's position.")]
        private Transform startAnchor;

        /// <summary>Optional end anchor. If set, the last segment is fixed to this point.</summary>
        [SerializeField]
        [Tooltip("Optional end anchor. Creates a bridge-like rope when set.")]
        private Transform endAnchor;

        [Header("Fire Interaction")]

        /// <summary>Rate at which fire burns through segments (seconds per segment).</summary>
        [SerializeField]
        [Tooltip("Time in seconds before fire burns through each segment.")]
        [Min(0.1f)]
        private float burnTimePerSegment = 0.5f;

        [Header("Visual")]

        /// <summary>Optional LineRenderer to draw the rope visually.</summary>
        [SerializeField]
        [Tooltip("LineRenderer that follows the rope segments.")]
        private LineRenderer ropeLineRenderer;

        /// <summary>Width of the rope visual.</summary>
        [SerializeField]
        [Min(0.01f)]
        private float ropeWidth = 0.05f;

        #endregion

        #region Public Properties

        /// <summary>All generated rope segment GameObjects.</summary>
        public List<GameObject> Segments => segments;

        /// <summary>Whether the rope is currently on fire.</summary>
        public bool IsBurning { get; private set; }

        /// <summary>Whether the rope has been frozen (rigid).</summary>
        public bool IsFrozen { get; private set; }

        /// <summary>Number of intact segments remaining.</summary>
        public int IntactSegmentCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < segments.Count; i++)
                {
                    if (segments[i] != null) count++;
                }
                return count;
            }
        }

        #endregion

        #region Cached References

        private readonly List<GameObject> segments = new List<GameObject>();
        private readonly List<DistanceJoint2D> joints = new List<DistanceJoint2D>();
        private int burnSegmentIndex;
        private float burnTimer;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            BuildRope();
        }

        private void Update()
        {
            if (IsBurning)
            {
                UpdateBurn();
            }

            UpdateLineRenderer();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Rebuilds the rope from scratch, destroying existing segments.
        /// </summary>
        public void Rebuild()
        {
            DestroyRope();
            BuildRope();
        }

        /// <summary>
        /// Ignites the rope, starting to burn through segments from the start.
        /// </summary>
        public void Ignite()
        {
            if (IsBurning) return;
            if (IsFrozen)
            {
                Thaw();
            }

            IsBurning = true;
            burnSegmentIndex = 0;
            burnTimer = 0f;
        }

        /// <summary>
        /// Extinguishes the fire, stopping the burn progression.
        /// </summary>
        public void Extinguish()
        {
            IsBurning = false;
        }

        /// <summary>
        /// Freezes the rope, making all joints rigid (high break force, no flexibility).
        /// </summary>
        public void Freeze()
        {
            if (IsFrozen) return;
            IsFrozen = true;

            if (IsBurning)
            {
                Extinguish();
            }

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] == null) continue;

                var rb = segments[i].GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.constraints = RigidbodyConstraints2D.FreezeAll;
                }
            }
        }

        /// <summary>
        /// Thaws the rope, restoring normal physics behavior.
        /// </summary>
        public void Thaw()
        {
            if (!IsFrozen) return;
            IsFrozen = false;

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] == null) continue;

                var rb = segments[i].GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.constraints = RigidbodyConstraints2D.None;
                }
            }
        }

        /// <summary>
        /// Cuts the rope at the specified segment index, destroying the joint.
        /// </summary>
        /// <param name="segmentIndex">Index of the segment to cut at.</param>
        public void CutAt(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= joints.Count) return;

            if (joints[segmentIndex] != null)
            {
                Destroy(joints[segmentIndex]);
                joints[segmentIndex] = null;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Builds the rope chain: creates segments, connects them with DistanceJoint2D,
        /// and optionally attaches to anchor points.
        /// </summary>
        private void BuildRope()
        {
            segments.Clear();
            joints.Clear();

            Vector3 startPos = startAnchor != null ? startAnchor.position : transform.position;
            float segmentLength = ropeLength / segmentCount;
            Rigidbody2D previousRb = null;

            // If start anchor has a Rigidbody2D, use it; otherwise treat as kinematic anchor
            if (startAnchor != null)
            {
                previousRb = startAnchor.GetComponent<Rigidbody2D>();
            }

            for (int i = 0; i < segmentCount; i++)
            {
                Vector3 segPos = startPos + Vector3.down * segmentLength * (i + 1);

                GameObject seg;
                if (segmentPrefab != null)
                {
                    seg = Instantiate(segmentPrefab, segPos, Quaternion.identity, transform);
                }
                else
                {
                    seg = CreateDefaultSegment(segPos, segmentLength);
                }

                seg.name = $"RopeSegment_{i}";

                // Configure Rigidbody2D
                var segRb = seg.GetComponent<Rigidbody2D>();
                if (segRb == null)
                {
                    segRb = seg.AddComponent<Rigidbody2D>();
                }
                segRb.mass = segmentMass;
                segRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                // Connect to previous segment or anchor
                var joint = seg.AddComponent<DistanceJoint2D>();
                joint.autoConfigureDistance = false;
                joint.distance = segmentLength;
                joint.maxDistanceOnly = true;
                joint.breakForce = jointBreakForce;

                if (previousRb != null)
                {
                    joint.connectedBody = previousRb;
                }
                else
                {
                    // Anchor to world position (start anchor without Rigidbody2D)
                    joint.connectedBody = null;
                    joint.connectedAnchor = i == 0 ? (Vector2)startPos : (Vector2)segments[i - 1].transform.position;
                }

                segments.Add(seg);
                joints.Add(joint);
                previousRb = segRb;
            }

            // Connect last segment to end anchor if specified
            if (endAnchor != null && segments.Count > 0)
            {
                var lastSeg = segments[segments.Count - 1];
                var endJoint = lastSeg.AddComponent<DistanceJoint2D>();
                endJoint.autoConfigureDistance = false;
                endJoint.distance = 0.1f;
                endJoint.breakForce = jointBreakForce;

                var endRb = endAnchor.GetComponent<Rigidbody2D>();
                if (endRb != null)
                {
                    endJoint.connectedBody = endRb;
                }
                else
                {
                    endJoint.connectedAnchor = endAnchor.position;
                }

                joints.Add(endJoint);
            }

            // Setup line renderer
            if (ropeLineRenderer != null)
            {
                ropeLineRenderer.positionCount = segments.Count + 1;
                ropeLineRenderer.startWidth = ropeWidth;
                ropeLineRenderer.endWidth = ropeWidth;
            }
        }

        /// <summary>
        /// Creates a default rope segment GameObject with a small circle collider and sprite.
        /// </summary>
        private GameObject CreateDefaultSegment(Vector3 position, float size)
        {
            var seg = new GameObject();
            seg.transform.position = position;
            seg.transform.SetParent(transform);

            var collider = seg.AddComponent<CircleCollider2D>();
            collider.radius = size * 0.3f;

            return seg;
        }

        /// <summary>
        /// Destroys all rope segments and clears the lists.
        /// </summary>
        private void DestroyRope()
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null)
                {
                    Destroy(segments[i]);
                }
            }

            segments.Clear();
            joints.Clear();
            IsBurning = false;
            IsFrozen = false;
        }

        /// <summary>
        /// Progresses the fire along the rope, destroying one joint per burn interval.
        /// </summary>
        private void UpdateBurn()
        {
            burnTimer += Time.deltaTime;

            if (burnTimer >= burnTimePerSegment)
            {
                burnTimer = 0f;

                if (burnSegmentIndex < joints.Count)
                {
                    CutAt(burnSegmentIndex);
                    burnSegmentIndex++;
                }
                else
                {
                    // All segments burned through
                    IsBurning = false;
                }
            }
        }

        /// <summary>
        /// Updates the LineRenderer positions to follow the rope segments.
        /// </summary>
        private void UpdateLineRenderer()
        {
            if (ropeLineRenderer == null) return;

            // Start point
            Vector3 start = startAnchor != null ? startAnchor.position : transform.position;

            int validCount = 1;
            ropeLineRenderer.SetPosition(0, start);

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null)
                {
                    ropeLineRenderer.SetPosition(validCount, segments[i].transform.position);
                    validCount++;
                }
            }

            ropeLineRenderer.positionCount = validCount;
        }

        #endregion

        #region Editor Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 0.2f, 0.8f);

            Vector3 start = startAnchor != null ? startAnchor.position : transform.position;
            Vector3 end = endAnchor != null
                ? endAnchor.position
                : start + Vector3.down * ropeLength;

            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(start, 0.05f);
            Gizmos.DrawSphere(end, 0.05f);
        }
#endif

        #endregion
    }
}
