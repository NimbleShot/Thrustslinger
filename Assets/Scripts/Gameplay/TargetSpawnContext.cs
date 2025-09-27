using Thrustslinger.Core;
using Thrustslinger.XR;
using UnityEngine;

namespace Thrustslinger.Gameplay
{
    public class TargetSpawnContext : IPoolSpawnContext
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Transform Parent;
        public IPlaneProvider Plane;
        public float SpeedOverride;
        public bool AssignPlane;

        public void ApplySpawnTransform(Transform instanceTransform)
        {
            if (Parent != null)
            {
                instanceTransform.SetParent(Parent, false);
            }
            else
            {
                instanceTransform.SetParent(null, false);
            }

            instanceTransform.SetPositionAndRotation(Position, Rotation);
        }
    }
}
