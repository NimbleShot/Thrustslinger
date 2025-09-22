using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace Thrustslinger.Gameplay
{
    /// <summary>
    /// Simple hitscan weapon driven by the right-hand controller trigger (XRI activateAction).
    /// Casts a ray from this transform (or optional muzzle) and applies damage to Target on hit.
    /// </summary>
    public class HitscanWeapon : MonoBehaviour
    {
        [Header("Firing")]
        [Tooltip("Shots per second (continuous while trigger held)")]
        [SerializeField] private float fireRate = 10f; // shots/sec
        [SerializeField] private float damage = 1f;
        [SerializeField] private float maxDistance = 100f;
        [SerializeField] private LayerMask layerMask = Physics.DefaultRaycastLayers;
    [SerializeField] private Transform muzzle; // optional; defaults to this transform
    [SerializeField] private Vector3 localForward = default; // if no muzzle, use this local dir (default Z+)

    [Header("Input")]
    [SerializeField] private InputActionReference fireAction; // preferred if assigned; otherwise we auto-create

        [Header("Debug")] 
        [SerializeField] private bool preferTargets = true; // choose nearest Target from RaycastAll
        [SerializeField] private bool drawDebugRay = true;

    [Header("Visuals")]
    [SerializeField] private bool showBeamInGame = true;
    [SerializeField] private float beamDuration = 0.06f;
    [SerializeField] private float beamWidth = 0.01f;
    [SerializeField] private LineRenderer beam;

        private bool _ownsResolvedFireAction;
private InputAction _resolvedFireAction;
        private bool _isFiringHeld;
        private float _nextFireTime;

        private void Awake()
        {
            if (localForward == default)
                localForward = Vector3.forward;
        }

        private void OnEnable()
        {
            ResolveAndBindAction();
            EnsureBeam();
        }

        private void OnDisable()
        {
            UnbindAction();
            if (beam) beam.enabled = false;
        }

#if UNITY_EDITOR
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogFireEvent(string phase, InputAction.CallbackContext ctx)
        {
            try
            {
                var val = ctx.ReadValue<float>();
                Debug.Log($"[HitscanWeapon] {phase} (value={val:F2}, control={ctx.control?.path})");
            }
            catch
            {
                Debug.Log($"[HitscanWeapon] {phase} (button={ctx.ReadValueAsButton()}, control={ctx.control?.path})");
            }
        }
#endif


private void Update()
        {
            if (!_isFiringHeld) return;
            if (Time.time < _nextFireTime) return;

            FireOnce();
            _nextFireTime = Time.time + (fireRate > 0f ? 1f / fireRate : 0.1f);
        }

private void ResolveAndBindAction()
        {
            UnbindAction();

            _resolvedFireAction = null;
            _ownsResolvedFireAction = false;

            // Prefer explicit InputActionReference assigned in Inspector
            if (fireAction != null)
            {
                _resolvedFireAction = fireAction.action;
            }

            // Final fallback: create an InputAction with several common bindings
            if (_resolvedFireAction == null)
            {
                var ia = new InputAction(name: "Fire (Auto)", type: InputActionType.Button);
                ia.AddBinding("<XRController>{RightHand}/trigger");
                ia.AddBinding("<XRController>/{PrimaryAction}");
                ia.AddBinding("<Gamepad>/rightTrigger");
#if UNITY_EDITOR
                ia.AddBinding("<Mouse>/leftButton");
                ia.AddBinding("<Keyboard>/space");
#endif
                _resolvedFireAction = ia;
                _ownsResolvedFireAction = true;
            }

            if (_resolvedFireAction != null)
            {
                _resolvedFireAction.performed += OnFirePerformed;
                _resolvedFireAction.canceled += OnFireCanceled;
                if (!_resolvedFireAction.enabled)
                    _resolvedFireAction.Enable();
#if UNITY_EDITOR
                Debug.Log($"[HitscanWeapon] Bound fire action: '{_resolvedFireAction.name}' with { _resolvedFireAction.bindings.Count } bindings");
#endif
            }
            else
            {
                Debug.LogWarning("[HitscanWeapon] No fire action resolved. Ensure this is on an ActionBasedController or assign Fire Action.", this);
            }
        }

        private void EnsureBeam()
        {
            if (!showBeamInGame) return;
            if (beam != null) return;

            var lrGo = new GameObject("HitscanBeam");
            lrGo.transform.SetParent(transform, false);
            beam = lrGo.AddComponent<LineRenderer>();
            beam.enabled = false;
            beam.positionCount = 2;
            beam.startWidth = beamWidth;
            beam.endWidth = beamWidth;
            beam.useWorldSpace = true;
            // Optional: color gradient for visibility
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.cyan, 0f), new GradientColorKey(Color.cyan, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            beam.colorGradient = grad;
        }

private void UnbindAction()
        {
            if (_resolvedFireAction != null)
            {
                _resolvedFireAction.performed -= OnFirePerformed;
                _resolvedFireAction.canceled -= OnFireCanceled;
                if (_ownsResolvedFireAction)
                {
                    if (_resolvedFireAction.enabled)
                        _resolvedFireAction.Disable();
                    _resolvedFireAction.Dispose();
                }
            }
            _resolvedFireAction = null;
            _ownsResolvedFireAction = false;
            _isFiringHeld = false;
        }

private void OnFirePerformed(InputAction.CallbackContext ctx)
        {
#if UNITY_EDITOR
            LogFireEvent("performed", ctx);
#endif
            _isFiringHeld = ctx.ReadValueAsButton() || ctx.ReadValue<float>() > 0.5f;
            if (Time.time >= _nextFireTime)
            {
                FireOnce();
                _nextFireTime = Time.time + (fireRate > 0f ? 1f / fireRate : 0.1f);
            }
        }

private void OnFireCanceled(InputAction.CallbackContext ctx)
        {
#if UNITY_EDITOR
            LogFireEvent("canceled", ctx);
#endif
            _isFiringHeld = false;
        }

        private void FireOnce()
        {
            Vector3 origin = muzzle ? muzzle.position : transform.position;
            Vector3 dir = muzzle ? muzzle.forward : transform.TransformDirection(localForward);

            if (drawDebugRay)
                Debug.DrawRay(origin, dir * maxDistance, Color.cyan, 0.1f);

            bool hitSomething = false;
            Vector3 endPoint = origin + dir * maxDistance;

            if (preferTargets)
            {
                var hits = Physics.RaycastAll(origin, dir, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
                float bestDist = float.MaxValue;
                RaycastHit bestHit = default;
                Thrustslinger.Gameplay.Target bestTarget = null;
                foreach (var h in hits)
                {
                    var t = h.collider.GetComponentInParent<Thrustslinger.Gameplay.Target>();
                    if (t == null) continue;
                    if (h.distance < bestDist)
                    {
                        bestDist = h.distance;
                        bestHit = h;
                        bestTarget = t;
                    }
                }
                if (bestTarget != null)
                {
                    hitSomething = true;
                    endPoint = bestHit.point;
                    ApplyHit(bestTarget, bestHit);
                }
            }

            if (!hitSomething)
            {
                if (Physics.Raycast(origin, dir, out var hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
                {
                    endPoint = hit.point;
                    var target = hit.collider.GetComponentInParent<Thrustslinger.Gameplay.Target>();
                    if (target != null)
                    {
                        ApplyHit(target, hit);
                    }
#if UNITY_EDITOR
                    else
                    {
                        Debug.Log($"[HitscanWeapon] Miss (hit {hit.collider.name} at {hit.distance:F1}m)");
                    }
#endif
                }
#if UNITY_EDITOR
                else
                {
                    Debug.Log("[HitscanWeapon] Miss (no hit)");
                }
#endif
            }

            if (showBeamInGame)
            {
                EnsureBeam();
                if (beam)
                {
                    beam.enabled = true;
                    beam.startWidth = beamWidth;
                    beam.endWidth = beamWidth;
                    beam.SetPosition(0, origin);
                    beam.SetPosition(1, endPoint);
                    StopAllCoroutines();
                    StartCoroutine(DisableBeamAfter(beamDuration));
                }
            }
        }

        private IEnumerator DisableBeamAfter(float t)
        {
            yield return new WaitForSeconds(t);
            if (beam)
                beam.enabled = false;
        }

        private void ApplyHit(Thrustslinger.Gameplay.Target target, RaycastHit hit)
        {
            target.OnHit(hit.point, damage);
#if UNITY_EDITOR
            float offset = target.GetHitOffset01(hit.point);
            Debug.Log($"[HitscanWeapon] Hit {target.name} at {hit.distance:F1}m, accuracyOffset={offset:F2}");
#endif
        }
    }
}
