using UnityEngine;

namespace Thrustslinger.XR
{
    /// <summary>
    /// Constrains a Rigidbody/Transform to a rectangular area on a locomotion plane ("back plane").
    /// Use with PlanarConstraint + ThrusterController to keep the player within a box-like arena while moving on the plane.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Transform))]
    public class PlanarRectBounds : MonoBehaviour
    {
        [Header("Plane & Basis")]
        [SerializeField] private MonoBehaviour planeProviderBehaviour; // IPlaneProvider
        [Tooltip("Transform whose right/up define in-plane axes after projection.")]
        [SerializeField] private Transform basisTransform; // optional; if null, uses this.transform

        [Header("Rect (Plane-Space)")]
        [Tooltip("Half-size of the bounds rectangle along in-plane X (right) and Y (up). Units in meters.")]
        [SerializeField] private Vector2 halfExtents = new Vector2(5f, 3f);
        [Tooltip("Center offset from the plane point along in-plane X/Y (meters).")]
        [SerializeField] private Vector2 centerOffset = Vector2.zero;

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
            _plane = planeProviderBehaviour as IPlaneProvider;

            // Fallback: try to auto-find a plane provider in scene if none assigned
            if (_plane == null)
            {
                // First, try local component
                var local = GetComponent<IPlaneProvider>();
                if (local != null)
                    _plane = local;
                else
                {
                    // Then, try any scene object that implements IPlaneProvider
                    foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    {
                        if (mb is IPlaneProvider prov)
                        {
                            _plane = prov;
                            break;
                        }
                    }
                }
            }

            if (_plane == null)
                Debug.LogError("PlanarRectBounds could not locate an IPlaneProvider in the scene", this);

            // Basis: if not set, prefer the provider's transform; fallback to self
            if (basisTransform == null)
            {
                if (_plane is MonoBehaviour mb)
                    basisTransform = mb.transform;
                if (basisTransform == null)
                    basisTransform = transform;
            }
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
            if (_plane == null) return;

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
            var refRight = basisTransform ? basisTransform.right : Vector3.right;
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

        public void SetCenter(Vector2 newCenter) => centerOffset = newCenter;
        public void SetHalfExtents(Vector2 newHalfExtents) => halfExtents = new Vector2(Mathf.Max(0, newHalfExtents.x), Mathf.Max(0, newHalfExtents.y));

    // Public accessors for other systems (e.g., spawners)
    public Vector2 GetHalfExtents() => halfExtents;
    public Vector2 GetCenterOffset() => centerOffset;
    public Transform GetBasisTransform() => basisTransform;
    public MonoBehaviour GetPlaneProviderBehaviour() => planeProviderBehaviour;

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            var plane = planeProviderBehaviour as IPlaneProvider;
            if (plane == null) return;

            var n = plane.Normal;
            GetPlaneAxes(n, out var axisX, out var axisY);
            var p0 = Application.isPlaying ? plane.PlanePoint : (plane.PlanePoint);
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
