using UnityEngine;

namespace Thrustslinger.Gameplay
{
    [DisallowMultipleComponent]
    public class TargetMover : MonoBehaviour
    {
        [Header("Movement towards player plane")]
        [Tooltip("Units per second the target advances towards the player's plane (along the player's forward normal)")]
        [SerializeField] private float speed = 1.5f;
        [Tooltip("Anchor for the player plane. Forward defines the plane normal. Defaults to Main Camera if not set.")]
        [SerializeField] private Transform planeAnchor;
        [Tooltip("Breach threshold along the plane normal (signed distance). Breach occurs when signed distance <= this value.")]
        [SerializeField] private float breachOffset = 0f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug;
    [SerializeField] private bool logBreach = true;

        private Transform _anchor;

        private void OnEnable()
        {
            EnsureAnchor();
        }

        private void EnsureAnchor()
        {
            if (planeAnchor != null)
            {
                _anchor = planeAnchor;
                return;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                _anchor = cam.transform;
            }
        }

        private void Update()
        {
            if (_anchor == null)
            {
                EnsureAnchor();
                if (_anchor == null) return;
            }

            Vector3 normal = _anchor.forward;
            if (normal.sqrMagnitude < 1e-6f) return;
            normal.Normalize();

            // Advance towards the player's plane along -normal
            transform.position += -normal * speed * Time.deltaTime;

            // Breach check: signed distance to plane (plane point=_anchor.position, normal=_anchor.forward)
            float signed = Vector3.Dot(normal, transform.position - _anchor.position);
            if (signed <= breachOffset)
            {
                if (logBreach)
                {
                    string anchorName = _anchor != null ? _anchor.name : "<null>";
                    Debug.Log($"[TargetMover] Breach: '{name}' crossed plane '{anchorName}' (signed={signed:F3} <= offset={breachOffset:F3}) at t={Time.time:F2}s", this);
                }
                var target = GetComponent<Target>();
                if (target != null)
                    target.OnBreach();
                else
                    gameObject.SetActive(false);
            }

#if UNITY_EDITOR
            if (drawDebug)
            {
                Debug.DrawRay(transform.position, normal * 0.25f, Color.magenta, 0f, false);
            }
#endif
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (planeAnchor == null) return;
            var normal = planeAnchor.forward;
            if (normal.sqrMagnitude < 1e-6f) return;
            normal.Normalize();
            var point = planeAnchor.position + normal * breachOffset;

            // Draw a small square to visualize the breach plane and a normal ray
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
            var right = Vector3.Cross(normal, Vector3.up);
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            right.Normalize();
            var up = Vector3.Cross(right, normal).normalized;
            float s = 0.25f;
            Vector3 a = point + right * s + up * s;
            Vector3 b = point + right * s - up * s;
            Vector3 c = point - right * s - up * s;
            Vector3 d = point - right * s + up * s;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(point, normal * 0.3f);
        }
#endif
    }
}
