using UnityEngine;
using UnityEngine.InputSystem;
using Thrustslinger.Core;

namespace Thrustslinger.XR
{
    /// <summary>
    /// Monitors XR input to toggle pause/resume during gameplay.
    /// Listens for a configured button press (e.g., menu button, start button) to pause/unpause the game.
    /// </summary>
    [AddComponentMenu("Thrustslinger/XR/Pause Input Controller")]
    [DisallowMultipleComponent]
    public sealed class PauseInputController : MonoBehaviour
    {
        [Header("Input Configuration")]
        [Tooltip("XR button action that toggles pause (e.g., menu button, start button).")]
        [SerializeField] private InputActionProperty pauseAction;

        [Header("Settings")]
        [Tooltip("If true, pause input only works while in Playing state. Resume works while Paused.")]
        [SerializeField] private bool requirePlayingState = true;

        [Header("Debug")]
        [SerializeField] private bool logPauseToggle = false;

        private GameManager _gameManager;
        private InputAction _pauseFallback;
        private bool _previouslyPressed;

        private void Awake()
        {
            _gameManager = GameManager.Instance;

            if (_gameManager == null)
            {
                Debug.LogError("[PauseInputController] GameManager instance not found.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            pauseAction.action?.Enable();

            // Fallback action if not wired via inspector InputActionProperty
            // Using menu button as default (common across VR controllers)
            if (pauseAction.action == null)
            {
                _pauseFallback ??= new InputAction(
                    name: "PauseFallback",
                    type: InputActionType.Button,
                    binding: "<XRController>{LeftHand}/{Menu}"
                );
                _pauseFallback.Enable();
            }
        }

        private void OnDisable()
        {
            pauseAction.action?.Disable();
            _pauseFallback?.Disable();
        }

        private void Update()
        {
            if (_gameManager == null) return;

            var action = pauseAction.action ?? _pauseFallback;
            if (action == null) return;

            bool isPressed = action.IsPressed();

            // Detect button press (transition from not pressed to pressed)
            if (isPressed && !_previouslyPressed)
            {
                HandlePauseButtonPressed();
            }

            _previouslyPressed = isPressed;
        }

        private void HandlePauseButtonPressed()
        {
            var state = _gameManager.State;

            // Toggle pause/resume based on current state
            if (state == GameState.Playing)
            {
                if (logPauseToggle)
                {
                    Debug.Log("[PauseInputController] Pausing game.");
                }
                _gameManager.Pause();
            }
            else if (state == GameState.Paused)
            {
                if (logPauseToggle)
                {
                    Debug.Log("[PauseInputController] Resuming game.");
                }
                _gameManager.Resume();
            }
            else if (!requirePlayingState)
            {
                // Allow pause toggle in other states if configured
                if (logPauseToggle)
                {
                    Debug.Log($"[PauseInputController] Pause button pressed in state {state} (ignored).");
                }
            }
        }

        #region Debug Helpers
#if UNITY_EDITOR
        [ContextMenu("Test Pause Toggle")]
        private void TestPauseToggle()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PauseInputController] Test Pause Toggle only works in Play mode.");
                return;
            }

            HandlePauseButtonPressed();
        }
#endif
        #endregion
    }
}
