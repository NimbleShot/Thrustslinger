using UnityEngine;

#if UNITY_EDITOR
namespace Thrustslinger.Gameplay
{
    // Minimal editor shooter: left click casts a ray from camera and calls Target.OnHit if found.
    public class EditorMouseShooter : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private float maxDistance = 200f;
        [SerializeField] private LayerMask layerMask = ~0; // everything by default
        [SerializeField] private bool preferTargets = true; // if true, ignore non-target hits in front

        private void Reset()
        {
            if (!cam) cam = Camera.main;
        }

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            if (!cam) return;

            if (Input.GetMouseButtonDown(0))
            {
                var ray = cam.ScreenPointToRay(Input.mousePosition);

                if (preferTargets)
                {
                    var hits = Physics.RaycastAll(ray, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
                    if (hits != null && hits.Length > 0)
                    {
                        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                        foreach (var h in hits)
                        {
                            var t = h.collider.GetComponentInParent<Target>();
                            if (t != null)
                            {
                                t.OnHit(h.point, 1f);
#if UNITY_EDITOR
                                Debug.DrawRay(ray.origin, ray.direction * h.distance, Color.yellow, 0.25f);
#endif
                                break;
                            }
                        }
                    }
                }
                else
                {
                    if (Physics.Raycast(ray, out var hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
                    {
                        var t = hit.collider.GetComponentInParent<Target>();
                        if (t != null)
                        {
                            t.OnHit(hit.point, 1f);
#if UNITY_EDITOR
                            Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.yellow, 0.25f);
#endif
                        }
                    }
                }
            }
        }
    }
}
#endif
