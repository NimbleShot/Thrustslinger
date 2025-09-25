using System.Collections;
using UnityEngine;
using Thrustslinger.XR;

namespace Thrustslinger.Gameplay
{
    [DisallowMultipleComponent]
    public class TargetSpawner : MonoBehaviour
    {
        [Header("Plane & Bounds")]
        [Tooltip("The player plane provider (use StaticPlaneProvider on your PlayerPlane object)")]
        [SerializeField] private MonoBehaviour planeProviderBehaviour; // IPlaneProvider
        [Tooltip("Rect bounds to match spawn area size to the player's movement area")]
        [SerializeField] private PlanarRectBounds playerBounds;
        [Tooltip("Transform used to define in-plane axes (usually the PlayerPlane transform)")]
        [SerializeField] private Transform basisTransform; // if null, use playerBounds.basis

        [Header("Spawn Distance")] 
        [Tooltip("How far in front of the player plane to spawn (meters along +Normal)")]
        [SerializeField] private float spawnDistance = 10f;

        [Header("Prefabs")]
        [SerializeField] private GameObject targetPrefab;

        [Header("Dynamic Timing & Spread")]
        [Tooltip("Initial spawn delay range (seconds)")]
        [SerializeField] private Vector2 delayRangeStart = new Vector2(1.5f, 2.5f);
        [Tooltip("Final spawn delay range (seconds) reached at maxDifficultyTime")]
        [SerializeField] private Vector2 delayRangeEnd = new Vector2(0.4f, 0.9f);
        [Tooltip("Initial planar spread half-extents scale (relative to player bounds halfExtents)")]
        [SerializeField] private Vector2 spreadScaleStart = new Vector2(0.4f, 0.3f);
        [Tooltip("Final planar spread half-extents scale (relative to player bounds halfExtents)")]
        [SerializeField] private Vector2 spreadScaleEnd = new Vector2(1.2f, 1.0f);
        [Tooltip("Seconds to reach full difficulty (1.0). Lerp is clamped 0..1.")]
        [SerializeField] private float maxDifficultyTime = 120f;

        [Header("Target Setup")] 
        [Tooltip("Optional: Assign the PlayerPlane provider to new TargetMover instances automatically")]
        [SerializeField] private bool assignMoverPlane = true;
        [Tooltip("Optional override for TargetMover speed on spawned targets (<=0 to keep prefab value)")]
        [SerializeField] private float moverSpeedOverride = 0f;

    [Header("Debug")] 
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool logSpawns = false;

        private IPlaneProvider _plane;
        private float _roundStartTime;

        private void Awake()
        {
            ResolveReferencesIfNeeded();
        }

