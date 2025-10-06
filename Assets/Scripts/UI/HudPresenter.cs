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

        [Header("Data Sources")]
        [Tooltip("Explicit IPlayerHealth provider. If unassigned the first PlayerHealth in the scene is used.")]
        [SerializeField] private MonoBehaviour healthProviderBehaviour;
        [Tooltip("Score provider that implements IRuntimeScoreProvider. If null we auto-find RuntimeScoreService.")]
        [SerializeField] private MonoBehaviour scoreProviderBehaviour;

        [Header("Formatting")]
        [SerializeField] private string healthFormat = "Health: {0}/{1}";
        [SerializeField] private string scoreFormat = "Score: {0}";
        [SerializeField] private string timeFormat = "Time: {0}";

        private IPlayerHealth _health;
        private IRuntimeScoreProvider _scoreProvider;
        private GameManager _gameManager;

        private float _currentHealth;
        private float _maxHealth;
        private float _currentScore;

        private bool _subscriptionsActive;

        private void Awake()
        {
            _gameManager = GameManager.Instance;
            ResolveHealthProvider();
            ResolveScoreProvider();
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
            Subscribe();
            RefreshAll();
        }

        private void HandleStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.MainMenu || current == GameState.Results)
            {
                _currentScore = _scoreProvider?.CurrentScore ?? 0f;
                UpdateScoreText();
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

            UpdateHealthText();
            UpdateScoreText();
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
