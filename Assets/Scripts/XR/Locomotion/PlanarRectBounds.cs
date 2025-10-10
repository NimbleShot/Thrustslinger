using UnityEngine;

namespace Thrustslinger.XR
{
    /// <summary>
    /// Constrains a Rigidbody/Transform to a rectangular area on a locomotion plane ("back plane").
    /// Use with PlanarConstraint + ThrusterController to keep the player within a box-like arena while moving on the plane.
    /// Dimensions are read from PlayerPlaneDefinition (or any IPlaneProvider that exposes dimensions).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Transform))]
    public class PlanarRectBounds : MonoBehaviour
    {
        [Header("Plane Reference")]
        [Tooltip("PlayerPlaneDefinition component that defines both the plane and its dimensions.")]
        [SerializeField] private PlayerPlaneDefinition playerPlaneDefinition;

        [Header("Behavior")]
        [Tooltip("If true, clamps in FixedUpdate (recommended with Rigidbody).")]
        [SerializeField] private bool clampInFixedUpdate = true;
        [Tooltip("If true and a Rigidbody is present, zero planar velocity components that would push further out when clamped.")]
        [SerializeField] private bool stopVelocityOnClamp = true;

        [Header("Debug")]
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.75f);
        [SerializeField] private bool drawGizmos = true;

        private IPlaneProvider _plane;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            
            // Get plane reference from PlayerPlaneDefinition
            if (playerPlaneDefinition != null)
            {
                _plane = playerPlaneDefinition;
            }
            else
            {
                // Fallback: try to auto-find PlayerPlaneDefinition in scene
                var planeDef = FindAnyObjectByType<PlayerPlaneDefinition>();
                if (planeDef != null)
                {
                    playerPlaneDefinition = planeDef;
                    _plane = planeDef;
                    Debug.LogWarning($"PlanarRectBounds auto-found PlayerPlaneDefinition on '{planeDef.name}'. Consider assigning it explicitly.", this);
                }
            }

            if (_plane == null)
                Debug.LogError("PlanarRectBounds requires a PlayerPlaneDefinition reference", this);
        }

        private void FixedUpdate()
        {
            if (clampInFixedUpdate)
                ClampToBounds();
        }

        private void LateUpdate()
        {
            // If not using physics, allow transform-only clamping each frame
            if (!clampInFixedUpdate && _rb == null)
                ClampToBounds();
        }

        /// <summary>
        /// Clamp the object to the rectangular bounds on the plane.
        /// </summary>
        public void ClampToBounds()
        {
            if (_plane == null || playerPlaneDefinition == null) return;

            // Get dimensions from PlayerPlaneDefinition
            var halfExtents = playerPlaneDefinition.HalfExtents;
            var centerOffset = playerPlaneDefinition.CenterOffset;

            // Compute plane basis
            var n = _plane.Normal;
            GetPlaneAxes(n, out var axisX, out var axisY);

            // Current world position and plane-space coordinates
            var pos = _rb ? _rb.position : transform.position;
            var p0 = _plane.PlanePoint;
            var toPos = pos - p0;
            var x = Vector3.Dot(toPos, axisX);
            var y = Vector3.Dot(toPos, axisY);
            // normal offset is ignored here; PlanarConstraint should correct it

            // Bounds in plane space
            var cx = centerOffset.x;
            var cy = centerOffset.y;
            var minX = cx - halfExtents.x;
            var maxX = cx + halfExtents.x;
            var minY = cy - halfExtents.y;
            var maxY = cy + halfExtents.y;

            var clampedX = Mathf.Clamp(x, minX, maxX);
            var clampedY = Mathf.Clamp(y, minY, maxY);

            bool didClampX = !Mathf.Approximately(clampedX, x);
            bool didClampY = !Mathf.Approximately(clampedY, y);

            if (!didClampX && !didClampY)
                return; // within bounds

            // New target position on the plane
            var newPos = p0 + axisX * clampedX + axisY * clampedY;

            if (_rb)
            {
                _rb.position = newPos;
                if (stopVelocityOnClamp)
                {
                    var v = _rb.linearVelocity;
                    var vx = Vector3.Dot(v, axisX);
                    var vy = Vector3.Dot(v, axisY);
                    var vn = Vector3.Dot(v, n); // keep normal comp to let PlanarConstraint remove it

                    if (didClampX)
                    {
                        // If we were beyond max and moving further out, stop; same for min
                        if (x > maxX && vx > 0f) vx = 0f;
                        else if (x < minX && vx < 0f) vx = 0f;
                    }
                    if (didClampY)
                    {
                        if (y > maxY && vy > 0f) vy = 0f;
                        else if (y < minY && vy < 0f) vy = 0f;
                    }

                    _rb.linearVelocity = axisX * vx + axisY * vy + n * vn;
                }
            }
            else
            {
                transform.position = newPos;
            }
        }

        private void GetPlaneAxes(in Vector3 normal, out Vector3 xAxis, out Vector3 yAxis)
        {
            // Use PlayerPlaneDefinition's transform for basis
            var refRight = playerPlaneDefinition ? playerPlaneDefinition.transform.right : Vector3.right;
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

        // Public accessors for backward compatibility with TargetSpawner
        public Vector2 GetHalfExtents() => playerPlaneDefinition ? playerPlaneDefinition.HalfExtents : Vector2.zero;
        public Vector2 GetCenterOffset() => playerPlaneDefinition ? playerPlaneDefinition.CenterOffset : Vector2.zero;
        public Transform GetBasisTransform() => playerPlaneDefinition ? playerPlaneDefinition.transform : transform;

        private void OnDrawGizmos()
        {
            if (!drawGizmos || playerPlaneDefinition == null) return;

            var plane = playerPlaneDefinition;
            var halfExtents = plane.HalfExtents;
            var centerOffset = plane.CenterOffset;

            var n = plane.Normal;
            GetPlaneAxes(n, out var axisX, out var axisY);
            var p0 = Application.isPlaying ? plane.PlanePoint : plane.PlanePoint;
            var center = p0 + axisX * centerOffset.x + axisY * centerOffset.y;

            var hx = halfExtents.x;
            var hy = halfExtents.y;
            var c0 = center + axisX * (-hx) + axisY * (-hy);
            var c1 = center + axisX * ( hx) + axisY * (-hy);
            var c2 = center + axisX * ( hx) + axisY * ( hy);
            var c3 = center + axisX * (-hx) + axisY * ( hy);

            var prev = Gizmos.color;
            Gizmos.color = gizmoColor;
            Gizmos.DrawLine(c0, c1);
            Gizmos.DrawLine(c1, c2);
            Gizmos.DrawLine(c2, c3);
            Gizmos.DrawLine(c3, c0);
            // Normal ray
            Gizmos.DrawRay(center, n * 0.5f);
            Gizmos.color = prev;
        }
    }
}
