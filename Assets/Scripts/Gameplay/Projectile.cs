using Thrustslinger.Core;
using UnityEngine;

namespace Thrustslinger.Gameplay
{
    /// <summary>
    /// Simple pooled projectile with rigidbody physics. Applies damage to Targets on impact and releases back to the pool.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour, IPoolable
    {
        [Header("Physics")]
        [SerializeField] private Rigidbody body;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [SerializeField] private bool resetVelocityOnSpawn = true;

        [Header("Damage & Lifetime")]
        [SerializeField, Min(0f)] private float baseDamage = 1f;
        [SerializeField, Min(0f)] private float defaultLifetime = 5f;
        [SerializeField] private bool destroyOnAnyHit = true;

        [Header("VFX & Audio")]
        [Tooltip("Optional pooled effect key triggered on hit (e.g., impact VFX). Leave empty to skip.")]
        [SerializeField] private string impactEffectPoolKey;
        [SerializeField] private Transform impactEffectAnchor;

        private float _damage;
        private float _expireTime;
        private bool _active;

        private void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Awake()
        {
            if (!body)
            {
                body = GetComponent<Rigidbody>();
            }
        }

        private void OnDisable()
        {
            // Ensure rigidbody is parked when disabled (e.g., on scene unload)
            if (body)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void Update()
        {
            if (!_active) return;
            if (_expireTime > 0f && Time.time >= _expireTime)
            {
                ReleaseSelf();
            }
        }

        public void OnSpawned(object context)
        {
            _active = true;
            _damage = baseDamage;
            var lifetime = defaultLifetime;
            var velocity = transform.forward;
            var angularVelocity = Vector3.zero;

            if (resetVelocityOnSpawn && body)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            var spawnCtx = context as ProjectileSpawnContext;
            if (spawnCtx != null)
            {
                if (spawnCtx.HasDamage)
                {
                    _damage = spawnCtx.Damage;
                }

                if (spawnCtx.HasLifetime)
                {
                    lifetime = Mathf.Max(0f, spawnCtx.Lifetime);
                }

                if (spawnCtx.HasVelocity)
                {
                    velocity = spawnCtx.Velocity;
                }

                if (spawnCtx.HasAngularVelocity)
                {
                    angularVelocity = spawnCtx.AngularVelocity;
                }
            }

            if (body)
            {
                body.linearVelocity = velocity;
                if (spawnCtx != null && spawnCtx.HasAngularVelocity)
                {
                    body.angularVelocity = angularVelocity;
                }
            }

            _expireTime = lifetime > 0f ? Time.time + lifetime : -1f;
        }

        public void OnDespawned()
        {
            _active = false;
            _expireTime = -1f;
            if (body)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_active) return;
            if (!IsLayerAllowed(collision.collider.gameObject.layer)) return;
            HandleImpact(collision.collider, collision.GetContact(0).point);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_active) return;
            if (!IsLayerAllowed(other.gameObject.layer)) return;
            HandleImpact(other, other.ClosestPoint(transform.position));
        }

        private bool IsLayerAllowed(int layer)
        {
            return (hitMask.value & (1 << layer)) != 0;
        }

        private void HandleImpact(Collider collider, Vector3 hitPoint)
        {
            if (!_active) return;

            var target = collider.GetComponentInParent<Target>();
            if (target != null)
            {
                target.OnHit(hitPoint, _damage);
            }

            if (destroyOnAnyHit)
            {
                TriggerImpactEffect(hitPoint, collider.transform.rotation);
                ReleaseSelf();
            }
        }

        private void TriggerImpactEffect(Vector3 hitPoint, Quaternion rotation)
        {
            if (!Application.isPlaying) return;
            if (string.IsNullOrEmpty(impactEffectPoolKey)) return;
            if (!PoolService.Instance.Contains(impactEffectPoolKey)) return;

            var context = new SimpleEffectSpawnContext
            {
                Position = impactEffectAnchor ? impactEffectAnchor.position : hitPoint,
                Rotation = rotation
            };

            PoolService.Instance.Get(impactEffectPoolKey, context);
        }

        private void ReleaseSelf()
        {
            if (!_active) return;
            _active = false;
            if (Application.isPlaying)
            {
                PoolService.Instance.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private sealed class SimpleEffectSpawnContext : IPoolSpawnContext
        {
            public Vector3 Position;
            public Quaternion Rotation;

            public void ApplySpawnTransform(Transform instanceTransform)
            {
                instanceTransform.SetParent(null, false);
                instanceTransform.SetPositionAndRotation(Position, Rotation);
            }
        }
    }
}
