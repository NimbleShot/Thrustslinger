using System;
using UnityEngine;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Tracks combo chains during gameplay. Increments on successful kills and resets on target breaches.
    /// Implements <see cref="IComboTracker"/> so it can plug directly into the <see cref="GameManager"/>.
    /// </summary>
    [AddComponentMenu("Thrustslinger/Core/Combo Tracker")]
    [DisallowMultipleComponent]
    public sealed class ComboTracker : MonoBehaviour, IComboTracker, IRuntimeComboProvider
    {
        [Header("Combo Settings")]
        [Tooltip("The highest combo achieved in the current run.")]
        [SerializeField, Min(0)] private int currentCombo;

        [Header("High Score Persistence")]
        [SerializeField] private bool trackHighestCombo = true;
        [SerializeField] private string highestComboPrefsKey = "Thrustslinger_HighestCombo";

        [Header("Debug")]
        [SerializeField] private bool logComboEvents;

        public event Action<int> ComboChanged;

        public int CurrentCombo
        {
            get => currentCombo;
            private set
            {
                if (currentCombo != value)
                {
                    currentCombo = value;
                    ComboChanged?.Invoke(currentCombo);

                    if (logComboEvents)
                    {
                        Debug.Log($"[ComboTracker] Combo changed to {currentCombo}");
                    }
                }
            }
        }

        public int HighestCombo { get; private set; }

        private void Awake()
        {
            LoadHighestCombo();
        }

        public void ResetCombo()
        {
            if (logComboEvents && CurrentCombo > 0)
            {
                Debug.Log($"[ComboTracker] Combo reset from {CurrentCombo}");
            }

            CurrentCombo = 0;
        }

        public void BreakCombo()
        {
            if (logComboEvents && CurrentCombo > 0)
            {
                Debug.Log($"[ComboTracker] Combo broken at {CurrentCombo}");
            }

            // Check if this was a new high score before breaking
            if (trackHighestCombo && CurrentCombo > HighestCombo)
            {
                HighestCombo = CurrentCombo;
                SaveHighestCombo();
            }

            CurrentCombo = 0;
        }

        public void IncrementCombo()
        {
            CurrentCombo++;

            if (logComboEvents)
            {
                Debug.Log($"[ComboTracker] Combo incremented to {CurrentCombo}");
            }
        }

        private void LoadHighestCombo()
        {
            if (!trackHighestCombo)
            {
                HighestCombo = 0;
                return;
            }

            HighestCombo = PlayerPrefs.GetInt(highestComboPrefsKey, 0);
        }

        private void SaveHighestCombo()
        {
            if (!trackHighestCombo)
            {
                return;
            }

            PlayerPrefs.SetInt(highestComboPrefsKey, HighestCombo);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        [ContextMenu("Clear Highest Combo")]
        private void ClearHighestCombo()
        {
            if (!trackHighestCombo)
            {
                Debug.LogWarning("[ComboTracker] Highest combo tracking is disabled.", this);
                return;
            }

            if (PlayerPrefs.HasKey(highestComboPrefsKey))
            {
                PlayerPrefs.DeleteKey(highestComboPrefsKey);
                PlayerPrefs.Save();
                HighestCombo = 0;
                Debug.Log($"[ComboTracker] Highest combo cleared from PlayerPrefs key: {highestComboPrefsKey}", this);
            }
            else
            {
                Debug.Log("[ComboTracker] No highest combo found to clear.", this);
            }
        }
#endif
    }
}
