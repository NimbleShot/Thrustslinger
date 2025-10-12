using System.Collections;
using TMPro;
using UnityEngine;
using Thrustslinger.Core;

namespace Thrustslinger.UI
{
    /// <summary>
    /// World-space UI panel that displays player health.
    /// Follows a specified transform (typically left hand controller).
    /// Features color-coded health thresholds and damage blink effect.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealthDisplay : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField, Tooltip("The health provider to display. If null, auto-finds PlayerHealth.")]
        private MonoBehaviour healthProviderBehaviour;
        
        [SerializeField, Tooltip("Transform to follow (e.g., left hand controller)")]
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
        [SerializeField] private TMP_Text healthText;

        [Header("Formatting")]
        [SerializeField] private string healthFormat = "{0} / {1}";

        [Header("Color Thresholds")]
        [SerializeField, Tooltip("Color when health is at 76-100%")]
        private Color highHealthColor = Color.green;
        
        [SerializeField, Tooltip("Color when health is at 51-75%")]
        private Color mediumHealthColor = new Color(1f, 0.5f, 0f); // Orange
        
        [SerializeField, Tooltip("Color when health is at 26-50%")]
        private Color lowHealthColor = Color.yellow;
        
        [SerializeField, Tooltip("Color when health is at 0-25%")]
        private Color criticalHealthColor = Color.red;

        [Header("Damage Blink Effect")]
        [SerializeField, Tooltip("Enable red blink effect when taking damage")]
        private bool enableDamageBlink = true;
        
        [SerializeField, Tooltip("Color to blink to when damaged")]
        private Color damageBlinkColor = Color.red;
        
        [SerializeField, Tooltip("Duration of damage blink effect in seconds")]
        [Range(0.05f, 1f)]
        private float blinkDuration = 0.2f;
        
        [SerializeField, Tooltip("Number of blinks when damaged")]
        [Range(1, 5)]
        private int blinkCount = 2;

        [Header("Debug")]
        [SerializeField] private bool logUpdates = false;

        private GameManager _gameManager;
        private IPlayerHealth _health;
        private Canvas _canvas;
        private float _lastHealth = -1f;
        private float _lastMaxHealth = -1f;
        private Coroutine _blinkRoutine;
        private Color _targetColor;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                Debug.LogWarning("[PlayerHealthDisplay] No Canvas component found. Display visibility control will be limited.", this);
            }

            if (healthText == null)
            {
                Debug.LogWarning("[PlayerHealthDisplay] Missing health text reference.", this);
            }

            ResolveHealthProvider();

            if (followTarget == null)
            {
                Debug.LogWarning("[PlayerHealthDisplay] No follow target assigned. Display will not move.", this);
            }
        }

        private void OnEnable()
        {
            _gameManager = GameManager.Instance;
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged += HandleStateChanged;
                _gameManager.OnRunStarted += HandleRunStarted;
                UpdateVisibility(_gameManager.State);
            }

            SubscribeToHealth();
        }

        private void OnDisable()
        {
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged -= HandleStateChanged;
                _gameManager.OnRunStarted -= HandleRunStarted;
            }

            UnsubscribeFromHealth();

            if (_blinkRoutine != null)
            {
                StopCoroutine(_blinkRoutine);
                _blinkRoutine = null;
            }
        }

        private void HandleRunStarted(RunContext context)
        {
            ResolveHealthProvider();
            SubscribeToHealth();
            ForceRefresh();
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
                Debug.Log($"[PlayerHealthDisplay] Visibility updated: {shouldBeVisible} (State={state})", this);
            }
