using UnityEngine;

namespace Thrustslinger.XR
{
    /// <summary>
    /// Unified component that defines the player's locomotion plane AND its rectangular dimensions.
    /// Place this on your PlayerPlane GameObject. It will automatically update the PlanarRectBounds
    /// component on the player root to match the dimensions you set here.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerPlaneDefinition : MonoBehaviour, IPlaneProvider
    {
        [Header("Plane Definition")]
        [Tooltip("Reference transform whose orientation defines the plane normal.")]
        public Transform reference;

        [Tooltip("Local axis on the reference that points along the plane normal.")]
        public Vector3 localNormalAxis = Vector3.forward;

        [Tooltip("Optional fixed point on the plane. If null, uses this transform's position at Start.")]
        public Transform planePointOverride;

        [Header("Plane Dimensions")]
        [Tooltip("Half-size of the plane rectangle (meters). Controls both player movement bounds and max spawn area.")]
        [SerializeField] private Vector2 halfExtents = new Vector2(5f, 3f);
        
        [Tooltip("Center offset from the plane point along in-plane X/Y (meters).")]
        [SerializeField] private Vector2 centerOffset = Vector2.zero;

        [Header("Debug")]
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.75f);
        [SerializeField] private bool drawGizmos = true;

        private Vector3 _planePoint;

        // IPlaneProvider implementation
        public Vector3 Normal => reference ? reference.TransformDirection(localNormalAxis).normalized : transform.forward;
        public Vector3 PlanePoint => _planePoint;
        public Vector3 Project(Vector3 v) => v - Vector3.Dot(v, Normal) * Normal;

        // Public accessors for dimensions
        public Vector2 HalfExtents => halfExtents;
        public Vector2 CenterOffset => centerOffset;

        private void Reset()
        {
            reference = transform;
            localNormalAxis = Vector3.forward;
        }

        private void Start()
        {
            _planePoint = planePointOverride ? planePointOverride.position : transform.position;
        }

        private void OnValidate()
        {
            // Ensure positive dimensions
            halfExtents.x = Mathf.Max(0.1f, halfExtents.x);
            halfExtents.y = Mathf.Max(0.1f, halfExtents.y);
        }

        /// <summary>
        /// Update the plane dimensions at runtime.
        /// </summary>
        public void SetDimensions(Vector2 newHalfExtents, Vector2 newCenterOffset)
        {
            halfExtents = new Vector2(Mathf.Max(0.1f, newHalfExtents.x), Mathf.Max(0.1f, newHalfExtents.y));
            centerOffset = newCenterOffset;
        }

        /// <summary>
        /// Update just the half extents.
        /// </summary>
        public void SetHalfExtents(Vector2 newHalfExtents)
        {
            SetDimensions(newHalfExtents, centerOffset);
        }

        /// <summary>
        /// Update just the center offset.
        /// </summary>
        public void SetCenterOffset(Vector2 newCenterOffset)
        {
            SetDimensions(halfExtents, newCenterOffset);
        }

        private void GetPlaneAxes(in Vector3 normal, out Vector3 xAxis, out Vector3 yAxis)
        {
            var refRight = reference ? reference.right : Vector3.right;
            var xProj = Vector3.ProjectOnPlane(refRight, normal);
            if (xProj.sqrMagnitude < 1e-6f)
            {
                // Fallback if refRight is nearly parallel to normal
                var arbitrary = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
                xProj = Vector3.ProjectOnPlane(arbitrary, normal);
            }
            xAxis = xProj.normalized;
            yAxis = Vector3.Cross(normal, xAxis).normalized; // ensures RHS basis
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            var n = Normal;
            GetPlaneAxes(n, out var axisX, out var axisY);
            var p0 = Application.isPlaying ? _planePoint : (planePointOverride ? planePointOverride.position : transform.position);
            var center = p0 + axisX * centerOffset.x + axisY * centerOffset.y;

            var hx = halfExtents.x;
            var hy = halfExtents.y;
            var c0 = center + axisX * (-hx) + axisY * (-hy);
            var c1 = center + axisX * ( hx) + axisY * (-hy);
            var c2 = center + axisX * ( hx) + axisY * ( hy);
            var c3 = center + axisX * (-hx) + axisY * ( hy);

            var prev = Gizmos.color;
            Gizmos.color = gizmoColor;
            
            // Draw rectangle
            Gizmos.DrawLine(c0, c1);
            Gizmos.DrawLine(c1, c2);
            Gizmos.DrawLine(c2, c3);
            Gizmos.DrawLine(c3, c0);
            
            // Draw normal ray
            Gizmos.DrawRay(center, n * 0.5f);
            
            // Draw small cross at center
            Gizmos.DrawLine(center - axisX * 0.15f, center + axisX * 0.15f);
            Gizmos.DrawLine(center - axisY * 0.15f, center + axisY * 0.15f);
            
            Gizmos.color = prev;
        }

        // Editor helper - draw plane normal always
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            
            var n = Normal;
            var p = Application.isPlaying ? _planePoint : (planePointOverride ? planePointOverride.position : transform.position);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(p, n * 0.5f);
        }
    }
}
