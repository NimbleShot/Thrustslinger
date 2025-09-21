using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_XR_INTERACTION_TOOLKIT
using UnityEngine.XR.Interaction.Toolkit;
#endif


namespace Thrustslinger.XR
{
    [RequireComponent(typeof(Rigidbody))]
    public class ThrusterController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MonoBehaviour planeProviderBehaviour; // IPlaneProvider
        [SerializeField] private ThrusterConfig config;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;


        [Header("Input (Action-based)")]
        [SerializeField] private InputActionProperty leftGripValue; // map to XRI LeftHand/Activate Value
        [SerializeField] private InputActionProperty rightGripValue; // map to XRI RightHand/Activate Value


        [Header("Palm Orientation")]
        [Tooltip("Local axis on the controller that points out of the palm. Commonly -Y for Touch controllers.")]
        public Vector3 localPalmAxis = Vector3.down;

    [Header("Debug")]
    [SerializeField] private bool showDebugHUD = false;
    [SerializeField] private bool logWhenNoThrust = false;

        private IPlaneProvider _plane;
        private Rigidbody _rb;
        private float _leftGrip, _rightGrip;
    private InputAction _leftGripFallback;
    private InputAction _rightGripFallback;
    private InputAction _leftGripBtnFallback;
    private InputAction _rightGripBtnFallback;
    private Vector3 _lastAccel;


#if UNITY_XR_INTERACTION_TOOLKIT
[Header("Optional Haptics")] [SerializeField]
private XRBaseController leftController;
[SerializeField] private XRBaseController rightController;
#endif


        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _plane = planeProviderBehaviour as IPlaneProvider;
            if (_plane == null)
                Debug.LogError("ThrusterController requires planeProviderBehaviour implementing IPlaneProvider");
        }


        private void OnEnable()
        {
            leftGripValue.action?.Enable();
            rightGripValue.action?.Enable();

            // Fallback actions if not wired via inspector InputActionProperty
            if (leftGripValue.action == null)
            {
                _leftGripFallback ??= new InputAction(name: "LeftGripFallback", type: InputActionType.Value, binding: "<XRController>{LeftHand}/{Grip}");
                _leftGripFallback.Enable();
                _leftGripBtnFallback ??= new InputAction(name: "LeftGripButtonFallback", type: InputActionType.Button, binding: "<XRController>{LeftHand}/{GripButton}");
                _leftGripBtnFallback.Enable();
            }
            if (rightGripValue.action == null)
            {
                _rightGripFallback ??= new InputAction(name: "RightGripFallback", type: InputActionType.Value, binding: "<XRController>{RightHand}/{Grip}");
                _rightGripFallback.Enable();
                _rightGripBtnFallback ??= new InputAction(name: "RightGripButtonFallback", type: InputActionType.Button, binding: "<XRController>{RightHand}/{GripButton}");
                _rightGripBtnFallback.Enable();
            }
        }


        private void OnDisable()
        {
            leftGripValue.action?.Disable();
            rightGripValue.action?.Disable();

            _leftGripFallback?.Disable();
            _rightGripFallback?.Disable();
            _leftGripBtnFallback?.Disable();
            _rightGripBtnFallback?.Disable();
        }


        private void Update()
        {
            var lg = leftGripValue.action != null ? leftGripValue.action.ReadValue<float>() : (_leftGripFallback?.ReadValue<float>() ?? 0f);
            var rg = rightGripValue.action != null ? rightGripValue.action.ReadValue<float>() : (_rightGripFallback?.ReadValue<float>() ?? 0f);
            // If analog grip isn't providing values, fall back to button press as 1.0
            if (lg <= 0f && _leftGripBtnFallback != null && _leftGripBtnFallback.IsPressed()) lg = 1f;
            if (rg <= 0f && _rightGripBtnFallback != null && _rightGripBtnFallback.IsPressed()) rg = 1f;
            _leftGrip = Mathf.Clamp01(lg);
            _rightGrip = Mathf.Clamp01(rg);
        }


        private void FixedUpdate()
        {
            if (_plane == null || config == null) return;


            var n = _plane.Normal;
            Vector3 accel = Vector3.zero;

            // Simple linear damping in acceleration space
            if (config.damping > 0f)
            {
                // Use current planar velocity for damping to avoid fighting the constraint
                var v = _rb.linearVelocity;
                v -= Vector3.Dot(v, n) * n; // remove normal component
                accel += -v * Mathf.Clamp01(config.damping);
            }

            // Accumulate thrust from each hand (projected onto plane)
            ApplyHand(ref accel, leftHand, _leftGrip);
            ApplyHand(ref accel, rightHand, _rightGrip);

            if (accel.sqrMagnitude > 0f)
            {
                // Acceleration mode ignores mass so config values are in m/s^2
                _rb.AddForce(accel, ForceMode.Acceleration);
            }
            _lastAccel = accel;
        }


        private void ApplyHand(ref Vector3 accel, Transform hand, float grip)
        {
            if (hand == null) return;

            // Dead zone
            var dz = Mathf.Clamp01(config.gripDeadZone);
            if (grip <= dz) return;

            // Normalize grip [deadZone..1] -> [0..1]
            var t = Mathf.InverseLerp(dz, 1f, grip);
            var strength = Mathf.Max(0f, config.maxAcceleration) * (config.gripToForce?.Evaluate(t) ?? t);

            // Palm world direction and projection onto plane
            var palmWorld = hand.TransformDirection(localPalmAxis).normalized;
            var proj = _plane.Project(palmWorld);
            var mag = proj.magnitude;

            // Ignore if nearly parallel to plane (projection too small)
            if (mag < 1e-3f)
            {
                if (logWhenNoThrust && grip > Mathf.Max(0.2f, config.gripDeadZone + 0.05f))
                {
                    Debug.Log($"[Thruster] No thrust from {(hand==leftHand?"Left":"Right")} hand: proj.magnitude={mag:F4} palmWorld={palmWorld} planeN={_plane.Normal}");
                }
                return;
            }

            var dir = (proj / mag);

            // Thrusters push opposite to the palm normal (hand faces the exhaust)
            accel += -dir * strength;

#if UNITY_XR_INTERACTION_TOOLKIT
            // Optional low-cost haptic tick
            var amplitude = 0.2f + 0.6f * t; // 0.2..0.8
            var duration = 0.01f;
            if (hand == leftHand && leftController)
                leftController.SendHapticImpulse(amplitude, duration);
            else if (hand == rightHand && rightController)
                rightController.SendHapticImpulse(amplitude, duration);
#endif
        }


        private void OnDrawGizmos()
        {
            if (_plane == null || config == null) return;
            Gizmos.color = config.thrustGizmoColor;
            var show = new[] { (leftHand, _leftGrip), (rightHand, _rightGrip) };
            foreach (var (hand, grip) in show)
            {
                if (hand == null) continue;
                var dz = Mathf.Clamp01(config.gripDeadZone);
                if (grip <= dz) continue;
                var palmWorld = hand.TransformDirection(localPalmAxis).normalized;
                var proj = _plane.Project(palmWorld);
                if (proj.sqrMagnitude < 1e-6f) continue;
                var dir = -proj.normalized; // thrust direction
                Gizmos.DrawRay(hand.position, dir * 0.25f);
            }
        }

        private void OnGUI()
        {
            if (!showDebugHUD || _rb == null || _plane == null) return;
            var v = _rb.linearVelocity;
            v -= Vector3.Dot(v, _plane.Normal) * _plane.Normal;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            var txt = $"Thruster Debug\nLGrip: {_leftGrip:F2}  RGrip: {_rightGrip:F2}\nPlanarSpeed: {v.magnitude:F2} m/s\nAccel: {_lastAccel}";
            GUI.Box(new Rect(10, 10, 280, 72), GUIContent.none);
            GUI.Label(new Rect(18, 18, 264, 56), txt, style);
        }
    }
}