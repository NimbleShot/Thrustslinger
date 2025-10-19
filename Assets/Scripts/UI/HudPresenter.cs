using System;
using TMPro;
using UnityEngine;
using Thrustslinger.Core;

namespace Thrustslinger.UI
{
    /// <summary>
    /// Binds runtime data (health, score, elapsed time) to HUD text elements.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudPresenter : MonoBehaviour
    {
        [Header("UI Text References")]
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text elapsedText;
        [SerializeField] private TMP_Text comboText;

        [Header("Data Sources")]
        [Tooltip("Explicit IPlayerHealth provider. If unassigned the first PlayerHealth in the scene is used.")]
    private MonoBehaviour healthProviderBehaviour;
    [Tooltip("Score provider that implements IRuntimeScoreProvider. If null we auto-find RuntimeScoreService.")]
    private MonoBehaviour scoreProviderBehaviour;
        [Tooltip("Combo provider that implements IRuntimeComboProvider. If null we auto-find ComboTracker.")]
        [SerializeField] private MonoBehaviour comboProviderBehaviour;

        [Header("Formatting")]
        [SerializeField] private string healthFormat = "Health: {0}/{1}";
        [SerializeField] private string scoreFormat = "Score: {0}";
        [SerializeField] private string timeFormat = "Time: {0}";
        [SerializeField] private string comboFormat = "Combo: {0}";
        [SerializeField] private int comboThreshold = 2;

        private IPlayerHealth _health;
        private IRuntimeScoreProvider _scoreProvider;
        private IRuntimeComboProvider _comboProvider;
        private GameManager _gameManager;

        private float _currentHealth;
        private float _maxHealth;
        private float _currentScore;
        private int _currentCombo;

        private bool _subscriptionsActive;

        private void Awake()
        {
            _gameManager = GameManager.Instance;
            ResolveHealthProvider();
            ResolveScoreProvider();
            ResolveComboProvider();
            RefreshAll();
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            UpdateElapsedTime();
        }

        private void Subscribe()
        {
            if (_subscriptionsActive)
            {
                // Refresh existing bindings to avoid duplicate entries.
                Unsubscribe();
            }

            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
                _health.OnHealthChanged += HandleHealthChanged;
            }

            if (_scoreProvider != null)
            {
                _scoreProvider.ScoreChanged -= HandleScoreChanged;
                _scoreProvider.ScoreChanged += HandleScoreChanged;
            }

            if (_comboProvider != null)
            {
                _comboProvider.ComboChanged -= HandleComboChanged;
                _comboProvider.ComboChanged += HandleComboChanged;
            }

            if (_gameManager != null)
            {
                _gameManager.OnRunStarted -= HandleRunStarted;
                _gameManager.OnRunStarted += HandleRunStarted;
                _gameManager.OnStateChanged -= HandleStateChanged;
                _gameManager.OnStateChanged += HandleStateChanged;
            }

            _subscriptionsActive = true;
        }

        private void Unsubscribe()
        {
            if (!_subscriptionsActive)
            {
                return;
            }

            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
            }

            if (_scoreProvider != null)
            {
                _scoreProvider.ScoreChanged -= HandleScoreChanged;
            }

            if (_comboProvider != null)
            {
                _comboProvider.ComboChanged -= HandleComboChanged;
            }

            if (_gameManager != null)
            {
                _gameManager.OnRunStarted -= HandleRunStarted;
                _gameManager.OnStateChanged -= HandleStateChanged;
            }

