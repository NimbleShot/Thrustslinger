using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Thrustslinger.Core;

namespace Thrustslinger.UI
{
    /// <summary>
    /// Pause menu presenter for VR that exposes Resume and Quit to Main Menu buttons.
    /// </summary>
    [AddComponentMenu("Thrustslinger/UI/Pause Menu Presenter")]
    [DisallowMultipleComponent]
    public sealed class PauseMenuPresenter : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitToMainMenuButton;

        [Header("Scene Loading")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private GameManager _gameManager;

        private void Awake()
        {
            _gameManager = GameManager.Instance;

            if (_gameManager == null)
            {
                Debug.LogError("[PauseMenuPresenter] GameManager instance not found.", this);
                enabled = false;
                return;
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(HandleResumeClicked);
            }

            if (quitToMainMenuButton != null)
            {
                quitToMainMenuButton.onClick.AddListener(HandleQuitToMainMenuClicked);
            }
        }

        private void OnDestroy()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(HandleResumeClicked);
            }

            if (quitToMainMenuButton != null)
            {
                quitToMainMenuButton.onClick.RemoveListener(HandleQuitToMainMenuClicked);
            }
        }

        private void HandleResumeClicked()
        {
            if (_gameManager != null && _gameManager.IsPaused)
            {
                _gameManager.Resume();
            }
        }

        private void HandleQuitToMainMenuClicked()
        {
            if (string.IsNullOrEmpty(mainMenuSceneName))
            {
                Debug.LogWarning("[PauseMenuPresenter] Main menu scene name is not configured.", this);
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
    }
}
