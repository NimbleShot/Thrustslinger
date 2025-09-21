using UnityEngine;


namespace Thrustslinger.XR
{
public interface IPlaneProvider
{
// World-space unit normal of the locomotion plane
Vector3 Normal { get; }
// Returns v projected onto the plane (removes the Normal component)
Vector3 Project(Vector3 v);
// A stable point on the plane (e.g., at spawn). Implementers may return transform.position.
Vector3 PlanePoint { get; }
}
}