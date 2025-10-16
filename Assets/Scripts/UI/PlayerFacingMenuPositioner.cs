using UnityEngine;
using Thrustslinger.XR;

namespace Thrustslinger.UI
{
    /// <summary>
    /// Positions a UI menu in front of the player's view with configurable distance and boundary constraints.
    /// Ensures the menu stays within the plane boundaries defined by PlayerPlaneDefinition.
    /// </summary>
    [AddComponentMenu("Thrustslinger/UI/Player Facing Menu Positioner")]
    [DisallowMultipleComponent]
    public sealed class PlayerFacingMenuPositioner : MonoBehaviour
    {
        [Header("Player Reference")]
        [Tooltip("The camera/head transform to face towards. If null, will auto-find Camera.main.")]
        [SerializeField] private Transform playerCamera;

        [Header("Positioning")]
        [Tooltip("Enable to position menu in front of player. If false, menu stays at original position.")]
        [SerializeField] private bool positionInFrontOfPlayer = true;

        [Tooltip("If true, uses plane normal direction. If false, uses camera forward projection for natural view direction.")]
        [SerializeField] private bool usePlaneNormalDirection = false;

        [Tooltip("Distance from the player camera to place the menu (meters).")]
        [SerializeField] private float distanceFromPlayer = 2.0f;

        [Tooltip("Vertical offset from camera eye level (meters). Positive moves up, negative moves down.")]
        [SerializeField] private float verticalOffset = 0.0f;

        [Header("Boundary Constraints")]
        [Tooltip("Reference to PlayerPlaneDefinition for boundary constraints. If null, will auto-find.")]
        [SerializeField] private PlayerPlaneDefinition planeDefinition;

        [Tooltip("Apply boundary constraints from the plane definition.")]
        [SerializeField] private bool applyBoundaryConstraints = true;

        [Tooltip("Additional inset from plane boundaries (meters). Accounts for menu size and prevents wall clipping.")]
        [SerializeField] private Vector2 boundaryInset = new Vector2(0.5f, 0.5f);

        [Header("Orientation")]
        [Tooltip("If true, menu faces camera directly (natural VR). If false, menu orientation respects plane alignment.")]
        [SerializeField] private bool faceCamera = true;

        [Tooltip("Lock rotation around the Y axis to keep menu upright. Only applies when faceCamera is false.")]
        [SerializeField] private bool lockYRotation = true;

        [Header("Debug")]
        [SerializeField] private bool drawDebugGizmos = true;
        [SerializeField] private Color gizmoColor = Color.yellow;

        private Vector3 _lastPosition;
        private bool _hasPositioned;

        private void Awake()
        {
            // Auto-find player camera if not assigned
            if (playerCamera == null)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    playerCamera = mainCamera.transform;
                }
                else
                {
                    Debug.LogWarning("[PlayerFacingMenuPositioner] No player camera assigned and Camera.main not found. Menu positioning disabled.", this);
                    positionInFrontOfPlayer = false;
                }
            }