#endif
        }

        private void Update()
        {
            UpdatePosition();
            UpdateHealthDisplay();
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

        private void UpdateHealthDisplay()
        {
            if (_health == null || healthText == null) return;

            var currentHealth = _health.CurrentHealth;
            var maxHealth = _health.MaxHealth;

            // Check if values changed
            var changed = !Mathf.Approximately(currentHealth, _lastHealth) || !Mathf.Approximately(maxHealth, _lastMaxHealth);
            if (!changed) return;

            // Detect damage (health decreased)
            var tookDamage = currentHealth < _lastHealth && _lastHealth > 0f;

            _lastHealth = currentHealth;
            _lastMaxHealth = maxHealth;

            // Update health text
            var healthValue = Mathf.RoundToInt(currentHealth);
            var maxValue = Mathf.RoundToInt(maxHealth);
            healthText.text = string.Format(healthFormat, healthValue, maxValue);

            // Calculate target color based on health percentage
            _targetColor = GetColorForHealthPercentage(currentHealth / maxHealth);

            // Apply color (or start blink if damaged)
            if (enableDamageBlink && tookDamage && currentHealth > 0f)
            {
                TriggerDamageBlink();
            }
            else if (_blinkRoutine == null)
            {
                // Only update color if not currently blinking
                healthText.color = _targetColor;
            }

#if UNITY_EDITOR
            if (logUpdates)
            {
                Debug.Log($"[PlayerHealthDisplay] Updated: {healthValue}/{maxValue} ({(currentHealth/maxHealth)*100f:F1}%), Damage={tookDamage}", this);
            }
#endif
        }

        private Color GetColorForHealthPercentage(float percentage)
        {
            if (percentage <= 0.25f)
            {
                return criticalHealthColor;
            }
            else if (percentage <= 0.50f)
            {
                return lowHealthColor;
            }
            else if (percentage <= 0.75f)
            {
                return mediumHealthColor;
            }
            else
            {
                return highHealthColor;
            }
        }

        private void TriggerDamageBlink()
        {
            if (_blinkRoutine != null)
            {
                StopCoroutine(_blinkRoutine);
            }
            _blinkRoutine = StartCoroutine(DamageBlinkRoutine());
        }

        private IEnumerator DamageBlinkRoutine()
        {
            if (healthText == null)
            {
                _blinkRoutine = null;
                yield break;
            }

            var blinkInterval = blinkDuration / (blinkCount * 2f); // Time for each on/off

            for (int i = 0; i < blinkCount; i++)
            {
                // Blink to damage color
                healthText.color = damageBlinkColor;
                yield return new WaitForSeconds(blinkInterval);

                // Blink back to target color
                healthText.color = _targetColor;
                yield return new WaitForSeconds(blinkInterval);
            }

            // Ensure we end on the target color
            healthText.color = _targetColor;
            _blinkRoutine = null;
        }

        private void ResolveHealthProvider()
        {
            if (healthProviderBehaviour != null)
            {
                if (healthProviderBehaviour is IPlayerHealth hp)
                {
                    _health = hp;
                }
                else
                {
                    Debug.LogWarning($"[PlayerHealthDisplay] Assigned health provider '{healthProviderBehaviour.name}' does not implement IPlayerHealth.", healthProviderBehaviour);
                }
            }

            if (_health == null)
            {
                var fallback = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
                if (fallback != null)
                {
                    _health = fallback;
#if UNITY_EDITOR
                    if (logUpdates)
                    {
                        Debug.Log($"[PlayerHealthDisplay] Auto-found PlayerHealth: {fallback.name}", this);
                    }
#endif
                }
                else
                {
                    Debug.LogWarning("[PlayerHealthDisplay] No PlayerHealth found in scene.", this);
                }
            }
        }

        private void SubscribeToHealth()
        {
            UnsubscribeFromHealth();

            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
            }
        }

        private void UnsubscribeFromHealth()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            // Force update on next frame
            _lastHealth = -1f;
        }

        private void ForceRefresh()
        {
            _lastHealth = -1f;
            _lastMaxHealth = -1f;
        }

        /// <summary>
        /// Manually set the health provider reference.
        /// </summary>
        public void SetHealthProvider(IPlayerHealth newHealthProvider)
        {
            UnsubscribeFromHealth();
            
            if (newHealthProvider is MonoBehaviour mb)
            {
                healthProviderBehaviour = mb;
            }
            
            _health = newHealthProvider;
            SubscribeToHealth();
            ForceRefresh();
        }

        /// <summary>
        /// Manually set the follow target transform.
        /// </summary>
        public void SetFollowTarget(Transform newTarget)
        {
            followTarget = newTarget;
        }

        /// <summary>
        /// Manually trigger the damage blink effect (for testing).
        /// </summary>
        public void TestDamageBlink()
        {
            TriggerDamageBlink();
        }
    }
}
