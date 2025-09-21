using System;
using UnityEngine;

namespace Thrustslinger.Effects
{
    /// <summary>
    /// Generates four translucent (acrylic-like) walls around a rectangular play area
    /// defined by a PlanarRectBounds on a locomotion plane. The walls are vertical panels
    /// rising along the plane normal so they are visible from inside the arena.
    /// </summary>
    [ExecuteAlways]
    public class PlanarRectWalls : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Back-plane provider; auto-found if not assigned.")]
        [SerializeField] private MonoBehaviour planeProviderBehaviour; // IPlaneProvider
        [Tooltip("Planar rect bounds to follow; auto-found if not assigned.")]
        [SerializeField] private MonoBehaviour boundsBehaviour; // PlanarRectBounds

        [Header("Appearance")]
        [SerializeField, Min(0.1f)] private float wallHeight = 2.2f;
        [SerializeField] private Color tint = new Color(0.65f, 0.95f, 1f, 0.18f);
        [SerializeField, Range(0f, 1f)] private float smoothness = 0.9f;
        [SerializeField, Range(0f, 1f)] private float metallic = 0.05f;
        [Tooltip("Optional material override; leave null to auto-create a URP Lit Transparent material.")]
        [SerializeField] private Material materialOverride;

        [Header("Options")]
        [SerializeField] private bool drawEdgeGizmos = true;

        private Thrustslinger.XR.IPlaneProvider _plane;
        private Thrustslinger.XR.PlanarRectBounds _bounds;
        private Material _sharedMat;
        private static Mesh s_Quad;

        private Transform _left, _right, _top, _bottom;
        private Vector2 _prevHalfExtents;
        private Vector2 _prevCenter;
        private Vector3 _prevPlanePoint;
        private Vector3 _prevNormal;

        private void OnEnable()
        {
            ResolveRefs();
            EnsureWalls();
            ApplyMaterial();
            UpdateWalls(force: true);
        }

        private void OnDisable()
        {
            // Keep walls in editor; do nothing
        }

