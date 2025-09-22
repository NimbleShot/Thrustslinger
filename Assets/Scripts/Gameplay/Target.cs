using UnityEngine;

namespace Thrustslinger.Gameplay
{
    [DisallowMultipleComponent]
    public class Target : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private float maxHp = 1f;
        [SerializeField] private bool disableInsteadOfDestroy = true;
        [SerializeField] private Transform center; // optional manual center

        private float _hp;
        private Collider _collider;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            _hp = Mathf.Max(1f, maxHp);
        }

        /// <summary>
        /// Apply a hit to this target. For now, any hit removes 1 HP and despawns on <= 0.
        /// </summary>
        public void OnHit(Vector3 hitPoint, float damage = 1f)
        {
            _hp -= Mathf.Max(0f, damage);
            if (_hp <= 0f)
            {
                Despawn();
            }
        }

        public void Despawn()
        {
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var c = (center != null ? center.position : transform.position);
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.6f);
            Gizmos.DrawSphere(c, 0.03f);
        }
#endif
    }
}
