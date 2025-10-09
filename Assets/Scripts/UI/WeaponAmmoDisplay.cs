using TMPro;
using UnityEngine;
using Thrustslinger.Core;

namespace Thrustslinger.UI
{
    /// <summary>
    /// World-space UI panel that displays ammo count for a ProjectileWeapon.
    /// Follows a specified transform (typically right hand controller).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponAmmoDisplay : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField, Tooltip("The weapon to display ammo for")]
        private Gameplay.ProjectileWeapon weapon;
        
        [SerializeField, Tooltip("Transform to follow (e.g., right hand controller)")]
        private Transform followTarget;

        [Header("Positioning")]
        [SerializeField, Tooltip("Offset from the follow target in local space")]
        private Vector3 localOffset = new Vector3(0f, 0.05f, 0.1f);
        
        [SerializeField, Tooltip("Should the panel face the follow target or face away?")]
        private bool faceAwayFromTarget = true;
        
        [SerializeField, Tooltip("Smoothing factor for position/rotation (0 = instant, higher = smoother)")]
        [Range(0f, 30f)]
        private float smoothSpeed = 10f;

        [Header("UI References")]
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private TMP_Text reloadText;

        [Header("Formatting")]
        [SerializeField] private string ammoFormat = "{0} / {1}";
        [SerializeField] private string reloadingText = "RELOADING...";
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color lowAmmoColor = Color.yellow;
        [SerializeField] private Color emptyColor = Color.red;
        [SerializeField, Range(0f, 1f)] private float lowAmmoThreshold = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool logUpdates = false;

        private GameManager _gameManager;
        private Canvas _canvas;
        private int _lastAmmo = -1;
        private int _lastCapacity = -1;
        private bool _lastReloadState = false;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                Debug.LogWarning("[WeaponAmmoDisplay] No Canvas component found. Display visibility control will be limited.", this);
            }

            if (ammoText == null)
            {
                Debug.LogWarning("[WeaponAmmoDisplay] Missing ammo text reference.", this);
            }

            if (weapon == null)
            {
                Debug.LogWarning("[WeaponAmmoDisplay] No weapon assigned. Auto-finding ProjectileWeapon...", this);
                weapon = FindFirstObjectByType<Gameplay.ProjectileWeapon>();
            }

            if (followTarget == null)
            {
                Debug.LogWarning("[WeaponAmmoDisplay] No follow target assigned. Display will not move.", this);
            }
            
            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            _gameManager = GameManager.Instance;
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged += HandleStateChanged;
                UpdateVisibility(_gameManager.State);
            }
        }

        private void OnDisable()
        {
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(GameState previousState, GameState newState)
        {
            UpdateVisibility(newState);
        }

        private void UpdateVisibility(GameState state)
        {
            var shouldBeVisible = state == GameState.Playing;
            
            if (_canvas != null)
            {
                _canvas.enabled = shouldBeVisible;
            }
            else
            {
                gameObject.SetActive(shouldBeVisible);
            }

#if UNITY_EDITOR
            if (logUpdates)
            {
                Debug.Log($"[WeaponAmmoDisplay] Visibility updated: {shouldBeVisible} (State={state})", this);
            }
#endif
        }

        private void Update()
        {
            UpdatePosition();
            UpdateAmmoDisplay();
        }

        private void UpdatePosition()
        {
            if (followTarget == null) return;

            // Calculate target position in world space
            var targetPosition = followTarget.TransformPoint(localOffset);
            
            // Calculate target rotation
            Quaternion targetRotation;
            if (faceAwayFromTarget)
            {
                // Face away from the target (good for displays on the back of the hand)
                targetRotation = Quaternion.LookRotation(followTarget.forward, followTarget.up);
            }
            else
            {
                // Face toward the target
                targetRotation = Quaternion.LookRotation(-followTarget.forward, followTarget.up);
            }

            // Apply smoothing
            if (smoothSpeed > 0f)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothSpeed * Time.deltaTime);
            }
            else
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
            }
        }

        private void UpdateAmmoDisplay()
        {
            if (weapon == null || ammoText == null) return;

            var currentAmmo = weapon.CurrentAmmo;
            var maxAmmo = weapon.MagazineCapacity;
            var isReloading = weapon.IsReloading;

            // Check if values changed
            var changed = currentAmmo != _lastAmmo || maxAmmo != _lastCapacity || isReloading != _lastReloadState;
            if (!changed) return;

            _lastAmmo = currentAmmo;
            _lastCapacity = maxAmmo;
            _lastReloadState = isReloading;

            // Update reload indicator - ensure the text is set before activating
            if (reloadText != null)
            {
                if (isReloading)
                {
                    reloadText.text = reloadingText;
                    reloadText.gameObject.SetActive(true);
                }
                else
                {
                    reloadText.gameObject.SetActive(false);
                }
            }

            // Update ammo text
            ammoText.text = string.Format(ammoFormat, currentAmmo, maxAmmo);

            // Update color based on ammo level
            if (currentAmmo <= 0)
            {
                ammoText.color = emptyColor;
            }
            else if (currentAmmo <= (maxAmmo * lowAmmoThreshold))
            {
                ammoText.color = lowAmmoColor;
            }
            else
            {
                ammoText.color = normalColor;
            }

#if UNITY_EDITOR
            if (logUpdates)
            {
                Debug.Log($"[WeaponAmmoDisplay] Updated: {currentAmmo}/{maxAmmo}, Reloading={isReloading}", this);
            }
#endif
        }

        /// <summary>
        /// Manually set the weapon reference.
        /// </summary>
        public void SetWeapon(Gameplay.ProjectileWeapon newWeapon)
        {
            weapon = newWeapon;
            _lastAmmo = -1; // Force refresh
            _lastCapacity = -1;
            _lastReloadState = false;
        }

        /// <summary>
        /// Manually set the follow target transform.
        /// </summary>
        public void SetFollowTarget(Transform newTarget)
        {
            followTarget = newTarget;
        }
    }
}
