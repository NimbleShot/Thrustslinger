using System;
using UnityEngine;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Player health implementation that raises events on damage and depletion for the GameManager to consume.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IPlayerHealth
    {
        [Header("Health Settings")]
        [Tooltip("Maximum health for the player. Run reset restores to this value.")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        [Tooltip("Automatically restore current health to max during Awake (useful when not driven by GameManager).")]
        [SerializeField] private bool initializeAtMaxOnAwake = true;

        [Header("Debug")]
        [SerializeField] private bool logDamage;

        public event Action<float, float> OnHealthChanged;
        public event Action OnHealthDepleted;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;

        private bool _depletedRaised;

        private void Awake()
        {
            if (initializeAtMaxOnAwake)
            {
                SetHealthInternal(maxHealth, forceNotify: true);
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            if (!Application.isPlaying)
            {
                CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);
            }
        }

        public void ResetHealth()
        {
            _depletedRaised = false;
            SetHealthInternal(maxHealth, forceNotify: true);
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            if (_depletedRaised && CurrentHealth <= 0f)
            {
                return;
            }

            var newHealth = Mathf.Clamp(CurrentHealth - amount, 0f, maxHealth);
            if (Mathf.Approximately(newHealth, CurrentHealth))
            {
                return;
            }

            if (logDamage)
            {
                Debug.Log($"[PlayerHealth] Damage {amount:F1} -> {newHealth:F1}/{maxHealth:F1}", this);
            }

            SetHealthInternal(newHealth, forceNotify: true);

            if (CurrentHealth <= 0f && !_depletedRaised)
            {
                _depletedRaised = true;
                OnHealthDepleted?.Invoke();
            }
        }

        private void SetHealthInternal(float value, bool forceNotify)
        {
            var clamped = Mathf.Clamp(value, 0f, maxHealth);
            var changed = forceNotify || !Mathf.Approximately(clamped, CurrentHealth);
            CurrentHealth = clamped;

            if (changed)
            {
                OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            }
        }
    }
}