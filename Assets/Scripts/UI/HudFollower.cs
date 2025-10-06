using UnityEngine;

namespace Thrustslinger.UI
{
    /// <summary>
    /// Keeps the HUD anchored in front of the player's view with a gentle smoothing effect.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudFollower : MonoBehaviour
    {
        [Header("Tracking")]
        [Tooltip("Explicit head transform to follow (e.g., XR Rig Camera). If null the main camera is used.")]
        [SerializeField] private Transform head;

        [Header("Offsets")]
        [Tooltip("Base distance in meters in front of the gaze direction.")]
        [SerializeField, Min(0.1f)] private float followDistance = 1.5f;
        [Tooltip("Additional local offset applied relative to the head orientation.")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, -0.05f, 0f);

        [Header("Smoothing")]
        [Tooltip("Smooth damp time for positional follow (seconds). Lower values = snappier movement.")]
        [SerializeField, Min(0f)] private float positionSmoothTime = 0.12f;
        [Tooltip("Lerp speed for rotation smoothing (degrees per second). Set to 0 for instant alignment.")]
        [SerializeField, Min(0f)] private float rotationLerpSpeed = 360f;
        [Tooltip("If true the HUD stays upright (roll/pitch removed) while facing the gaze azimuth.")]
        [SerializeField] private bool keepUpright = true;

        private Transform _resolvedHead;
        private Vector3 _velocity;

        private void Awake()
        {
            ResolveHeadTransform(force: true);
        }

        private void LateUpdate()
        {
            var targetHead = ResolveHeadTransform(force: false);
            if (targetHead == null)
            {
                return;
            }

            var baseForward = targetHead.forward;
            if (baseForward.sqrMagnitude < 1e-4f)
            {
                baseForward = targetHead.TransformDirection(Vector3.forward);
            }

            var targetPosition = targetHead.position + baseForward.normalized * followDistance;
            targetPosition += targetHead.TransformVector(localOffset);

            if (positionSmoothTime > 0f)
            {
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, positionSmoothTime);
            }
            else
            {
                transform.position = targetPosition;
            }

            var desiredForward = baseForward;
            var desiredUp = targetHead.up;

            if (keepUpright)
            {
                desiredForward = Vector3.ProjectOnPlane(desiredForward, Vector3.up);
                if (desiredForward.sqrMagnitude < 1e-4f)
                {
                    desiredForward = targetHead.forward;
                }
                desiredForward = desiredForward.normalized;
                desiredUp = Vector3.up;
            }
            else
            {
                desiredForward = desiredForward.normalized;
                desiredUp = desiredUp.normalized;
            }

            if (desiredForward.sqrMagnitude < 1e-4f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(desiredForward, desiredUp);
            if (rotationLerpSpeed <= 0f)
            {
                transform.rotation = targetRotation;
            }
            else
            {
                var step = rotationLerpSpeed * Mathf.Deg2Rad * Time.deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-step));
            }
        }

        /// <summary>
        /// Allows runtime assignment of the head transform.
        /// </summary>
        public void SetHead(Transform newHead)
        {
            head = newHead;
            _resolvedHead = newHead;
        }

        private Transform ResolveHeadTransform(bool force)
        {
            if (!force && _resolvedHead != null)
            {
                return _resolvedHead;
            }

            if (head != null)
            {
                _resolvedHead = head;
                return _resolvedHead;
            }

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                _resolvedHead = mainCamera.transform;
                return _resolvedHead;
            }

            var anyCamera = FindFirstObjectByType<Camera>();
            _resolvedHead = anyCamera != null ? anyCamera.transform : null;
            return _resolvedHead;
        }
    }
}
