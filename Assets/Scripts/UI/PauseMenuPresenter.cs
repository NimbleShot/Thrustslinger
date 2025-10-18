using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Thrustslinger.Core;
using Thrustslinger.XR;

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
        [SerializeField] private Button restartButton;
        [SerializeField] private Button recenterViewButton;
        [SerializeField] private Button quitToMainMenuButton;

        [Header("Scene Loading")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Recenter View")]
        [Tooltip("Reference to the player's XR Origin/rig root transform that will be rotated. If null, will attempt to find Camera.main.")]
        [SerializeField] private Transform playerRigTransform;
        [Tooltip("Reference to PlayerPlaneDefinition. If null, will attempt to find it in the scene.")]
        [SerializeField] private MonoBehaviour planeProviderBehaviour;

        private GameManager _gameManager;
        private PlayerFacingMenuPositioner _menuPositioner;
        private IPlaneProvider _planeProvider;
        private bool _pendingRestart;

        private void Awake()
        {
            _gameManager = GameManager.Instance;

            if (_gameManager == null)
            {
                Debug.LogError("[PauseMenuPresenter] GameManager instance not found.", this);
                enabled = false;
                return;
            }

            // Resolve plane provider for recenter view
            ResolvePlaneProvider();

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(HandleResumeClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(HandleRestartClicked);
            }

            if (recenterViewButton != null)
            {
                recenterViewButton.onClick.AddListener(HandleRecenterViewClicked);
            }

            if (quitToMainMenuButton != null)
            {
                quitToMainMenuButton.onClick.AddListener(HandleQuitToMainMenuClicked);
            }

            // Get menu positioner component if present
            _menuPositioner = GetComponent<PlayerFacingMenuPositioner>();
        }

        private void OnEnable()
        {
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            // Don't unsubscribe here if we have a pending restart
            // The state change event needs to fire even when the UI is disabled
            if (_gameManager != null && !_pendingRestart)
            {
                _gameManager.OnStateChanged -= HandleStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(HandleResumeClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(HandleRestartClicked);
            }

            if (recenterViewButton != null)
            {
                recenterViewButton.onClick.RemoveListener(HandleRecenterViewClicked);
            }

            if (quitToMainMenuButton != null)
            {
                quitToMainMenuButton.onClick.RemoveListener(HandleQuitToMainMenuClicked);
            }
        }

        private void HandleStateChanged(GameState previous, GameState current)
        {
            // Update menu position when entering pause state
            if (current == GameState.Paused && _menuPositioner != null)
            {
                _menuPositioner.UpdatePosition();
            }
            
            // Handle pending restart when we reach GameOver state
            if (_pendingRestart && current == GameState.GameOver)
            {
                _pendingRestart = false;
                
                if (_gameManager != null)
                {
                    Debug.Log("[PauseMenuPresenter] Calling Restart() from HandleStateChanged", this);
                    // Restart immediately - the OnStateChanged event fires synchronously,
                    // so we can call Restart right away. Restart() will hide the UI and start BeginRunRoutine.
                    _gameManager.Restart();
                }
                
                // Now that restart is handled, we can safely unsubscribe if we're disabled
                if (_gameManager != null && !enabled)
                {
                    _gameManager.OnStateChanged -= HandleStateChanged;
                }
            }
        }

        private void HandleResumeClicked()
        {
            if (_gameManager != null && _gameManager.IsPaused)
            {
                _gameManager.Resume();
            }
        }

        private void HandleRestartClicked()
        {
            if (_gameManager == null) return;

            // Set flag to indicate we want to restart (used by state change handler)
            _pendingRestart = true;
            
            // If currently paused, we need to transition to a state where restart is allowed
            // Restart only works from GameOver state, so we need to call EndRun
            // But we'll immediately call Restart in the state change handler
            // to minimize the time the game over UI is visible
            _gameManager.EndRun();
        }

        private void HandleRecenterViewClicked()
        {
            if (_planeProvider == null)
            {
                Debug.LogWarning("[PauseMenuPresenter] Cannot recenter view - no plane provider found.", this);
                return;
            }

            // Find player rig if not set
            Transform rigTransform = playerRigTransform;
            
            if (rigTransform == null)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    // Find XR Origin by walking up the hierarchy
                    Transform current = mainCamera.transform;
                    while (current.parent != null)
                    {
                        current = current.parent;
                        
                        // Look for XR Origin or similar root component
                        if (current.name.Contains("XR") || current.name.Contains("Origin") || current.name.Contains("Rig"))
                        {
                            rigTransform = current;
                            break;
                        }
                    }

                    // If no XR Origin found, use the camera's parent
                    if (rigTransform == null && mainCamera.transform.parent != null)
                    {
                        rigTransform = mainCamera.transform.parent;
                    }
                }
            }

            if (rigTransform == null)
            {
                Debug.LogWarning("[PauseMenuPresenter] Cannot recenter view - no player rig transform found.", this);
                return;
            }

            // Get the plane normal
            Vector3 planeNormal = _planeProvider.Normal;
            var mainCam = Camera.main;
            
            if (mainCam == null)
            {
                Debug.LogWarning("[PauseMenuPresenter] Cannot recenter view - no camera found.", this);
                return;
            }

            // Calculate the desired rotation to face the plane normal
            // We only want to rotate around the world Y axis (vertical)
            Vector3 planeForward = planeNormal;
            planeForward.y = 0f; // Project to horizontal plane
            
            if (planeForward.sqrMagnitude < 0.001f)
            {
                Debug.LogWarning("[PauseMenuPresenter] Plane normal is vertical, cannot determine horizontal facing direction.", this);
                return;
            }
            
            planeForward.Normalize();
            
            // Get current camera forward projected to horizontal
            Vector3 cameraForward = mainCam.transform.forward;
            cameraForward.y = 0f;
            
            if (cameraForward.sqrMagnitude < 0.001f)
            {
                Debug.LogWarning("[PauseMenuPresenter] Camera forward is vertical, cannot recenter horizontally.", this);
                return;
            }
            
            cameraForward.Normalize();
            
            // Calculate the rotation delta needed
            float angle = Vector3.SignedAngle(cameraForward, planeForward, Vector3.up);
            
            // Apply rotation to the rig transform
            // We rotate the entire rig, which includes the camera as a child
            Quaternion deltaRotation = Quaternion.AngleAxis(angle, Vector3.up);
            
            // Store camera world position before rotation
            Vector3 cameraWorldPos = mainCam.transform.position;
            
            // Rotate the rig
            rigTransform.rotation = rigTransform.rotation * deltaRotation;
            
            // Calculate how much the camera moved due to the rotation
            Vector3 cameraNewPos = mainCam.transform.position;
            Vector3 cameraDelta = cameraWorldPos - cameraNewPos;
            
            // Move the rig to compensate, keeping the camera in the same world position
            rigTransform.position += cameraDelta;
            
            Debug.Log($"[PauseMenuPresenter] Recentered view to face plane normal: {planeNormal}, rotated by {angle:F1}° around Y axis", this);
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

        private void ResolvePlaneProvider()
        {
            if (planeProviderBehaviour != null)
            {
                _planeProvider = planeProviderBehaviour as IPlaneProvider;
                if (_planeProvider == null)
                {
                    Debug.LogWarning($"[PauseMenuPresenter] Assigned plane provider '{planeProviderBehaviour.name}' does not implement IPlaneProvider.", planeProviderBehaviour);
                }
            }

            // Auto-find if not assigned
            if (_planeProvider == null)
            {
                var planeDefinition = FindFirstObjectByType<PlayerPlaneDefinition>(FindObjectsInactive.Include);
                if (planeDefinition != null)
                {
                    _planeProvider = planeDefinition;
                    Debug.Log("[PauseMenuPresenter] Auto-found PlayerPlaneDefinition for recenter view.", this);
                }
                else
                {
                    Debug.LogWarning("[PauseMenuPresenter] No IPlaneProvider found in scene. Recenter view button will not work.", this);
                }
            }
        }
    }
}
