using System.Collections;
using Thrustslinger.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Thrustslinger.Gameplay
{
    /// <summary>
    /// Weapon implementation that spawns pooled rigidbody projectiles when the right-hand trigger is pressed.
    /// Supports magazine size, timed reload, and optional carrier velocity inheritance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileWeapon : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Vector3 localForward = Vector3.forward;
        [SerializeField, Min(0f)] private float muzzleVelocity = 18f;
        [SerializeField, Min(0f)] private float projectileLifetime = 5f;
        [SerializeField, Min(0f)] private float projectileDamage = 1f;
        [SerializeField] private bool inheritCarrierVelocity = true;
        [SerializeField] private Rigidbody carrierRigidbody;
        [SerializeField] private Transform projectileSpawnParent;

        [Header("Pooling")]
        [SerializeField] private string projectilePoolKey = "projectiles.default";

        [Header("Firing")]
        [SerializeField, Min(0f)] private float fireRate = 6f;
        [Tooltip("If true, fire rate only limits continuous fire. Manual trigger pulls ignore fire rate.")]
        [SerializeField] private bool allowManualRapidFire = true;
        [SerializeField, Min(1)] private int magazineCapacity = 12;
        [SerializeField] private bool autoReloadOnEmpty = true;

        [Header("Reload")]
        [SerializeField, Min(0f)] private float reloadDuration = 1.5f;

    [Header("Input")]
    [SerializeField] private InputActionProperty fireAction;
    [SerializeField] private InputActionProperty reloadAction;

        [Header("Debug")]
        [SerializeField] private bool logReloads;
        [SerializeField] private bool logShots;

        private int _currentAmmo;
        private bool _isFiringHeld;
        private bool _isReloading;
        private float _nextFireTime;
        private bool _triggerWasPressed; // Track trigger state to detect new pulls

        private Coroutine _reloadRoutine;
        private float _reloadCompleteTime;

        private InputAction _resolvedFireAction;
        private InputAction _resolvedReloadAction;
        private bool _ownsFireAction;
        private bool _ownsReloadAction;

        private readonly ProjectileSpawnContext _spawnContext = new();

        public int CurrentAmmo => _currentAmmo;
        public int MagazineCapacity => magazineCapacity;
        public bool IsReloading => _isReloading;
        public float ReloadTimeRemaining => _isReloading ? Mathf.Max(0f, _reloadCompleteTime - Time.time) : 0f;

        private void Awake()
        {
            if (localForward == default)
            {
                localForward = Vector3.forward;
            }

            _currentAmmo = Mathf.Max(1, magazineCapacity);
            EnsureCarrierRigidbody();
        }

        private void OnEnable()
        {
            ResolveInputActions();
            ResetAmmoIfNeeded();
        }

        private void OnDisable()
        {
            UnbindInputActions();
            StopReloadRoutine();
        }

        private void Update()
        {
            if (_isReloading) return;
            if (!_isFiringHeld) return;
            if (Time.time < _nextFireTime) return;

            TryFire();
        }

        private void ResolveInputActions()
        {
            UnbindInputActions();

            // InputActionProperty stores either a reference or an inline action. Read the assigned action (may be null).
            _resolvedFireAction = fireAction.action;
            _resolvedReloadAction = reloadAction.action;

            if (_resolvedFireAction == null)
            {
                var ia = new InputAction(name: "Fire (Projectile Auto)", type: InputActionType.Button);
                ia.AddBinding("<XRController>{RightHand}/trigger");
                ia.AddBinding("<XRController>/{PrimaryAction}");
                ia.AddBinding("<Gamepad>/rightTrigger");
#if UNITY_EDITOR
                ia.AddBinding("<Mouse>/leftButton");
                ia.AddBinding("<Keyboard>/space");
#endif
                _resolvedFireAction = ia;
                _ownsFireAction = true;
            }

            if (_resolvedReloadAction == null)
            {
                var ia = new InputAction(name: "Reload (Projectile Auto)", type: InputActionType.Button);
                ia.AddBinding("<XRController>{RightHand}/primaryButton");
                ia.AddBinding("<XRController>{RightHand}/secondaryButton");
                ia.AddBinding("<Gamepad>/x");
#if UNITY_EDITOR
                ia.AddBinding("<Keyboard>/r");
#endif
                _resolvedReloadAction = ia;
                _ownsReloadAction = true;
            }

            if (_resolvedFireAction != null)
            {
                _resolvedFireAction.performed += OnFirePerformed;
                _resolvedFireAction.canceled += OnFireCanceled;
                if (!_resolvedFireAction.enabled)
                {
                    _resolvedFireAction.Enable();
                }
            }

            if (_resolvedReloadAction != null)
            {
                _resolvedReloadAction.performed += OnReloadPerformed;
                if (!_resolvedReloadAction.enabled)
                {
                    _resolvedReloadAction.Enable();
                }
            }
        }

        private void UnbindInputActions()
        {
            if (_resolvedFireAction != null)
            {
                _resolvedFireAction.performed -= OnFirePerformed;
                _resolvedFireAction.canceled -= OnFireCanceled;
                if (_ownsFireAction)
                {
                    if (_resolvedFireAction.enabled)
                    {
                        _resolvedFireAction.Disable();
                    }
                    _resolvedFireAction.Dispose();
                }
            }

            if (_resolvedReloadAction != null)
            {
                _resolvedReloadAction.performed -= OnReloadPerformed;
                if (_ownsReloadAction)
                {
                    if (_resolvedReloadAction.enabled)
                    {
                        _resolvedReloadAction.Disable();
                    }
                    _resolvedReloadAction.Dispose();
                }
            }

            _resolvedFireAction = null;
            _resolvedReloadAction = null;
            _ownsFireAction = false;
            _ownsReloadAction = false;
            _isFiringHeld = false;
        }

        private void ResetAmmoIfNeeded()
        {
            if (_currentAmmo <= 0)
            {
                _currentAmmo = Mathf.Max(1, magazineCapacity);
            }
        }

        private void TryFire()
        {
            if (_isReloading) return;
            if (_currentAmmo <= 0)
            {
                if (autoReloadOnEmpty)
                {
                    BeginReload();
                }
                return;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            if (projectilePrefab == null)
            {
                Debug.LogWarning("[ProjectileWeapon] Missing projectile prefab reference.", this);
                return;
            }

            var origin = muzzle ? muzzle.position : transform.position;
            var forward = muzzle ? muzzle.forward : transform.TransformDirection(localForward);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
            }
            var rotation = Quaternion.LookRotation(forward, Vector3.up);

            var velocity = forward.normalized * muzzleVelocity;
            if (inheritCarrierVelocity && carrierRigidbody)
            {
                velocity += carrierRigidbody.linearVelocity;
            }

            _spawnContext.Position = origin;
            _spawnContext.Rotation = rotation;
            _spawnContext.Parent = projectileSpawnParent;
            _spawnContext.Velocity = velocity;
            _spawnContext.HasVelocity = true;
            _spawnContext.Damage = projectileDamage;
            _spawnContext.HasDamage = true;
            _spawnContext.Lifetime = projectileLifetime;
            _spawnContext.HasLifetime = projectileLifetime > 0f;
            _spawnContext.Owner = this;

            var projectile = PoolService.Instance.Get<Projectile>(projectilePoolKey, _spawnContext);
            _spawnContext.ResetTransientFlags();

            if (projectile == null)
            {
                Debug.LogWarning($"[ProjectileWeapon] Failed to spawn projectile for key '{projectilePoolKey}'.", this);
                return;
            }

            _currentAmmo = Mathf.Max(0, _currentAmmo - 1);
            _nextFireTime = Time.time + (fireRate > 0f ? 1f / fireRate : 0.1f);

#if UNITY_EDITOR
            if (logShots)
            {
                Debug.Log($"[ProjectileWeapon] Fired projectile. Ammo remaining: {_currentAmmo}/{magazineCapacity}", this);
            }
#endif

            if (_currentAmmo <= 0 && autoReloadOnEmpty)
            {
                BeginReload();
            }
        }

        private void OnFirePerformed(InputAction.CallbackContext ctx)
        {
            var wasPressed = _isFiringHeld;
            _isFiringHeld = ctx.ReadValueAsButton() || ctx.ReadValue<float>() > 0.5f;
            if (!_isFiringHeld) return;

            // Check if this is a new trigger pull (not held continuously)
            var isNewPull = !wasPressed && _isFiringHeld;
            
            // If manual rapid fire is enabled and this is a new trigger pull, ignore fire rate
            if (allowManualRapidFire && isNewPull)
            {
                TryFire();
            }
            else if (Time.time >= _nextFireTime)
            {
                TryFire();
            }
        }

        private void OnFireCanceled(InputAction.CallbackContext ctx)
        {
            _isFiringHeld = false;
        }

        private void OnReloadPerformed(InputAction.CallbackContext ctx)
        {
            if (!ctx.ReadValueAsButton()) return;
            BeginReload();
        }

        private void BeginReload()
        {
            if (_isReloading) return;
            if (_currentAmmo >= magazineCapacity) return;
            if (!Application.isPlaying) return;

            StopReloadRoutine(); // Clean up any existing reload first
            
            _isReloading = true;
            _reloadCompleteTime = Time.time + reloadDuration;
            _reloadRoutine = StartCoroutine(ReloadRoutine());

#if UNITY_EDITOR
            if (logReloads)
            {
                Debug.Log($"[ProjectileWeapon] Reload started (duration={reloadDuration:F2}s), _isReloading={_isReloading}", this);
            }
#endif
        }

        private IEnumerator ReloadRoutine()
        {
#if UNITY_EDITOR
            if (logReloads)
            {
                Debug.Log($"[ProjectileWeapon] ReloadRoutine started, waiting {reloadDuration:F2}s...", this);
            }
#endif

            if (reloadDuration > 0f)
            {
                yield return new WaitForSeconds(reloadDuration);
            }

            _currentAmmo = Mathf.Max(1, magazineCapacity);
            _isReloading = false;
            _nextFireTime = Time.time;
            _reloadRoutine = null;
            _reloadCompleteTime = 0f;

#if UNITY_EDITOR
            if (logReloads)
            {
                Debug.Log($"[ProjectileWeapon] Reload complete. Ammo={_currentAmmo}/{magazineCapacity}, _isReloading={_isReloading}", this);
            }
#endif
        }

        private void StopReloadRoutine()
        {
            if (_reloadRoutine != null)
            {
                StopCoroutine(_reloadRoutine);
                _reloadRoutine = null;
                _isReloading = false;
                _reloadCompleteTime = 0f;
            }
        }

        private void EnsureCarrierRigidbody()
        {
            if (carrierRigidbody != null) return;
            carrierRigidbody = GetComponentInParent<Rigidbody>();
        }
    }
}
