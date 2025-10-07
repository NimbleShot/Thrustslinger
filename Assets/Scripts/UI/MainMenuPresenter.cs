using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Thrustslinger.Core;

namespace Thrustslinger.UI
{
    /// <summary>
    /// Minimal VR main menu presenter that only exposes Start and Quit buttons.
    /// </summary>
    [AddComponentMenu("Thrustslinger/UI/Main Menu Presenter")]
    [DisallowMultipleComponent]
    public sealed class MainMenuPresenter : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;

        [Header("Scene Loading")]
        [SerializeField] private string arenaSceneName = "Game";
        [SerializeField, Min(0f)] private float sceneLoadDelaySeconds = 0.05f;

        private Coroutine _loadRoutine;

        private void Awake()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(HandleStartClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(HandleQuitClicked);
            }

            var context = MenuRunContextStore.GetMenuContext();
            MenuRunContextStore.SetMenuContext(context);
        }

        private void OnDisable()
        {
            StopLoadRoutine();
        }

        private void OnDestroy()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStartClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(HandleQuitClicked);
            }
        }

        private void HandleStartClicked()
        {
            if (string.IsNullOrEmpty(arenaSceneName))
            {
                Debug.LogWarning("[MainMenuPresenter] Arena scene name is not configured.");
                return;
            }

            if (_loadRoutine != null)
            {
                return;
            }

            var context = MenuRunContextStore.GetMenuContext();
            MenuRunContextStore.SetMenuContext(context);
            MenuRunContextStore.SetPendingStart(context);

            _loadRoutine = StartCoroutine(LoadArenaSceneAsync());
        }

        private IEnumerator LoadArenaSceneAsync()
        {
            if (sceneLoadDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(sceneLoadDelaySeconds);
            }

            var operation = SceneManager.LoadSceneAsync(arenaSceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[MainMenuPresenter] Unable to load scene '{arenaSceneName}'.");
                _loadRoutine = null;
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }

            _loadRoutine = null;
        }

        private void HandleQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void StopLoadRoutine()
        {
            if (_loadRoutine == null)
            {
                return;
            }

            StopCoroutine(_loadRoutine);
            _loadRoutine = null;
        }
    }
}
