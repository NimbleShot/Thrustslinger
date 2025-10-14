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
        [Tooltip("PlayerPlaneDefinition that defines both the plane and its dimensions; auto-found if not assigned.")]
        [SerializeField] private Thrustslinger.XR.PlayerPlaneDefinition playerPlaneDefinition;

        [Header("Material")]
        [Tooltip("Material to use for the walls. Required.")]
        [SerializeField] private Material wallMaterial;
        
        [Header("Dimensions")]
        [SerializeField, Min(0.1f)] private float wallHeight = 2.2f;
        [Tooltip("Offset along the plane normal (positive = towards targets, negative = behind player).")]
        [SerializeField] private float normalOffset = 0f;
        [Tooltip("Distance to push walls outward from the boundary edges (prevents walls from disappearing when player gets close).")]
        [SerializeField, Min(0f)] private float edgeOffset = 0.5f;

        [Header("Options")]
        [SerializeField] private bool drawEdgeGizmos = true;

        private static Mesh s_Quad;

        private Transform _left, _right, _top, _bottom;
        private Vector2 _prevHalfExtents;
        private Vector2 _prevCenter;
        private Vector3 _prevPlanePoint;
        private Vector3 _prevNormal;
        private float _prevNormalOffset;
        private float _prevEdgeOffset;

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
            if (playerPlaneDefinition == null)
            {
                // Auto-find PlayerPlaneDefinition in scene
                playerPlaneDefinition = FindFirstObjectByType<Thrustslinger.XR.PlayerPlaneDefinition>();
                if (playerPlaneDefinition != null)
                {
                    Debug.LogWarning($"PlanarRectWalls auto-found PlayerPlaneDefinition on '{playerPlaneDefinition.name}'. Consider assigning it explicitly.", this);
                }
            }

            if (wallMaterial == null)
            {
                Debug.LogError("PlanarRectWalls requires a Wall Material to be assigned.", this);
            }
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
                mr.sharedMaterial = wallMaterial;
                t = go.transform;
            }
            else
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = GetQuad();
                var mr = t.GetComponent<MeshRenderer>();
                if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = wallMaterial;
            }
            return t;
        }

        private void ApplyMaterial()
        {
            if (wallMaterial == null) return;
            
            foreach (var t in new[] { _left, _right, _top, _bottom })
            {
                if (t == null) continue;
                var mr = t.GetComponent<MeshRenderer>();
                if (mr) mr.sharedMaterial = wallMaterial;
            }
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
            if (playerPlaneDefinition == null || wallMaterial == null)
                return;

            var n = playerPlaneDefinition.Normal;
            GetPlaneAxes(n, out var axisX, out var axisY);

            // Get dimensions directly from PlayerPlaneDefinition
            var half = playerPlaneDefinition.HalfExtents;
            var center = playerPlaneDefinition.CenterOffset;
            var p0 = playerPlaneDefinition.PlanePoint;

            bool changed = force ||
                           half != _prevHalfExtents ||
                           center != _prevCenter ||
                           p0 != _prevPlanePoint ||
                           n != _prevNormal ||
                           !Mathf.Approximately(normalOffset, _prevNormalOffset) ||
                           !Mathf.Approximately(edgeOffset, _prevEdgeOffset);

            if (!changed) return;

            // Apply normal offset to shift walls along plane normal (Z-axis in plane space)
            var offsetP0 = p0 + n * normalOffset;
            var centerWorld = offsetP0 + axisX * center.x + axisY * center.y;
            
            // Apply edge offset to push walls outward from boundary
            var hx = half.x + edgeOffset;
            var hy = half.y + edgeOffset;

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
            _prevNormalOffset = normalOffset;
            _prevEdgeOffset = edgeOffset;
        }

        private static void PositionWall(Transform wall, Vector3 center, float width, float height, Vector3 widthDir, Vector3 upDir, Vector3 forward)
        {
            if (wall == null) return;
            wall.position = center;
            wall.rotation = Quaternion.LookRotation(forward, upDir);
            wall.localScale = new Vector3(width, height, 1f);
        }

        private void GetPlaneAxes(in Vector3 normal, out Vector3 xAxis, out Vector3 yAxis)
        {
            // Use PlayerPlaneDefinition's transform for consistent basis
            var refRight = playerPlaneDefinition ? playerPlaneDefinition.transform.right : Vector3.right;
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
            if (!drawEdgeGizmos || playerPlaneDefinition == null) return;
            var n = playerPlaneDefinition.Normal;
            GetPlaneAxes(n, out var axisX, out var axisY);
            var p0 = Application.isPlaying ? playerPlaneDefinition.PlanePoint : playerPlaneDefinition.PlanePoint;
            var half = playerPlaneDefinition.HalfExtents;
            var center = playerPlaneDefinition.CenterOffset;
            
            // Apply normal offset to gizmo visualization
            var offsetP0 = p0 + n * normalOffset;
            var centerWorld = offsetP0 + axisX * center.x + axisY * center.y;
            
            // Apply edge offset to show actual wall positions
            var hx = half.x + edgeOffset;
            var hy = half.y + edgeOffset;
            
            var c0 = centerWorld + axisX * (-hx) + axisY * (-hy);
            var c1 = centerWorld + axisX * ( hx) + axisY * (-hy);
            var c2 = centerWorld + axisX * ( hx) + axisY * ( hy);
            var c3 = centerWorld + axisX * (-hx) + axisY * ( hy);
            var prev = Gizmos.color;
            
            // Use material color if available
            if (wallMaterial != null && wallMaterial.HasProperty("_BaseColor"))
            {
                var gizmoColor = wallMaterial.GetColor("_BaseColor");
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            }
            else if (wallMaterial != null && wallMaterial.HasProperty("_Color"))
            {
                var gizmoColor = wallMaterial.GetColor("_Color");
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            }
            else
            {
                Gizmos.color = new Color(0.65f, 0.95f, 1f, 1f); // Default cyan
            }
            
            Gizmos.DrawLine(c0, c1);
            Gizmos.DrawLine(c1, c2);
            Gizmos.DrawLine(c2, c3);
            Gizmos.DrawLine(c3, c0);
            
            // Draw normal offset indicator if offset is non-zero
            if (!Mathf.Approximately(normalOffset, 0f))
            {
                Gizmos.color = Color.yellow;
                var originalCenter = p0 + axisX * center.x + axisY * center.y;
                Gizmos.DrawLine(originalCenter, centerWorld);
                Gizmos.DrawWireSphere(centerWorld, 0.1f);
            }
            
            Gizmos.color = prev;
        }
    }
}
