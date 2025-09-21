using UnityEngine;


namespace Thrustslinger.XR
{
// Simple provider that defines a plane by a Transform and local normal axis
public class StaticPlaneProvider : MonoBehaviour, IPlaneProvider
{
[Tooltip("Reference transform whose orientation defines the plane normal.")]
public Transform reference;


[Tooltip("Local axis on the reference that points along the plane normal.")]
public Vector3 localNormalAxis = Vector3.forward;


[Tooltip("Optional fixed point on the plane. If null, uses this transform's position at Start.")]
public Transform planePointOverride;


private Vector3 _planePoint;


public Vector3 Normal => reference ? reference.TransformDirection(localNormalAxis).normalized : transform.forward;
public Vector3 PlanePoint => _planePoint;
public Vector3 Project(Vector3 v) => v - Vector3.Dot(v, Normal) * Normal;


private void Reset()
{
reference = transform;
localNormalAxis = Vector3.forward;
}


private void Start()
{
_planePoint = planePointOverride ? planePointOverride.position : transform.position;
}


private void OnDrawGizmosSelected()
{
var n = Normal;
var p = Application.isPlaying ? _planePoint : (planePointOverride ? planePointOverride.position : transform.position);
Gizmos.color = Color.cyan;
Gizmos.DrawRay(p, n * 0.5f);
// Draw a small cross to visualize the plane
var t = reference ? reference : transform;
var right = t.right;
var up = t.up;
Gizmos.DrawLine(p - right * 0.25f, p + right * 0.25f);
Gizmos.DrawLine(p - up * 0.25f, p + up * 0.25f);
}
}
}