        private void Update()
        {
            // In edit and play, update if anything changed
            UpdateWalls(force: false);
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                ApplyMaterial();
                UpdateWalls(force: true);
            }
        }

        private void ResolveRefs()
        {
            _plane = planeProviderBehaviour as Thrustslinger.XR.IPlaneProvider;
            if (_plane == null)
            {
                // Try find in scene
                foreach (var mb in FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb is Thrustslinger.XR.IPlaneProvider prov)
                    {
                        _plane = prov;
                        break;
                    }
                }
            }

            _bounds = boundsBehaviour as Thrustslinger.XR.PlanarRectBounds;
            if (_bounds == null)
                _bounds = FindObjectOfType<Thrustslinger.XR.PlanarRectBounds>();
        }

        private void EnsureWalls()
        {
            _left = GetOrCreateChild("Wall_Left");
            _right = GetOrCreateChild("Wall_Right");
            _top = GetOrCreateChild("Wall_Top");
            _bottom = GetOrCreateChild("Wall_Bottom");
        }

        private Transform GetOrCreateChild(string name)
        {
            var t = transform.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mf.sharedMesh = GetQuad();
                mr.sharedMaterial = materialOverride ? materialOverride : GetOrCreateMaterial();
                t = go.transform;
            }
            else
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = GetQuad();
                var mr = t.GetComponent<MeshRenderer>();
                if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = materialOverride ? materialOverride : GetOrCreateMaterial();
            }
            return t;
        }

        private void ApplyMaterial()
        {
            var mat = materialOverride ? materialOverride : GetOrCreateMaterial();
            foreach (var t in new[] { _left, _right, _top, _bottom })
            {
                if (t == null) continue;
                var mr = t.GetComponent<MeshRenderer>();
                if (mr) mr.sharedMaterial = mat;
            }
        }

        private Material GetOrCreateMaterial()
        {
            if (_sharedMat != null) return _sharedMat;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("URP/Lit shader not found. Using Standard shader as fallback.");
                shader = Shader.Find("Standard");
            }
            _sharedMat = new Material(shader)
            {
                name = "AcrylicWalls (Auto)"
            };

            // Configure for transparent acrylic look (URP Lit)
            _sharedMat.SetColor("_BaseColor", tint);
            _sharedMat.SetFloat("_Smoothness", smoothness);
            _sharedMat.SetFloat("_Metallic", metallic);

            // URP Lit transparency setup
            _sharedMat.SetFloat("_Surface", 1f); // Transparent
            _sharedMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _sharedMat.SetFloat("_ZWrite", 0f);
            _sharedMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return _sharedMat;
        }

        private static Mesh GetQuad()
        {
            if (s_Quad != null) return s_Quad;
            s_Quad = new Mesh { name = "ProceduralQuad" };
            // Quad in XY plane, facing +Z
            s_Quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f)
            };
            s_Quad.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            s_Quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            s_Quad.RecalculateNormals();
            s_Quad.RecalculateTangents();
            return s_Quad;
        }

        private void UpdateWalls(bool force)
        {
            ResolveRefs();
            if (_plane == null || _bounds == null)
                return;

            var n = _plane.Normal;
            GetPlaneAxes(n, _bounds, out var axisX, out var axisY);

            // Derive rect params
            var half = GetHalfExtents(_bounds);
            var center = GetCenterOffset(_bounds);
            var p0 = _plane.PlanePoint;

            bool changed = force ||
                           half != _prevHalfExtents ||
                           center != _prevCenter ||
                           p0 != _prevPlanePoint ||
                           n != _prevNormal;

            if (!changed) return;

            var centerWorld = p0 + axisX * center.x + axisY * center.y;
            var hx = half.x;
            var hy = half.y;

            // Left/Right walls: width along axisY, height along n, forward points inward (+X for left, -X for right)
            PositionWall(_left, centerWorld + axisX * (-hx), width: hy * 2f, height: wallHeight, widthDir: axisY, upDir: n, forward: axisX);
            PositionWall(_right, centerWorld + axisX * ( hx), width: hy * 2f, height: wallHeight, widthDir: axisY, upDir: n, forward: -axisX);

            // Bottom/Top walls: width along axisX, forward points inward (+Y for bottom, -Y for top)
            PositionWall(_bottom, centerWorld + axisY * (-hy), width: hx * 2f, height: wallHeight, widthDir: axisX, upDir: n, forward: axisY);
            PositionWall(_top, centerWorld + axisY * ( hy), width: hx * 2f, height: wallHeight, widthDir: axisX, upDir: n, forward: -axisY);

            _prevHalfExtents = half;
            _prevCenter = center;
            _prevPlanePoint = p0;
            _prevNormal = n;
        }

        private static void PositionWall(Transform wall, Vector3 center, float width, float height, Vector3 widthDir, Vector3 upDir, Vector3 forward)
        {
            if (wall == null) return;
            wall.position = center;
            wall.rotation = Quaternion.LookRotation(forward, upDir);
            wall.localScale = new Vector3(width, height, 1f);
        }

        private static Vector2 GetHalfExtents(Thrustslinger.XR.PlanarRectBounds b)
        {
            // Access via reflection to keep fields private; fallback to serialized copy if needed
            var fi = typeof(Thrustslinger.XR.PlanarRectBounds).GetField("halfExtents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null) return (Vector2)fi.GetValue(b);
            return new Vector2(5f, 3f);
        }

        private static Vector2 GetCenterOffset(Thrustslinger.XR.PlanarRectBounds b)
        {
            var fi = typeof(Thrustslinger.XR.PlanarRectBounds).GetField("centerOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null) return (Vector2)fi.GetValue(b);
            return Vector2.zero;
        }

        private static void GetPlaneAxes(in Vector3 normal, Thrustslinger.XR.PlanarRectBounds bounds, out Vector3 xAxis, out Vector3 yAxis)
        {
            // Try to use the same basis as bounds for consistency by peeking into its basisTransform
            var fi = typeof(Thrustslinger.XR.PlanarRectBounds).GetField("basisTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Transform basis = fi != null ? (Transform)fi.GetValue(bounds) : null;
            var refRight = basis ? basis.right : Vector3.right;
            var xProj = Vector3.ProjectOnPlane(refRight, normal);
            if (xProj.sqrMagnitude < 1e-6f)
            {
                var arbitrary = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
                xProj = Vector3.ProjectOnPlane(arbitrary, normal);
            }
            xAxis = xProj.normalized;
            yAxis = Vector3.Cross(normal, xAxis).normalized;
        }

        private void OnDrawGizmos()
        {
            if (!drawEdgeGizmos || _plane == null || _bounds == null) return;
            var n = _plane.Normal;
            GetPlaneAxes(n, _bounds, out var axisX, out var axisY);
            var p0 = _plane.PlanePoint;
            var half = GetHalfExtents(_bounds);
            var center = GetCenterOffset(_bounds);
            var centerWorld = p0 + axisX * center.x + axisY * center.y;
            var hx = half.x; var hy = half.y;
            var c0 = centerWorld + axisX * (-hx) + axisY * (-hy);
            var c1 = centerWorld + axisX * ( hx) + axisY * (-hy);
            var c2 = centerWorld + axisX * ( hx) + axisY * ( hy);
            var c3 = centerWorld + axisX * (-hx) + axisY * ( hy);
            var prev = Gizmos.color;
            Gizmos.color = new Color(tint.r, tint.g, tint.b, 1f);
            Gizmos.DrawLine(c0, c1);
            Gizmos.DrawLine(c1, c2);
            Gizmos.DrawLine(c2, c3);
            Gizmos.DrawLine(c3, c0);
            Gizmos.color = prev;
        }
    }
}
