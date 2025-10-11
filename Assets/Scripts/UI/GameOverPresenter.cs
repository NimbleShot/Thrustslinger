using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Thrustslinger.Core;

namespace Thrustslinger.UI
{
    /// <summary>
    /// Game Over screen presenter that displays final run statistics and provides restart/menu navigation.
    /// USAGE: Attach this component to a parent GameObject that stays enabled. Set 'uiPanel' to reference
    /// the child Canvas/Panel that should be shown/hidden. The presenter will automatically show the panel
    /// only when GameState == GameOver.
    /// </summary>
    [AddComponentMenu("Thrustslinger/UI/Game Over Presenter")]
    [DisallowMultipleComponent]
    public sealed class GameOverPresenter : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The UI panel/canvas to show/hide. If null, uses this GameObject.")]
        [SerializeField] private GameObject uiPanel;

        [Header("UI Text References")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text elapsedTimeText;
        [SerializeField] private TMP_Text highScoreText;

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button returnToMenuButton;

        [Header("Formatting")]
        [SerializeField] private string scoreFormat = "Score: {0}";
        [SerializeField] private string timeFormat = "Time: {0}";
        [SerializeField] private string highScoreFormat = "High Score: {0}";

        [Header("Scene Loading")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Data Sources")]
        [Tooltip("Score service that implements IRuntimeScoreProvider. If null we auto-find RuntimeScoreService.")]
        [SerializeField] private MonoBehaviour scoreServiceBehaviour;

        private GameManager _gameManager;
        private IRuntimeScoreProvider _scoreProvider;

        private void Awake()
        {
            _gameManager = GameManager.Instance;

            if (_gameManager == null)
            {
                Debug.LogError("[GameOverPresenter] GameManager instance not found.", this);
                enabled = false;
                return;
            }

            // If no UI panel specified, control this GameObject's active state
            if (uiPanel == null)
            {
                uiPanel = gameObject;
            }

            ResolveScoreProvider();

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(HandleRestartClicked);
            }

            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.AddListener(HandleReturnToMenuClicked);
            }

            // Initial state - hide the panel
            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged += HandleStateChanged;
            }

            // Update visibility based on current state
            UpdateVisibility();
        }

        private void OnDisable()
        {
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged -= HandleStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(HandleRestartClicked);
            }

            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.RemoveListener(HandleReturnToMenuClicked);
            }
        }

        private void HandleStateChanged(GameState previous, GameState current)
        {
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (_gameManager == null)
            {
                return;
            }

            var isGameOver = _gameManager.State == GameState.GameOver;
            SetPanelVisible(isGameOver);

            // Refresh display when entering Game Over state
            if (isGameOver)
            {
                RefreshDisplay();
            }
        }

        private void SetPanelVisible(bool visible)
        {
            if (uiPanel != null && uiPanel.activeSelf != visible)
            {
                uiPanel.SetActive(visible);
            }
        }

        private void RefreshDisplay()
        {
            if (_gameManager == null)
            {
                return;
            }

            var summary = _gameManager.CurrentSummary;
            if (summary == null)
            {
                Debug.LogWarning("[GameOverPresenter] No run summary available to display.", this);
                return;
            }

            UpdateScoreText(summary.finalScore);
            UpdateElapsedTimeText(summary.runTimeSeconds);
            UpdateHighScoreText();
        }

        private void UpdateScoreText(float score)
        {
            if (scoreText == null)
            {
                Debug.LogWarning("[GameOverPresenter] scoreText is not assigned in the inspector.", this);
                return;
            }

            var formattedScore = Mathf.RoundToInt(score).ToString("N0");
            scoreText.text = string.Format(scoreFormat, formattedScore);
        }

        private void UpdateElapsedTimeText(float seconds)
        {
            if (elapsedTimeText == null)
            {
                Debug.LogWarning("[GameOverPresenter] elapsedTimeText is not assigned in the inspector.", this);
                return;
            }

            var formatted = FormatTime(seconds);
            elapsedTimeText.text = string.Format(timeFormat, formatted);
        }

        private void UpdateHighScoreText()
        {
            if (highScoreText == null)
            {
                Debug.LogWarning("[GameOverPresenter] highScoreText is not assigned in the inspector.", this);
                return;
            }

            if (_scoreProvider == null)
            {
                highScoreText.text = string.Format(highScoreFormat, "---");
                return;
            }

            var highScore = _scoreProvider.HighScore;
            var formattedHighScore = Mathf.RoundToInt(highScore).ToString("N0");
            highScoreText.text = string.Format(highScoreFormat, formattedHighScore);
        }

        private void HandleRestartClicked()
        {
            if (_gameManager != null)
            {
                _gameManager.Restart();
            }
        }

        private void HandleReturnToMenuClicked()
        {
            if (string.IsNullOrEmpty(mainMenuSceneName))
            {
                Debug.LogWarning("[GameOverPresenter] Main menu scene name is not configured.", this);
                return;
            }

            // Use GameManager's QuitToMenu to properly clean up the run state
            if (_gameManager != null)
            {
                MenuRunContextStore.SetMenuContext(_gameManager.MenuContext);
                MenuRunContextStore.CacheSummary(_gameManager.CurrentSummary);
                _gameManager.QuitToMenu();
            }

            // Scene loading now handled by GameManager.QuitToMenu
        }

        private void ResolveScoreProvider()
        {
            if (scoreServiceBehaviour != null)
            {
                if (scoreServiceBehaviour is IRuntimeScoreProvider provider)
                {
                    _scoreProvider = provider;
                }
                else if (_scoreProvider == null)
                {
                    Debug.LogWarning($"[GameOverPresenter] Assigned score provider '{scoreServiceBehaviour.name}' does not implement IRuntimeScoreProvider.", scoreServiceBehaviour);
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