            // Auto-find plane definition if boundary constraints enabled but not assigned
            if (applyBoundaryConstraints && planeDefinition == null)
            {
                planeDefinition = FindFirstObjectByType<PlayerPlaneDefinition>();
                if (planeDefinition == null)
                {
                    Debug.LogWarning("[PlayerFacingMenuPositioner] Boundary constraints enabled but no PlayerPlaneDefinition found. Constraints disabled.", this);
                    applyBoundaryConstraints = false;
                }
            }
        }

        /// <summary>
        /// Call this when the menu becomes visible to update its position.
        /// </summary>
        public void UpdatePosition()
        {
            if (!positionInFrontOfPlayer || playerCamera == null)
            {
                return;
            }

            Vector3 targetPosition = ComputeTargetPosition();

            // Apply boundary constraints if enabled
            if (applyBoundaryConstraints && planeDefinition != null)
            {
                targetPosition = ClampToPlaneBounds(targetPosition);
            }

            // Set position
            transform.position = targetPosition;
            _lastPosition = targetPosition;
            _hasPositioned = true;

            // Update rotation to face player
            UpdateRotation();
        }

        /// <summary>
        /// Compute the unconstrained target position in front of the player.
        /// </summary>
        private Vector3 ComputeTargetPosition()
        {
            if (playerCamera == null)
            {
                return transform.position;
            }

            Vector3 forward;

            if (usePlaneNormalDirection && planeDefinition != null)
            {
                // Use plane normal as the forward direction (position menu along plane normal from player)
                forward = planeDefinition.Normal;
            }
            else
            {
                // Use camera forward projected onto horizontal plane (natural view direction)
                forward = playerCamera.forward;
                forward.y = 0f;
                
                if (forward.sqrMagnitude < 0.001f)
                {
                    // Camera looking straight up/down, use camera right as fallback
                    forward = playerCamera.right;
                    forward.y = 0f;
                }

                forward.Normalize();
            }

            // Position menu in front of player
            Vector3 basePosition = playerCamera.position + forward * distanceFromPlayer;
            basePosition.y = playerCamera.position.y + verticalOffset;

            return basePosition;
        }

        /// <summary>
        /// Clamp the menu position to stay within the plane boundaries with insets.
        /// </summary>
        private Vector3 ClampToPlaneBounds(Vector3 position)
        {
            if (planeDefinition == null)
            {
                return position;
            }

            // Get plane properties
            Vector3 planeNormal = planeDefinition.Normal;
            Vector3 planePoint = planeDefinition.PlanePoint;
            Vector2 halfExtents = planeDefinition.HalfExtents;
            Vector2 centerOffset = planeDefinition.CenterOffset;

            // Apply insets to create safe zone
            Vector2 safeHalfExtents = new Vector2(
                Mathf.Max(0.1f, halfExtents.x - boundaryInset.x),
                Mathf.Max(0.1f, halfExtents.y - boundaryInset.y)
            );

            // Get plane coordinate system
            GetPlaneAxes(planeNormal, out Vector3 axisX, out Vector3 axisY);

            // Calculate plane center in world space
            Vector3 planeCenter = planePoint + axisX * centerOffset.x + axisY * centerOffset.y;

            // Convert world position to plane-relative coordinates
            Vector3 toPos = position - planeCenter;
            float x = Vector3.Dot(toPos, axisX);
            float y = Vector3.Dot(toPos, axisY);
            float z = Vector3.Dot(toPos, planeNormal);

            // Clamp to safe bounds
            x = Mathf.Clamp(x, -safeHalfExtents.x, safeHalfExtents.x);
            y = Mathf.Clamp(y, -safeHalfExtents.y, safeHalfExtents.y);

            // Reconstruct world position with clamped coordinates
            return planeCenter + axisX * x + axisY * y + planeNormal * z;
        }

        /// <summary>
        /// Update menu rotation to face the player.
        /// </summary>
        private void UpdateRotation()
        {
            if (playerCamera == null)
            {
                return;
            }

            // Calculate direction from menu to camera (we'll negate this for LookRotation)
            Vector3 toCamera = playerCamera.position - transform.position;

            if (toCamera.sqrMagnitude < 0.001f)
            {
                return;
            }

            if (faceCamera)
            {
                // Face camera directly with world up (natural VR behavior)
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude < 0.001f)
                {
                    return;
                }
                
                // Negate direction so menu faces camera (not away from it)
                transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
            }
            else
            {
                // Respect plane orientation
                if (lockYRotation)
                {
                    // Keep menu upright with world up
                    toCamera.y = 0f;
                    if (toCamera.sqrMagnitude < 0.001f)
                    {
                        return;
                    }
                    transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
                }
                else if (planeDefinition != null)
                {
                    // Use plane normal as up vector to respect plane orientation
                    Vector3 planeNormal = planeDefinition.Normal;
                    Vector3 projectedDir = Vector3.ProjectOnPlane(toCamera, planeNormal);
                    
                    if (projectedDir.sqrMagnitude < 0.001f)
                    {
                        // Direction is parallel to normal, use plane's basis
                        GetPlaneAxes(planeNormal, out Vector3 axisX, out _);
                        projectedDir = -axisX; // Negate to face camera
                    }
                    else
                    {
                        projectedDir = -projectedDir.normalized; // Negate to face camera
                    }
                    
                    transform.rotation = Quaternion.LookRotation(projectedDir, planeNormal);
                }
                else
                {
                    // Fallback: free rotation facing camera
                    transform.rotation = Quaternion.LookRotation(-toCamera.normalized);
                }
            }
        }

        /// <summary>
        /// Get the coordinate axes of the plane.
        /// </summary>
        private void GetPlaneAxes(Vector3 normal, out Vector3 xAxis, out Vector3 yAxis)
        {
            // Use plane definition's transform for consistent basis
            Transform refTransform = planeDefinition != null ? planeDefinition.transform : transform;
            Vector3 refRight = refTransform.right;

            Vector3 xProj = Vector3.ProjectOnPlane(refRight, normal);
            if (xProj.sqrMagnitude < 1e-6f)
            {
                // Fallback if refRight is nearly parallel to normal
                Vector3 arbitrary = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
                xProj = Vector3.ProjectOnPlane(arbitrary, normal);
            }
            xAxis = xProj.normalized;
            yAxis = Vector3.Cross(normal, xAxis).normalized;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos || !positionInFrontOfPlayer)
            {
                return;
            }

            if (playerCamera == null)
            {
                return;
            }

            Gizmos.color = gizmoColor;

            // Draw line from camera to menu
            Vector3 menuPos = Application.isPlaying && _hasPositioned ? _lastPosition : ComputeTargetPosition();
            Gizmos.DrawLine(playerCamera.position, menuPos);

            // Draw sphere at target position
            Gizmos.DrawWireSphere(menuPos, 0.1f);

            // Draw boundary constraints if enabled
            if (applyBoundaryConstraints && planeDefinition != null)
            {
                DrawBoundaryGizmo();
            }
        }

        private void DrawBoundaryGizmo()
        {
            if (planeDefinition == null)
            {
                return;
            }

            Vector3 planeNormal = planeDefinition.Normal;
            Vector3 planePoint = planeDefinition.PlanePoint;
            Vector2 halfExtents = planeDefinition.HalfExtents;
            Vector2 centerOffset = planeDefinition.CenterOffset;

            // Apply insets
            Vector2 safeHalfExtents = new Vector2(
                Mathf.Max(0.1f, halfExtents.x - boundaryInset.x),
                Mathf.Max(0.1f, halfExtents.y - boundaryInset.y)
            );

            GetPlaneAxes(planeNormal, out Vector3 axisX, out Vector3 axisY);
            Vector3 center = planePoint + axisX * centerOffset.x + axisY * centerOffset.y;

            // Draw safe boundary rectangle
            Vector3 c0 = center + axisX * (-safeHalfExtents.x) + axisY * (-safeHalfExtents.y);
            Vector3 c1 = center + axisX * ( safeHalfExtents.x) + axisY * (-safeHalfExtents.y);
            Vector3 c2 = center + axisX * ( safeHalfExtents.x) + axisY * ( safeHalfExtents.y);
            Vector3 c3 = center + axisX * (-safeHalfExtents.x) + axisY * ( safeHalfExtents.y);

            Color prevColor = Gizmos.color;
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.5f);
            
            Gizmos.DrawLine(c0, c1);
            Gizmos.DrawLine(c1, c2);
            Gizmos.DrawLine(c2, c3);
            Gizmos.DrawLine(c3, c0);

            Gizmos.color = prevColor;
        }
    }
}
