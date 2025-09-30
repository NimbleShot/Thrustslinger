using Thrustslinger.Core;
using UnityEngine;

namespace Thrustslinger.Gameplay
{
    /// <summary>
    /// Spawn context supplied when retrieving projectiles from the shared pool.
    /// Carries initial transform, velocity, and gameplay properties.
    /// </summary>
    public sealed class ProjectileSpawnContext : IPoolSpawnContext
    {
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public Transform Parent;

        public Vector3 Velocity;
        public bool HasVelocity;

        public Vector3 AngularVelocity;
        public bool HasAngularVelocity;

        public float Damage;
        public bool HasDamage;

        public float Lifetime;
        public bool HasLifetime;

        public object Owner;

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

        /// <summary>
        /// Clears transient flags so the context can be safely reused without reallocation.
        /// </summary>
        public void ResetTransientFlags()
        {
            HasVelocity = false;
            HasAngularVelocity = false;
            HasDamage = false;
            HasLifetime = false;
            Owner = null;
            Velocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
            Damage = 0f;
            Lifetime = 0f;
        }
    }
}
