using UnityEngine;
using Thrustslinger.Core;

namespace Thrustslinger.Gameplay
{
    [DisallowMultipleComponent]
    public class Target : MonoBehaviour, IPoolable
    {
        [Header("State")]
        [SerializeField] private float maxHp = 1f;
        [SerializeField] private bool disableInsteadOfDestroy = true;
        [Tooltip("Damage applied to the player if this target breaches the plane.")]
        [SerializeField, Min(0f)] private float breachDamage = 10f;
        [SerializeField] private Transform center; // optional manual center
    [Header("Scoring")]
    [Tooltip("Identifier used when reporting kills to the score service.")]
    [SerializeField] private string archetypeId = "targets.generic";

        private float _hp;
        private Collider _collider;
        private TargetMover _mover;
        private PooledObject _pooledObject;
        private TargetSpawner _spawner;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _mover = GetComponent<TargetMover>();
        }

        private void OnEnable()
        {
            ResetHp();
        }

        /// <summary>
        /// Apply a hit to this target. For now, any hit removes 1 HP and despawns on <= 0.
        /// </summary>
        public void OnHit(Vector3 hitPoint, float damage = 1f)
        {
            _hp -= Mathf.Max(0f, damage);
            if (_hp <= 0f)
            {
                ReportKill(hitPoint);
                Despawn();
            }
        }

        /// <summary>
        /// Called when the target breaches the player's plane. Applies player damage then despawns.
        /// </summary>
        public void OnBreach()
        {
#if UNITY_EDITOR
            Debug.Log($"[Target] OnBreach -> despawn '{name}'", this);
#endif
            var damage = Mathf.Max(0f, breachDamage);
            var manager = GameManager.Instance;
            manager.NotifyPlaneBreach(damage);
            Despawn();
        }

        public void Despawn()
        {
            if (ReleaseToPool())
                return;

            if (disableInsteadOfDestroy)
            {
                gameObject.SetActive(false);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Returns a 0..1 offset of hit from the center based on collider bounds extents (for future scoring).
        /// 0 = dead center; 1 = at or beyond nominal radius.
        /// </summary>
        public float GetHitOffset01(Vector3 hitPoint)
        {
            var c = (center != null ? center.position : transform.position);

            // Project hitPoint onto the target's local forward plane for consistency
            var toHit = hitPoint - c;
            var planeNormal = transform.forward; // assume target faces +Z forward towards player
            // Remove depth along normal to measure planar offset
            toHit -= Vector3.Project(toHit, planeNormal);

            // Nominal radius from collider bounds
            float radius = 0.5f;
            if (_collider != null)
            {
                var r = _collider.bounds.extents;
                radius = Mathf.Max(r.x, r.y, r.z);
            }
            radius = Mathf.Max(0.0001f, radius);

            return Mathf.Clamp01(toHit.magnitude / radius);
        }

        public void OnSpawned(object context)
        {
            CachePoolBinding();
            ResetHp();

            if (context is TargetSpawnContext spawnContext)
            {
                _spawner = spawnContext.Spawner;

                if (spawnContext.AssignPlane || spawnContext.SpeedOverride > 0f)
                {
                    var mover = EnsureMover();
                    if (mover != null)
                    {
                        mover.SetPlane(spawnContext.AssignPlane ? spawnContext.Plane : null);
                        if (spawnContext.SpeedOverride > 0f)
                        {
                            mover.SetSpeed(spawnContext.SpeedOverride);
                        }
                    }
                }
            }
            else
            {
                _spawner = null;
            }
        }

        public void OnDespawned()
        {
            if (_mover != null)
            {
                _mover.SetPlane(null);
            }

            if (_spawner != null)
            {
                _spawner.NotifyTargetDespawned(this);
                _spawner = null;
            }
        }

        private void ResetHp()
        {
            _hp = Mathf.Max(1f, maxHp);
        }

        private TargetMover EnsureMover()
        {
            if (_mover == null)
            {
                if (!TryGetComponent(out _mover))
                {
                    _mover = gameObject.AddComponent<TargetMover>();
                }
            }

            return _mover;
        }

        private void CachePoolBinding()
        {
            if (_pooledObject == null)
            {
                TryGetComponent(out _pooledObject);
            }
        }

        private bool ReleaseToPool()
        {
            if (!Application.isPlaying)
                return false;

            CachePoolBinding();
            if (_pooledObject == null)
                return false;

            PoolService.Instance.Release(this);
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var c = (center != null ? center.position : transform.position);
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.6f);
            Gizmos.DrawSphere(c, 0.03f);
        }
#endif

        private void ReportKill(Vector3 hitPoint)
        {
            var manager = GameManager.Instance;
            if (manager == null || !manager.IsPlaying)
            {
                return;
            }

            var accuracy = ClassifyAccuracyBucket(hitPoint);
            float distance = 0f;
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                distance = Vector3.Distance(mainCamera.transform.position, transform.position);
            }

            var killData = new RunKillData(GetArchetypeId(), distance, accuracy, 0f);
            manager.RegisterKill(killData);
        }

        private string GetArchetypeId()
        {
            return string.IsNullOrWhiteSpace(archetypeId) ? gameObject.name : archetypeId;
        }

        private HitAccuracyBucket ClassifyAccuracyBucket(Vector3 hitPoint)
        {
            var offset = GetHitOffset01(hitPoint);

            if (offset <= 0.1f)
            {
                return HitAccuracyBucket.Center;
            }

            if (offset <= 0.25f)
            {
                return HitAccuracyBucket.NearCenter;
            }

            if (offset <= 0.5f)
            {
                return HitAccuracyBucket.Body;
            }

            return HitAccuracyBucket.Graze;
        }
    }
}
