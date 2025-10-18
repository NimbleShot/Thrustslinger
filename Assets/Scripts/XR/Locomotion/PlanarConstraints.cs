using UnityEngine;


namespace Thrustslinger.XR
{
[RequireComponent(typeof(Rigidbody))]
public class PlanarConstraint : MonoBehaviour
{
[SerializeField] private MonoBehaviour planeProviderBehaviour; // IPlaneProvider
[SerializeField, Min(0)] private float maxPlanarSpeed = 6f;
[SerializeField, Range(0,1f)] private float positionCorrection = 1f; // 1 = full snap to plane each step


private IPlaneProvider _plane;
private Rigidbody _rb;


private void Awake()
{
_rb = GetComponent<Rigidbody>();
_rb.useGravity = false;
_rb.constraints = RigidbodyConstraints.FreezeRotation; // yaw handled by turn provider
_plane = planeProviderBehaviour as IPlaneProvider;
if (_plane == null)
Debug.LogError("PlanarConstraint requires planeProviderBehaviour implementing IPlaneProvider");
}


private void FixedUpdate()
{
if (_plane == null) return;

// Skip constraint updates when rigidbody is kinematic
// (e.g., during pause or when frozen)
if (_rb.isKinematic) return;

var n = _plane.Normal;


// Remove velocity component along the normal
var v = _rb.linearVelocity;
v -= Vector3.Dot(v, n) * n;


// Clamp planar speed
var speed = v.magnitude;
if (speed > maxPlanarSpeed)
v = v.normalized * maxPlanarSpeed;
_rb.linearVelocity = v;


// Correct out-of-plane drift by projecting position back to the plane
var p = _rb.position;
var dist = Vector3.Dot(p - _plane.PlanePoint, n);
if (Mathf.Abs(dist) > 0.0001f)
{
var correction = -dist * positionCorrection;
_rb.position = p + n * correction;
}
}


// Public API for other systems
public void SetMaxPlanarSpeed(float newMax) => maxPlanarSpeed = Mathf.Max(0, newMax);
}
}