            _subscriptionsActive = false;
        }

        private void HandleRunStarted(RunContext context)
        {
            ResolveHealthProvider();
            ResolveScoreProvider();
            ResolveComboProvider();
            Subscribe();
            RefreshAll();
        }

        private void HandleStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.MainMenu || current == GameState.GameOver)
            {
                _currentScore = _scoreProvider?.CurrentScore ?? 0f;
                UpdateScoreText();
                
                _currentCombo = _comboProvider?.CurrentCombo ?? 0;
                UpdateComboText();
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            _currentHealth = Mathf.Max(0f, current);
            _maxHealth = Mathf.Max(1f, max);
            UpdateHealthText();
        }

        private void HandleScoreChanged(float newScore)
        {
            _currentScore = Mathf.Max(0f, newScore);
            UpdateScoreText();
        }

        private void HandleComboChanged(int newCombo)
        {
            _currentCombo = Mathf.Max(0, newCombo);
            UpdateComboText();
        }

        private void RefreshAll()
        {
            if (_health != null)
            {
                _currentHealth = _health.CurrentHealth;
                _maxHealth = _health.MaxHealth;
            }
            else
            {
                _currentHealth = 0f;
                _maxHealth = 1f;
            }

            _currentScore = _scoreProvider?.CurrentScore ?? 0f;

            _currentCombo = _comboProvider?.CurrentCombo ?? 0;

            UpdateHealthText();
            UpdateScoreText();
            UpdateComboText();
            UpdateElapsedTime();
        }

        private void UpdateHealthText()
        {
            if (healthText == null)
            {
                return;
            }

            var healthValue = Mathf.RoundToInt(_currentHealth);
            var maxValue = Mathf.RoundToInt(_maxHealth);
            healthText.text = string.Format(healthFormat, healthValue, maxValue);
        }

        private void UpdateScoreText()
        {
            if (scoreText == null)
            {
                return;
            }

            var formattedScore = Mathf.RoundToInt(_currentScore).ToString("N0");
            scoreText.text = string.Format(scoreFormat, formattedScore);
        }

        private void UpdateComboText()
        {
            if (comboText == null)
            {
                return;
            }

            // Only show combo text if it meets the threshold
            if (_currentCombo >= comboThreshold)
            {
                comboText.text = string.Format(comboFormat, _currentCombo);
            }
            else
            {
                // Clear the text instead of disabling the GameObject
                comboText.text = string.Empty;
            }
        }

        private void UpdateElapsedTime()
        {
            if (elapsedText == null)
            {
                return;
            }

            var seconds = 0f;
            if (_gameManager != null)
            {
                seconds = _gameManager.RunTimeSeconds;
            }

            var formatted = FormatTime(seconds);
            elapsedText.text = string.Format(timeFormat, formatted);
        }

        private void ResolveHealthProvider()
        {
            if (healthProviderBehaviour != null)
            {
                if (healthProviderBehaviour is IPlayerHealth hp)
                {
                    _health = hp;
                }
                else if (_health == null)
                {
                    Debug.LogWarning($"[HudPresenter] Assigned health provider '{healthProviderBehaviour.name}' does not implement IPlayerHealth.", healthProviderBehaviour);
                }
            }

            if (_health == null)
            {
                var fallback = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
                if (fallback != null)
                {
                    _health = fallback;
                }
            }
        }

        private void ResolveScoreProvider()
        {
            if (scoreProviderBehaviour != null)
            {
                if (scoreProviderBehaviour is IRuntimeScoreProvider provider)
                {
                    _scoreProvider = provider;
                }
                else if (_scoreProvider == null)
                {
                    Debug.LogWarning($"[HudPresenter] Assigned score provider '{scoreProviderBehaviour.name}' does not implement IRuntimeScoreProvider.", scoreProviderBehaviour);
                }
            }

            if (_scoreProvider == null)
            {
                var fallback = FindFirstObjectByType<RuntimeScoreService>(FindObjectsInactive.Include);
                if (fallback != null)
                {
                    _scoreProvider = fallback;
                }
            }
        }

        private void ResolveComboProvider()
        {
            if (comboProviderBehaviour != null)
            {
                if (comboProviderBehaviour is IRuntimeComboProvider provider)
                {
                    _comboProvider = provider;
                }
                else if (_comboProvider == null)
                {
                    Debug.LogWarning($"[HudPresenter] Assigned combo provider '{comboProviderBehaviour.name}' does not implement IRuntimeComboProvider.", comboProviderBehaviour);
                }
            }

            if (_comboProvider == null)
            {
                var fallback = FindFirstObjectByType<ComboTracker>(FindObjectsInactive.Include);
                if (fallback != null)
                {
                    _comboProvider = fallback;
                }
            }
        }

        private static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var span = TimeSpan.FromSeconds(seconds);
            if (span.TotalHours >= 1d)
            {
                return span.ToString("hh\\:mm\\:ss");
            }

            return span.ToString("mm\\:ss");
        }
    }
}
