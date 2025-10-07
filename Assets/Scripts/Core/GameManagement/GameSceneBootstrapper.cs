using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Scene-level helper that wires the active gameplay scene to the persistent <see cref="GameManager"/>.
    /// Responsible for applying scene bindings, auto-starting runs requested from the menu, and caching results.
    /// </summary>
    [AddComponentMenu("Thrustslinger/Core/Game Scene Bootstrapper")]
    [DisallowMultipleComponent]
    public sealed class GameSceneBootstrapper : MonoBehaviour
    {
        [Header("Scene Bindings")]
        [SerializeField] private GameManager.SceneBindings sceneBindings;

        [Header("Run Flow")]
        [SerializeField] private bool autoStartPendingRun = true;
        [SerializeField] private float waitAfterBootSeconds = 0.05f;

        [Header("Menu Navigation")]
        [SerializeField] private string menuSceneName = "MainMenu";

        private GameManager _gameManager;
        private bool _bindingsApplied;

        private void OnEnable()
        {
            StartCoroutine(InitializeAsync());
        }

        private IEnumerator InitializeAsync()
        {
            // Wait a frame to ensure the GameManager in this scene has finished Awake/Start.
            yield return null;

            _gameManager = GameManager.Instance;
            if (_gameManager == null)
            {
                Debug.LogError("[GameSceneBootstrapper] GameManager instance not found in the scene. Ensure it exists before this bootstrapper runs.", this);
                yield break;
            }

            if (!_bindingsApplied)
            {
                _gameManager.ApplySceneBindings(sceneBindings);
                _bindingsApplied = true;
            }

            _gameManager.OnRunCompleted += HandleRunCompleted;

            var menuContext = MenuRunContextStore.GetMenuContext();
            _gameManager.UpdateMenuContext(menuContext);

            if (!autoStartPendingRun)
            {
                yield break;
            }

            // Wait until the GameManager boot sequence has completed before attempting a start.
            yield return new WaitUntil(() => _gameManager.BootComplete);

            if (waitAfterBootSeconds > 0f)
            {
                yield return new WaitForSeconds(waitAfterBootSeconds);
            }

            if (!MenuRunContextStore.TryConsumePendingStart(out var pendingContext))
            {
                yield break;
            }

            // GameManager only allows StartRun from MainMenu/Results. Wait until it's in a valid state.
            if (_gameManager.State != GameState.MainMenu && _gameManager.State != GameState.Results)
            {
                yield return new WaitUntil(() =>
                    _gameManager.State == GameState.MainMenu || _gameManager.State == GameState.Results);
            }

            _gameManager.StartRun(pendingContext);
        }

        private void OnDisable()
        {
            if (_gameManager != null)
            {
                _gameManager.OnRunCompleted -= HandleRunCompleted;
            }

            if (_bindingsApplied && _gameManager != null)
            {
                _gameManager.ClearSceneBindings();
                _bindingsApplied = false;
            }
        }

        private void HandleRunCompleted(RunSummary summary)
        {
            MenuRunContextStore.CacheSummary(summary);
            MenuRunContextStore.SetMenuContext(_gameManager.MenuContext);
        }

        /// <summary>
        /// Loads the configured menu scene. Intended for use from UI button events.
        /// </summary>
        public void ReturnToMenu()
        {
            if (string.IsNullOrEmpty(menuSceneName))
            {
                Debug.LogWarning("[GameSceneBootstrapper] Menu scene name is not configured.", this);
                return;
            }

            if (_gameManager != null)
            {
                MenuRunContextStore.SetMenuContext(_gameManager.MenuContext);
                MenuRunContextStore.CacheSummary(_gameManager.CurrentSummary);
                _gameManager.QuitToMenu();
            }

            SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
        }
    }
}
