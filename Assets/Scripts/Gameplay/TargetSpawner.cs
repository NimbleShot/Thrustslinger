using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Thrustslinger.Core;
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

    [Header("Target Speed")]
    [Tooltip("Enable dynamic TargetMover speed override using a lerped min/max range over time")]
    [SerializeField] private bool useDynamicSpeedRange = false;
    [Tooltip("Initial min/max TargetMover speed (m/s) applied when dynamic speed is enabled")]
    [SerializeField] private Vector2 speedRangeStart = new Vector2(2f, 3f);
    [Tooltip("Final min/max TargetMover speed (m/s) reached at maxDifficultyTime")]
    [SerializeField] private Vector2 speedRangeEnd = new Vector2(6f, 8f);

        [Header("Pooling")]
        [Tooltip("Lookup key used when requesting targets from the pool service.")]
        [SerializeField] private string targetPoolKey = "targets.default";
        [Tooltip("Optional parent transform assigned to spawned targets (null = world root).")]
        [SerializeField] private Transform spawnParent;

    [Header("Spawn Limits")]
    [Tooltip("Maximum number of concurrent active targets (0 = unlimited)")]
    [SerializeField, Min(0)] private int maxConcurrentTargets = 0;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool logSpawns = false;

    [Header("Visualize Difficulty (Runtime)")]
    [Tooltip("Draw the current spawn rectangle at spawn distance every frame (LineRenderer in Game view or Debug.DrawLine if disabled)")] 
    [SerializeField] private bool drawRuntimeSpawnArea = true;
    [SerializeField] private Color runtimeRectColor = new Color(0.95f, 0.7f, 0.2f, 1f);
    [Tooltip("Use a LineRenderer (visible in Game view) instead of Debug.DrawLine (requires Gizmos)")]
    [SerializeField] private bool useLineRendererForOutline = true;
    [SerializeField, Min(0.001f)] private float outlineWidth = 0.02f;
    [Tooltip("Show a small on-screen HUD with current difficulty, delay range, and spread")] 
    [SerializeField] private bool showDifficultyHUD = true;
    [SerializeField] private Vector2 hudOffset = new Vector2(12, 12);
    [SerializeField] private int hudFontSize = 12;

        private IPlaneProvider _plane;
        private float _roundStartTime;
        private float _lastDelayChosen;
        private LineRenderer _outlineLR;
        private Material _outlineMaterial;
        private readonly TargetSpawnContext _spawnContext = new TargetSpawnContext();
        private readonly HashSet<Target> _activeTargets = new HashSet<Target>();
        private Vector2 _lastSpeedRange;
        private float _lastSpeedChosen;

        private const float CapacityRetryInterval = 0.1f;

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
            if (_outlineLR) _outlineLR.enabled = false;
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

                // Lerp delay range by difficulty
                var delayMin = Mathf.Lerp(delayRangeStart.x, delayRangeEnd.x, t);
                var delayMax = Mathf.Lerp(delayRangeStart.y, delayRangeEnd.y, t);

                _lastSpeedRange = ComputeSpeedRange(t);

                if (IsAtCapacity())
                {
                    var retry = Mathf.Max(0.01f, Mathf.Min(delayMin, CapacityRetryInterval));
                    yield return new WaitForSeconds(retry);
                    continue;
                }

                float delay = Random.Range(delayMin, delayMax);
                _lastDelayChosen = delay;

                var halfExt = playerBounds.GetHalfExtents();
                var spreadScale = Vector2.Lerp(spreadScaleStart, spreadScaleEnd, t);
                var spawnHalf = new Vector2(Mathf.Abs(halfExt.x) * Mathf.Abs(spreadScale.x), Mathf.Abs(halfExt.y) * Mathf.Abs(spreadScale.y));

                var speedOverride = ChooseSpeedOverride(_lastSpeedRange);
                _lastSpeedChosen = speedOverride;

                SpawnOne(spawnHalf, speedOverride);
                yield return new WaitForSeconds(delay);
            }
        }

        private void SpawnOne(in Vector2 spawnHalfExtents, float speedOverride)
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

            var spawnRot = Quaternion.LookRotation(-n, axisY);
            _spawnContext.Position = spawnPos;
            _spawnContext.Rotation = spawnRot;
            _spawnContext.Parent = spawnParent;
            _spawnContext.AssignPlane = assignMoverPlane;
            _spawnContext.Plane = assignMoverPlane ? _plane : null;
            _spawnContext.SpeedOverride = speedOverride > 0f ? speedOverride : 0f;
            _spawnContext.Spawner = this;

            var target = PoolService.Instance.Get<Target>(targetPoolKey, _spawnContext);
            if (target == null)
            {
                Debug.LogWarning($"[TargetSpawner] Failed to fetch a pooled target for key '{targetPoolKey}'.", this);
                return;
            }

            RegisterActiveTarget(target);

            if (logSpawns)
            {
                Debug.Log($"[TargetSpawner] Spawned '{target.name}' at {spawnPos} (n={n}, rx={rx:F2}, ry={ry:F2}, speed={speedOverride:F2})", target);
            }
        }

        private void LateUpdate()
        {
            // In case references were assigned after enable, try resolving lazily
            if (_plane == null || playerBounds == null || basisTransform == null)
            {
                ResolveReferencesIfNeeded();
            }
            if (!drawRuntimeSpawnArea)
            {
                if (_outlineLR && _outlineLR.enabled) _outlineLR.enabled = false;
                return;
            }
            if (_plane == null || playerBounds == null || basisTransform == null) return;

            float t = Mathf.Clamp01((Time.time - _roundStartTime) / Mathf.Max(0.0001f, maxDifficultyTime));
            var n = _plane.Normal;
            GetPlaneAxes(n, out var axisX, out var axisY);
            var p0 = _plane.PlanePoint;
            var center = p0 + n * spawnDistance;
            var halfPlayer = playerBounds.GetHalfExtents();
            var spread = Vector2.Lerp(spreadScaleStart, spreadScaleEnd, t);
            var spawnHalf = new Vector2(Mathf.Abs(halfPlayer.x) * Mathf.Abs(spread.x), Mathf.Abs(halfPlayer.y) * Mathf.Abs(spread.y));

            var c0 = center + axisX * (-spawnHalf.x) + axisY * (-spawnHalf.y);
            var c1 = center + axisX * ( spawnHalf.x) + axisY * (-spawnHalf.y);
            var c2 = center + axisX * ( spawnHalf.x) + axisY * ( spawnHalf.y);
            var c3 = center + axisX * (-spawnHalf.x) + axisY * ( spawnHalf.y);

            if (useLineRendererForOutline)
            {
                EnsureOutlineRenderer();
                if (_outlineLR)
                {
                    _outlineLR.enabled = true;
                    _outlineLR.startWidth = outlineWidth;
                    _outlineLR.endWidth = outlineWidth;
                    ApplyOutlineColor(runtimeRectColor);
                    _outlineLR.positionCount = 5; // closed loop
                    _outlineLR.SetPosition(0, c0);
                    _outlineLR.SetPosition(1, c1);
                    _outlineLR.SetPosition(2, c2);
                    _outlineLR.SetPosition(3, c3);
                    _outlineLR.SetPosition(4, c0);
                }
            }
            else
            {
                if (_outlineLR && _outlineLR.enabled) _outlineLR.enabled = false; // turn off LR when using debug lines
                // Requires Game view Gizmos to be enabled
                Debug.DrawLine(c0, c1, runtimeRectColor, 0f, false);
                Debug.DrawLine(c1, c2, runtimeRectColor, 0f, false);
                Debug.DrawLine(c2, c3, runtimeRectColor, 0f, false);
                Debug.DrawLine(c3, c0, runtimeRectColor, 0f, false);
            }
        }

        private void OnGUI()
        {
            if (!showDifficultyHUD) return;

            // Simple IMGUI overlay for quick iteration
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = hudFontSize,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };

            float t = Mathf.Clamp01((Time.time - _roundStartTime) / Mathf.Max(0.0001f, maxDifficultyTime));
            var delayMin = Mathf.Lerp(delayRangeStart.x, delayRangeEnd.x, t);
            var delayMax = Mathf.Lerp(delayRangeStart.y, delayRangeEnd.y, t);
            var spread = Vector2.Lerp(spreadScaleStart, spreadScaleEnd, t);
            var half = playerBounds ? playerBounds.GetHalfExtents() : Vector2.zero;
            var spawnHalf = new Vector2(Mathf.Abs(half.x) * Mathf.Abs(spread.x), Mathf.Abs(half.y) * Mathf.Abs(spread.y));
            var speedRangeForHud = ComputeSpeedRange(t);

            string text =
                $"Difficulty t: {t:F2}\n" +
                $"Elapsed: {(Time.time - _roundStartTime):F1}s  MaxTime: {maxDifficultyTime:F0}s\n" +
                $"Delay range: [{delayMin:F2} .. {delayMax:F2}]s  Last: {_lastDelayChosen:F2}s\n" +
                $"Spread scale: x={spread.x:F2}, y={spread.y:F2}\n" +
                $"Spawn half-extents: x={spawnHalf.x:F2}m, y={spawnHalf.y:F2}m\n" +
                $"Spawn distance: {spawnDistance:F1}m\n";

            if (maxConcurrentTargets > 0)
            {
                text += $"Active targets: {_activeTargets.Count}/{maxConcurrentTargets}\n";
            }
            else
            {
                text += $"Active targets: {_activeTargets.Count}\n";
            }

            if (useDynamicSpeedRange)
            {
                text += $"Speed range: [{speedRangeForHud.x:F2} .. {speedRangeForHud.y:F2}]m/s  Last: {_lastSpeedChosen:F2}m/s";
            }
            else if (moverSpeedOverride > 0f)
            {
                text += $"Speed override: {moverSpeedOverride:F2}m/s";
            }
            else
            {
                text += "Speed override: (prefab)";
            }

            var size = style.CalcSize(new GUIContent(text));
            var rect = new Rect(hudOffset.x, hudOffset.y, Mathf.Max(size.x + 12, 240), size.y + 12);
            GUI.Box(rect, text, style);
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

            // Draw spawn rectangle
            // In edit mode (not playing), preview the START spread only to avoid confusion.
            var t = Application.isPlaying ? Mathf.Clamp01((Time.time - _roundStartTime) / Mathf.Max(0.0001f, maxDifficultyTime)) : 0f;
            var spread = Vector2.Lerp(spreadScaleStart, spreadScaleEnd, t);
            var spawnHalf = new Vector2(Mathf.Abs(half.x) * Mathf.Abs(spread.x), Mathf.Abs(half.y) * Mathf.Abs(spread.y));

            var c0 = center + axisX * (-spawnHalf.x) + axisY * (-spawnHalf.y);
            var c1 = center + axisX * ( spawnHalf.x) + axisY * (-spawnHalf.y);
            var c2 = center + axisX * ( spawnHalf.x) + axisY * ( spawnHalf.y);
            var c3 = center + axisX * (-spawnHalf.x) + axisY * ( spawnHalf.y);

            var prev = Gizmos.color;
            // Use the same color as the runtime outline for consistency in editor previews
            var gizmoColor = runtimeRectColor;
            if (!Application.isPlaying)
            {
                // Make sure it's a bit translucent in the Scene view when not playing
                gizmoColor.a = Mathf.Clamp01(gizmoColor.a * 0.75f + 0.15f);
            }
            Gizmos.color = gizmoColor;
            Gizmos.DrawLine(c0, c1);
            Gizmos.DrawLine(c1, c2);
            Gizmos.DrawLine(c2, c3);
            Gizmos.DrawLine(c3, c0);
            Gizmos.color = prev;
        }

        private void OnValidate()
        {
            // Keep outline width sane
            outlineWidth = Mathf.Max(0.001f, outlineWidth);
            if (speedRangeStart.x > speedRangeStart.y)
            {
                var tmp = speedRangeStart.x;
                speedRangeStart.x = speedRangeStart.y;
                speedRangeStart.y = tmp;
            }
            if (speedRangeEnd.x > speedRangeEnd.y)
            {
                var tmp = speedRangeEnd.x;
                speedRangeEnd.x = speedRangeEnd.y;
                speedRangeEnd.y = tmp;
            }
            speedRangeStart.x = Mathf.Max(0f, speedRangeStart.x);
            speedRangeStart.y = Mathf.Max(0f, speedRangeStart.y);
            speedRangeEnd.x = Mathf.Max(0f, speedRangeEnd.x);
            speedRangeEnd.y = Mathf.Max(0f, speedRangeEnd.y);
            maxConcurrentTargets = Mathf.Max(0, maxConcurrentTargets);
            // Try to keep references wired after inspector edits
            if (!Application.isPlaying)
            {
                ResolveReferencesIfNeeded();
            }
            // Reflect inspector changes immediately for LR if it already exists
            if (_outlineLR)
            {
                _outlineLR.startWidth = outlineWidth;
                _outlineLR.endWidth = outlineWidth;
                ApplyOutlineColor(runtimeRectColor);
                _outlineLR.enabled = drawRuntimeSpawnArea && useLineRendererForOutline;
            }
        }

        private Vector2 ComputeSpeedRange(float t)
        {
            if (useDynamicSpeedRange)
            {
                float min = Mathf.Lerp(speedRangeStart.x, speedRangeEnd.x, t);
                float max = Mathf.Lerp(speedRangeStart.y, speedRangeEnd.y, t);
                if (max < min)
                {
                    var swap = min;
                    min = max;
                    max = swap;
                }
                min = Mathf.Max(0f, min);
                max = Mathf.Max(0f, max);
                return new Vector2(min, max);
            }

            if (moverSpeedOverride > 0f)
            {
                return new Vector2(moverSpeedOverride, moverSpeedOverride);
            }

            return Vector2.zero;
        }

        private float ChooseSpeedOverride(in Vector2 speedRange)
        {
            if (useDynamicSpeedRange)
            {
                if (speedRange == Vector2.zero)
                    return 0f;
                if (Mathf.Approximately(speedRange.x, speedRange.y))
                    return speedRange.x;
                return Random.Range(speedRange.x, speedRange.y);
            }

            if (moverSpeedOverride > 0f)
            {
                return moverSpeedOverride;
            }

            return 0f;
        }

        private void RegisterActiveTarget(Target target)
        {
            if (target == null)
                return;

            _activeTargets.Add(target);
        }

        internal void NotifyTargetDespawned(Target target)
        {
            if (target == null)
                return;

            _activeTargets.Remove(target);
        }

        private bool IsAtCapacity()
        {
            return maxConcurrentTargets > 0 && _activeTargets.Count >= maxConcurrentTargets;
        }

        private void EnsureOutlineRenderer()
        {
            if (_outlineLR != null) return;

            var go = new GameObject("SpawnOutline");
            go.transform.SetParent(transform, false);
            _outlineLR = go.AddComponent<LineRenderer>();
            _outlineLR.useWorldSpace = true;
            _outlineLR.loop = false;
            _outlineLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _outlineLR.receiveShadows = false;
            _outlineLR.textureMode = LineTextureMode.Stretch;
            // Create a lightweight runtime material suitable for URP
            if (_outlineMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                _outlineMaterial = new Material(shader);
                _outlineMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            _outlineLR.material = _outlineMaterial;
            ApplyOutlineColor(runtimeRectColor);
        }

        private void ApplyOutlineColor(Color c)
        {
            if (_outlineLR)
            {
                // Also set gradient for completeness
                var grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                    new[] { new GradientAlphaKey(c.a, 0f), new GradientAlphaKey(c.a, 1f) }
                );
                _outlineLR.colorGradient = grad;
            }
            if (_outlineMaterial)
            {
                if (_outlineMaterial.HasProperty("_BaseColor"))
                    _outlineMaterial.SetColor("_BaseColor", c);
                else if (_outlineMaterial.HasProperty("_Color"))
                    _outlineMaterial.SetColor("_Color", c);
            }
        }
    }
}