        private void OnEnable()
        {
            ResolveReferencesIfNeeded();
            _roundStartTime = Time.time;
            StartCoroutine(SpawnLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private IEnumerator SpawnLoop()
        {
            if (_plane == null || playerBounds == null || targetPrefab == null || basisTransform == null)
            {
                Debug.LogWarning($"[TargetSpawner] Missing references. plane={(_plane!=null)} bounds={(playerBounds!=null)} prefab={(targetPrefab!=null)} basis={(basisTransform!=null)}. Assign missing fields.", this);
                yield break;
            }

            while (enabled)
            {
                float t = Mathf.Clamp01((Time.time - _roundStartTime) / Mathf.Max(0.0001f, maxDifficultyTime));

                // Lerp delay range and spread scale by difficulty t
                var delayMin = Mathf.Lerp(delayRangeStart.x, delayRangeEnd.x, t);
                var delayMax = Mathf.Lerp(delayRangeStart.y, delayRangeEnd.y, t);
                float delay = Random.Range(delayMin, delayMax);

                var halfExt = playerBounds.GetHalfExtents();
                var spreadScale = Vector2.Lerp(spreadScaleStart, spreadScaleEnd, t);
                var spawnHalf = new Vector2(Mathf.Abs(halfExt.x) * Mathf.Abs(spreadScale.x), Mathf.Abs(halfExt.y) * Mathf.Abs(spreadScale.y));

                SpawnOne(spawnHalf);
                yield return new WaitForSeconds(delay);
            }
        }

        private void SpawnOne(in Vector2 spawnHalfExtents)
        {
            var n = _plane.Normal;
            // Build RHS basis using player bounds/basis transform
            GetPlaneAxes(n, out var axisX, out var axisY);

            // Random in-plane offset inside rectangle matching player size scaled by spread
            float rx = Random.Range(-spawnHalfExtents.x, spawnHalfExtents.x);
            float ry = Random.Range(-spawnHalfExtents.y, spawnHalfExtents.y);

            var p0 = _plane.PlanePoint;
            var center = p0 + n * spawnDistance; // in front of the plane along +normal
            var spawnPos = center + axisX * rx + axisY * ry;

            var go = Instantiate(targetPrefab, spawnPos, Quaternion.LookRotation(-n, axisY));
            if (logSpawns)
            {
                Debug.Log($"[TargetSpawner] Spawned '{go.name}' at {spawnPos} (n={n}, rx={rx:F2}, ry={ry:F2})", this);
            }

            // Optional: wire TargetMover
            if (assignMoverPlane)
            {
                var mover = go.GetComponent<TargetMover>();
                if (mover == null) mover = go.AddComponent<TargetMover>();
                if (moverSpeedOverride > 0f)
                    mover.SetSpeed(moverSpeedOverride);
                // assign plane (prefer explicit provider on this spawner)
                mover.SetPlane(_plane);
            }
        }

        private void ResolveReferencesIfNeeded()
        {
            // Plane
            if (_plane == null)
            {
                _plane = planeProviderBehaviour as IPlaneProvider;
                if (_plane == null)
                {
                    foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                        if (mb is IPlaneProvider p) { _plane = p; break; }
                }
            }

            // Bounds
            if (playerBounds == null)
            {
                var allBounds = FindObjectsByType<PlanarRectBounds>(FindObjectsSortMode.None);
                if (allBounds != null && allBounds.Length > 0)
                    playerBounds = allBounds[0];
            }

            // Basis
            if (basisTransform == null && playerBounds != null)
            {
                basisTransform = playerBounds.GetBasisTransform();
                if (basisTransform == null)
                {
                    // Try provider transform if available
                    if (planeProviderBehaviour != null)
                        basisTransform = planeProviderBehaviour.transform;
                    else if (_plane is MonoBehaviour mb)
                        basisTransform = mb.transform;
                }
            }
        }

        private void GetPlaneAxes(in Vector3 normal, out Vector3 xAxis, out Vector3 yAxis)
        {
            var refRight = basisTransform ? basisTransform.right : Vector3.right;
            var xProj = Vector3.ProjectOnPlane(refRight, normal);
            if (xProj.sqrMagnitude < 1e-6f)
            {
                var arbitrary = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
                xProj = Vector3.ProjectOnPlane(arbitrary, normal);
            }
            xAxis = xProj.normalized;
            yAxis = Vector3.Cross(normal, xAxis).normalized;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || targetPrefab == null) return;

            var plane = _plane ?? (planeProviderBehaviour as IPlaneProvider);
            var bounds = playerBounds;
            var basis = basisTransform ?? (bounds ? bounds.GetBasisTransform() : null);
            if (plane == null || bounds == null || basis == null) return;

            var n = plane.Normal;
            GetPlaneAxes(n, out var axisX, out var axisY);
            var p0 = plane.PlanePoint;
            var center = p0 + n * spawnDistance;
            var half = bounds.GetHalfExtents();

            // Draw spawn rectangle (at current difficulty midpoint estimate)
            var t = Application.isPlaying ? Mathf.Clamp01((Time.time - _roundStartTime) / Mathf.Max(0.0001f, maxDifficultyTime)) : 0.5f;
            var spread = Vector2.Lerp(spreadScaleStart, spreadScaleEnd, t);
            var spawnHalf = new Vector2(Mathf.Abs(half.x) * Mathf.Abs(spread.x), Mathf.Abs(half.y) * Mathf.Abs(spread.y));

            var c0 = center + axisX * (-spawnHalf.x) + axisY * (-spawnHalf.y);
            var c1 = center + axisX * ( spawnHalf.x) + axisY * (-spawnHalf.y);
            var c2 = center + axisX * ( spawnHalf.x) + axisY * ( spawnHalf.y);
            var c3 = center + axisX * (-spawnHalf.x) + axisY * ( spawnHalf.y);

            var prev = Gizmos.color;
            Gizmos.color = new Color(0.9f, 0.8f, 0.1f, 0.65f);
            Gizmos.DrawLine(c0, c1);
            Gizmos.DrawLine(c1, c2);
            Gizmos.DrawLine(c2, c3);
            Gizmos.DrawLine(c3, c0);
            Gizmos.color = prev;
        }
    }
}
