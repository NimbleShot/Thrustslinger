using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Thrustslinger.Gameplay;
using Thrustslinger.XR;
using UnityEngine.SceneManagement;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Single authority for the arena run lifecycle. Owns state transitions, gating of gameplay systems,
    /// run timing, and orchestration of services such as scoring and health.
    /// </summary>
    [AddComponentMenu("Thrustslinger/Core/Game Manager")]
    [DisallowMultipleComponent]
    public sealed class GameManager : Singleton<GameManager>
    {
        #region Nested types

        [Serializable]
        private class GameStateEvent : UnityEvent<GameState> { }

        [Serializable]
        private class RunSummaryEvent : UnityEvent<RunSummary> { }

        [Serializable]
        private struct PoolWarmupEntry
        {
            public string key;
            public int count;
        }

        [Serializable]
        public struct SceneBindings
        {
            public TargetSpawner targetSpawner;
            public ThrusterController thrusterController;
            public MonoBehaviour[] weaponSystems;
            public MonoBehaviour[] additionalGameplaySystems;
            public MonoBehaviour[] hapticsSystems;
            public MonoBehaviour pauseInputController;
            public GameObject hudUI;
            public GameObject pauseUI;
            public GameObject gameOverUI;
            public GameObject xrMenuRayRoot;
            public MonoBehaviour scoreServiceBehaviour;
            public MonoBehaviour comboTrackerBehaviour;
            public MonoBehaviour playerHealthBehaviour;
            public MonoBehaviour hapticsRouterBehaviour;
        }

        #endregion

        #region Inspector

        [Header("Subsystem References")]
        [Tooltip("Primary spawner that should only run while in the Playing state.")]
        [SerializeField] private TargetSpawner targetSpawner;
        [Tooltip("Thruster locomotion controller (plane locked).")]
        [SerializeField] private ThrusterController thrusterController;
        [Tooltip("Player plane definition (used for recentering).")]
        [SerializeField] private PlayerPlaneDefinition playerPlaneDefinition;
        [Tooltip("Weapon systems that should be toggled when entering/leaving gameplay.")]
        [SerializeField] private MonoBehaviour[] weaponSystems;
        [Tooltip("Other gameplay scripts that must be active only during Playing.")]
        [SerializeField] private MonoBehaviour[] additionalGameplaySystems;
        [Tooltip("Systems that drive runtime haptics (optional).")]
        [SerializeField] private MonoBehaviour[] hapticsSystems;
        [Tooltip("Pause input controller that listens for XR pause button (not gated, stays active).")]
        [SerializeField] private MonoBehaviour pauseInputController;

    [Header("UI Routing")]
    [SerializeField] private GameObject hudUI;
    [SerializeField] private GameObject pauseUI;
    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private GameObject xrMenuRayRoot;

        [Header("Lifecycle Settings")]
        [Tooltip("Automatically pause the run when application focus is lost.")]
        [SerializeField] private bool autoPauseOnFocusLoss = true;
        [Tooltip("If enabled, pause will drive Time.timeScale = 0.")]
        [SerializeField] private bool pauseUsesTimeScale = true;
        [Tooltip("Seconds used as default difficulty warmup if context does not override it.")]
        [SerializeField, Min(0f)] private float defaultDifficultyWarmupSeconds = 5f;
        [Tooltip("Seconds to let physics settle during warmup before enabling gameplay.")]
        [SerializeField, Min(0f)] private float physicsSettleSeconds = 0.25f;
        [Tooltip("Optional pools to prewarm during boot.")]
        [SerializeField] private List<PoolWarmupEntry> poolWarmups = new();

    [Header("Debug Controls")]
    [Tooltip("Draws a temporary on-screen button to start the run when no menu exists yet.")]
    [SerializeField] private bool showDebugStartButton = true;
    [Tooltip("Allow a keyboard shortcut to start the run while debugging.")]
    [SerializeField] private bool allowDebugStartHotkey = true;
    [Tooltip("Key used to start the run when in MainMenu/Results while debugging.")]
    [SerializeField] private KeyCode debugStartKey = KeyCode.F5;

        [Header("Optional Services")]
        [SerializeField] private MonoBehaviour scoreServiceBehaviour;
        [SerializeField] private MonoBehaviour comboTrackerBehaviour;
        [SerializeField] private MonoBehaviour playerHealthBehaviour;
        [SerializeField] private MonoBehaviour hapticsRouterBehaviour;

        [Header("Unity Events")]
        [SerializeField] private GameStateEvent onStateEntered = new();
        [SerializeField] private RunSummaryEvent onResultsReady = new();
        
    [Header("Scene Navigation")]
    [Tooltip("Name of the main menu scene to load when quitting to menu.")]
    [SerializeField] private string menuSceneName = "MainMenu";

        #endregion

        #region Public surface

        /// <summary>Raised whenever the lifecycle state changes (oldState, newState).</summary>
        public event Action<GameState, GameState> OnStateChanged;
        /// <summary>Raised when a run officially begins (after warmup).</summary>
        public event Action<RunContext> OnRunStarted;
        /// <summary>Raised once the run has ended and the summary is finalised.</summary>
        public event Action<RunSummary> OnRunCompleted;

        /// <summary>Current lifecycle state.</summary>
        public GameState State { get; private set; } = GameState.Boot;
        /// <summary>Returns true while gameplay systems should be active.</summary>
        public bool IsPlaying => State == GameState.Playing;
        /// <summary>Returns true while the run is paused.</summary>
        public bool IsPaused => State == GameState.Paused;

        /// <summary>Pause-safe run timer (seconds) that excludes warmup and pause periods.</summary>
        public float RunTimeSeconds { get; private set; }

        /// <summary>Difficulty timer (seconds) that starts once the warmup grace period has elapsed.</summary>
        public float DifficultyTimeSeconds => Mathf.Max(0f, RunTimeSeconds - _activeDifficultyWarmupSeconds);

        /// <summary>The run context used for the active or most recent run.</summary>
        public RunContext CurrentRun { get; private set; }

        /// <summary>Latest finalised run summary (null until a run ends).</summary>
        public RunSummary CurrentSummary => _currentSummary;

        /// <summary>Context loaded at boot and edited by the main menu.</summary>
        public RunContext MenuContext => _menuContext ??= new RunContext().EnsureDefaults();

    /// <summary>True once the boot sequence has finished and the manager is in a steady state.</summary>
    public bool BootComplete => _bootComplete;

        #endregion

        #region Private state

        private readonly List<Behaviour> _gatedSystems = new();

        private IRunScoreService _scoreService;
        private IComboTracker _comboTracker;
        private IPlayerHealth _playerHealth;
        private IRunHapticsRouter _hapticsRouter;

        private RunContext _menuContext;
        private RunSummary _currentSummary;
        private Coroutine _runRoutine;
        private bool _autoPausedByFocus;
        private bool _bootComplete;
        private bool _scoreFinalised;
        private float _cachedTimeScale = 1f;
        private int _breachCount;
        private float _activeDifficultyWarmupSeconds;
        private float _lastKnownHealth;

        #endregion

        #region Unity lifecycle

        private void Awake()
        {
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            CacheGatedSystems();
            ResolveOptionalServices();
            SetGatedSystemsActive(false);
            ToggleUIForState(GameState.Boot);
            State = GameState.Boot;
        }

        private void Start()
        {
            StartCoroutine(BootSequence());
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnsubscribeHealthCallbacks();
        }

        private void Update()
        {
            if (State == GameState.Playing)
            {
                RunTimeSeconds += Time.unscaledDeltaTime;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!autoPauseOnFocusLoss) return;

            if (!hasFocus && State == GameState.Playing)
            {
                Pause(internalRequest: true);
                _autoPausedByFocus = true;
            }
            else if (hasFocus && _autoPausedByFocus && State == GameState.Paused)
            {
                _autoPausedByFocus = false;
                Resume();
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !showDebugStartButton)
            {
                return;
            }

            if (State != GameState.MainMenu && State != GameState.GameOver)
            {
                return;
            }

            const float width = 200f;
            const float height = 36f;
            var rect = new Rect(12f, Screen.height - height - 12f, width, height);
            var label = State == GameState.GameOver ? "Restart Run" : "Start Run";

            if (GUI.Button(rect, label))
            {
                StartRun(MenuContext);
            }
        }


#if UNITY_EDITOR
        [ContextMenu("Start Run (Debug)")]
        private void EditorContextStartRun()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            StartRun(MenuContext);
        }
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Starts a new run from the provided context. Valid from MainMenu or GameOver.
        /// </summary>
        public void StartRun(RunContext context)
        {
            if (State != GameState.MainMenu && State != GameState.GameOver)
            {
                Debug.LogWarning($"[GameManager] StartRun ignored while in state {State}.", this);
                return;
            }

            Debug.Log($"[GameManager] StartRun() called from state {State}", this);

            var sourceContext = context != null ? context.Clone() : MenuContext.Clone();
            CurrentRun = sourceContext.EnsureDefaults();

            if (_runRoutine != null)
            {
                StopCoroutine(_runRoutine);
            }

            Debug.Log("[GameManager] Starting BeginRunRoutine coroutine", this);
            _runRoutine = StartCoroutine(BeginRunRoutine(CurrentRun));
        }

        /// <summary>Pauses the current run.</summary>
        public void Pause() => Pause(internalRequest: false);

        private void Pause(bool internalRequest)
        {
            if (State != GameState.Playing) return;

            SetGatedSystemsActive(false);
            _hapticsRouter?.SetGameplayEnabled(false);

            if (pauseUsesTimeScale)
            {
                _cachedTimeScale = Time.timeScale == 0f ? 1f : Time.timeScale;
                Time.timeScale = 0f;
            }

            ToggleUIForState(GameState.Paused);
            SetState(GameState.Paused);
        }

        /// <summary>Resumes the run if previously paused.</summary>
        public void Resume()
        {
            if (State != GameState.Paused) return;

            if (pauseUsesTimeScale)
            {
                Time.timeScale = Mathf.Approximately(_cachedTimeScale, 0f) ? 1f : _cachedTimeScale;
            }

            ToggleUIForState(GameState.Playing);
            SetGatedSystemsActive(true);
            _hapticsRouter?.SetGameplayEnabled(true);
            SetState(GameState.Playing);
        }

        /// <summary>Ends the run and transitions to GameOver/Results.</summary>
        public void EndRun()
        {
            if (State != GameState.Playing && State != GameState.Paused)
            {
                Debug.LogWarning($"[GameManager] EndRun ignored while in state {State}.", this);
                return;
            }

            if (_runRoutine != null)
            {
                StopCoroutine(_runRoutine);
                _runRoutine = null;
            }

            if (pauseUsesTimeScale)
            {
                Time.timeScale = 1f;
            }

            // Keep gated systems disabled so player doesn't drift
            SetGatedSystemsActive(false);
            _hapticsRouter?.SetGameplayEnabled(false);
            
            // Stop player movement by freezing the rigidbody if thruster controller exists
            FreezePlayerMovement();
            
            // Finalize score BEFORE changing state so listeners can access the summary
            FinaliseScore();
            
            ToggleUIForState(GameState.GameOver);
            SetState(GameState.GameOver);
            
            OnRunCompleted?.Invoke(_currentSummary);
        }

        /// <summary>Restarts the last run using the same context.</summary>
        public void Restart()
        {
            if (State != GameState.GameOver)
            {
                Debug.LogWarning($"[GameManager] Restart ignored while in state {State}.", this);
                return;
            }

            Debug.Log("[GameManager] Restart() called - hiding UI and starting run", this);
            
            // Immediately hide game over UI to prevent it from being visible during restart
            ToggleUIForState(GameState.Boot);
            
            // Note: Unfreezing and recentering are handled in BeginRunRoutine
            var restartContext = CurrentRun?.Clone() ?? MenuContext.Clone();
            
            Debug.Log($"[GameManager] Calling StartRun with context: {restartContext != null}", this);
            StartRun(restartContext);
        }

        /// <summary>Quit back to the main menu, abandoning any active run.</summary>
        public void QuitToMenu()
        {
            StopActiveCoroutines();

            if (pauseUsesTimeScale)
            {
                Time.timeScale = 1f;
            }

            SetGatedSystemsActive(false);
            _hapticsRouter?.SetGameplayEnabled(false);
            
            // Unfreeze player movement before transitioning to menu
            UnfreezePlayerMovement();
            
            ToggleUIForState(GameState.MainMenu);
            SetState(GameState.MainMenu);
            _currentSummary = null;
            _scoreFinalised = false;
            // Load the main menu scene
            if (!string.IsNullOrEmpty(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning($"[GameManager] menuSceneName is not configured, cannot load main menu.", this);
            }
        }

        /// <summary>Registers a kill so scoring can be aggregated centrally.</summary>
        public void RegisterKill(in RunKillData killData)
        {
            _scoreService?.RegisterKill(killData);
            
            // Increment combo on successful kill
            if (_comboTracker is IRuntimeComboProvider comboProvider)
            {
                comboProvider.IncrementCombo();
            }
        }

        /// <summary>Called when a target breaches the plane so health/combo can be managed.</summary>
        public void NotifyPlaneBreach(float damageAmount)
        {
            if (_playerHealth != null && damageAmount > 0f)
            {
                _playerHealth.ApplyDamage(damageAmount);
            }
            else
            {
                _comboTracker?.BreakCombo();
            }

            _breachCount++;
        }

        /// <summary>Allows menus to persist newly selected comfort/difficulty before the next run.</summary>
        public void UpdateMenuContext(RunContext updated)
        {
            if (updated == null) return;
            _menuContext = updated.Clone().EnsureDefaults();
            SaveMenuContext(_menuContext);
            ApplyComfortSettings(_menuContext.comfort);
            MenuRunContextStore.SetMenuContext(_menuContext);
        }

        /// <summary>
        /// Applies scene-specific references (spawners, UI roots, etc.) so the GameManager can control them.
        /// Call this after loading a gameplay scene.
        /// </summary>
        public void ApplySceneBindings(SceneBindings bindings)
        {
            targetSpawner = bindings.targetSpawner;
            thrusterController = bindings.thrusterController;
            weaponSystems = bindings.weaponSystems ?? Array.Empty<MonoBehaviour>();
            additionalGameplaySystems = bindings.additionalGameplaySystems ?? Array.Empty<MonoBehaviour>();
            hapticsSystems = bindings.hapticsSystems ?? Array.Empty<MonoBehaviour>();
            pauseInputController = bindings.pauseInputController;
            hudUI = bindings.hudUI;
            pauseUI = bindings.pauseUI;
            gameOverUI = bindings.gameOverUI;
            xrMenuRayRoot = bindings.xrMenuRayRoot;

            // Update service references from scene bindings
            scoreServiceBehaviour = bindings.scoreServiceBehaviour;
            comboTrackerBehaviour = bindings.comboTrackerBehaviour;
            playerHealthBehaviour = bindings.playerHealthBehaviour;
            hapticsRouterBehaviour = bindings.hapticsRouterBehaviour;

            // Re-resolve services after scene transition to ensure references are valid
            ResolveOptionalServices();

            CacheGatedSystems();
            ToggleUIForState(State);
        }

        /// <summary>
        /// Clears scene references when a gameplay scene unloads to avoid dangling references.
        /// </summary>
        public void ClearSceneBindings()
        {
            targetSpawner = null;
            thrusterController = null;
            weaponSystems = Array.Empty<MonoBehaviour>();
            additionalGameplaySystems = Array.Empty<MonoBehaviour>();
            hapticsSystems = Array.Empty<MonoBehaviour>();
            pauseInputController = null;
            hudUI = null;
            pauseUI = null;
            gameOverUI = null;
            xrMenuRayRoot = null;

            // Clear service references to avoid dangling pointers after scene transitions
            UnsubscribeHealthCallbacks();
            scoreServiceBehaviour = null;
            comboTrackerBehaviour = null;
            playerHealthBehaviour = null;
            hapticsRouterBehaviour = null;
            _scoreService = null;
            _comboTracker = null;
            _playerHealth = null;
            _hapticsRouter = null;

            CacheGatedSystems();
        }

        #endregion

        #region Boot & warmup

        private IEnumerator BootSequence()
        {
            SetState(GameState.Boot);
            ToggleUIForState(GameState.Boot);
            yield return null; // allow single frame for service instantiation

            _menuContext = LoadMenuContext();
            ApplyComfortSettings(_menuContext.comfort);
            RecenterRig();

            yield return PrewarmPools();
            yield return SettlePhysics();

            _bootComplete = true;
            ToggleUIForState(GameState.MainMenu);
            SetState(GameState.MainMenu);
        }

        private IEnumerator BeginRunRoutine(RunContext context)
        {
            // Ensure boot sequence has completed before starting a run.
            while (!_bootComplete)
            {
                yield return null;
            }

            SetGatedSystemsActive(false);
            ToggleUIForState(GameState.Boot);

            _activeDifficultyWarmupSeconds = context?.difficulty?.warmupSeconds ?? defaultDifficultyWarmupSeconds;
            RunTimeSeconds = 0f;
            _breachCount = 0;
            _scoreFinalised = false;
            _currentSummary = null;

            _scoreService?.ResetScore();
            _comboTracker?.ResetCombo();
            _playerHealth?.ResetHealth();
            _lastKnownHealth = _playerHealth?.CurrentHealth ?? float.NaN;

            // Reset weapon systems (ammo, reload state, etc.)
            ResetWeaponSystems();

            ApplyComfortSettings(context.comfort);
            
            // Unfreeze player movement and recenter to start position
            UnfreezePlayerMovement();
            RecenterRig();

            yield return PrewarmPools();
            yield return SettlePhysics();

            ToggleUIForState(GameState.Playing);
            SetGatedSystemsActive(true);
            _hapticsRouter?.SetGameplayEnabled(true);
            SetState(GameState.Playing);
            OnRunStarted?.Invoke(context);
            _scoreService?.BeginRun(context);
        }

        private IEnumerator PrewarmPools()
        {
            if (poolWarmups == null || poolWarmups.Count == 0)
            {
                yield break;
            }

            var pool = PoolService.Instance;
            foreach (var entry in poolWarmups)
            {
                if (string.IsNullOrWhiteSpace(entry.key) || entry.count <= 0) continue;
                pool.Prewarm(entry.key, entry.count);
                yield return null; // spread cost over frames
            }
        }

        private IEnumerator SettlePhysics()
        {
            if (physicsSettleSeconds <= 0f) yield break;
            var elapsed = 0f;
            while (elapsed < physicsSettleSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        #endregion

        #region Helpers

        private void CacheGatedSystems()
        {
            _gatedSystems.Clear();
            AddIfValid(targetSpawner);
            AddIfValid(thrusterController);
            AddRangeIfValid(weaponSystems);
            AddRangeIfValid(additionalGameplaySystems);
            AddRangeIfValid(hapticsSystems);
        }

        private void AddIfValid(Behaviour behaviour)
        {
            if (behaviour == null) return;
            if (!_gatedSystems.Contains(behaviour))
            {
                _gatedSystems.Add(behaviour);
            }
        }

        private void AddRangeIfValid(IEnumerable<MonoBehaviour> behaviours)
        {
            if (behaviours == null) return;
            foreach (var behaviour in behaviours)
            {
                AddIfValid(behaviour);
            }
        }

        private void SetGatedSystemsActive(bool active)
        {
            foreach (var behaviour in _gatedSystems)
            {
                if (behaviour == null) continue;
                behaviour.enabled = active;
            }
        }

        /// <summary>
        /// Resets all weapon systems to initial state (ammo, reload state, etc.).
        /// Called at the start of each run to ensure consistent starting conditions.
        /// </summary>
        private void ResetWeaponSystems()
        {
            if (weaponSystems == null) return;

            foreach (var weaponBehaviour in weaponSystems)
            {
                if (weaponBehaviour == null) continue;

                // Check if it's a ProjectileWeapon and call its reset method
                if (weaponBehaviour is Gameplay.ProjectileWeapon projectileWeapon)
                {
                    projectileWeapon.ResetWeapon();
                }
                // HitscanWeapon doesn't need reset (no ammo system)
                // Future weapon types can be added here
            }
        }

        private void ResolveOptionalServices()
        {
            // Try to resolve from assigned references first
            _scoreService = ResolveService<IRunScoreService>(scoreServiceBehaviour, nameof(scoreServiceBehaviour));
            _comboTracker = ResolveService<IComboTracker>(comboTrackerBehaviour, nameof(comboTrackerBehaviour));
            _playerHealth = ResolveService<IPlayerHealth>(playerHealthBehaviour, nameof(playerHealthBehaviour));
            _hapticsRouter = ResolveService<IRunHapticsRouter>(hapticsRouterBehaviour, nameof(hapticsRouterBehaviour));

            // Fallback: auto-find services if not assigned (helpful after scene transitions)
            if (_scoreService == null)
            {
                var fallback = FindFirstObjectByType<RuntimeScoreService>();
                if (fallback != null)
                {
                    scoreServiceBehaviour = fallback;
                    _scoreService = fallback;
                }
            }

            if (_comboTracker == null)
            {
                var fallback = FindFirstObjectByType<ComboTracker>();
                if (fallback != null)
                {
                    comboTrackerBehaviour = fallback;
                    _comboTracker = fallback;
                }
            }

            if (_playerHealth == null)
            {
                var fallback = FindFirstObjectByType<PlayerHealth>();
                if (fallback != null)
                {
                    playerHealthBehaviour = fallback;
                    _playerHealth = fallback;
                }
            }

            if (_playerHealth != null)
            {
                SubscribeHealthCallbacks();
            }
        }

        private T ResolveService<T>(MonoBehaviour behaviour, string fieldName) where T : class
        {
            if (behaviour == null) return null;
            if (behaviour is T service) return service;

            Debug.LogWarning($"[GameManager] Field '{fieldName}' does not implement required interface {typeof(T).Name}.", behaviour);
            return null;
        }

        private void SubscribeHealthCallbacks()
        {
            if (_playerHealth == null) return;
            _playerHealth.OnHealthChanged += HandleHealthChanged;
            _playerHealth.OnHealthDepleted += HandleHealthDepleted;
            _lastKnownHealth = _playerHealth.CurrentHealth;
        }

        private void UnsubscribeHealthCallbacks()
        {
            if (_playerHealth == null) return;
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
            _playerHealth.OnHealthDepleted -= HandleHealthDepleted;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (float.IsNaN(_lastKnownHealth))
            {
                _lastKnownHealth = current;
                return;
            }

            if (current < _lastKnownHealth && State == GameState.Playing)
            {
                _comboTracker?.BreakCombo();
            }

            _lastKnownHealth = current;

            if (current <= 0f)
            {
                HandleHealthDepleted();
            }
        }

        private void HandleHealthDepleted()
        {
            if (State == GameState.Playing || State == GameState.Paused)
            {
                EndRun();
            }
        }

        private void FinaliseScore()
        {
            if (_scoreFinalised) return;

            _currentSummary = _scoreService?.BuildSummary() ?? new RunSummary();
            if (_currentSummary.context == null)
            {
                _currentSummary.context = CurrentRun?.Clone();
            }

            _currentSummary.runTimeSeconds = RunTimeSeconds;
            _currentSummary.difficultyTimeSeconds = DifficultyTimeSeconds;
            _currentSummary.breaches = _breachCount;

            _scoreService?.FinalizeRun(_currentSummary);
            _scoreService?.SubmitResults(_currentSummary);
            MenuRunContextStore.CacheSummary(_currentSummary);
            _scoreFinalised = true;
        }

        private void ToggleUIForState(GameState state)
        {
            if (hudUI) hudUI.SetActive(state == GameState.Playing || state == GameState.Paused);
            if (pauseUI) pauseUI.SetActive(state == GameState.Paused);
            if (gameOverUI) gameOverUI.SetActive(state == GameState.GameOver);

            var enableMenuRay = state == GameState.MainMenu || state == GameState.GameOver || state == GameState.Paused;
            SetMenuRayActive(enableMenuRay);
        }

        private void SetMenuRayActive(bool active)
        {
            if (xrMenuRayRoot == null) return;
            xrMenuRayRoot.SetActive(active);
        }

        private void SetState(GameState newState)
        {
            if (State == newState) return;
            var previous = State;
            State = newState;
            onStateEntered.Invoke(newState);
            OnStateChanged?.Invoke(previous, newState);
        }

        private void StopActiveCoroutines()
        {
            if (_runRoutine != null)
            {
                StopCoroutine(_runRoutine);
                _runRoutine = null;
            }
        }

        private RunContext LoadMenuContext()
        {
            // TODO: Replace with persistent storage (PlayerPrefs / JSON file) when implemented.
            return (_menuContext ?? new RunContext()).EnsureDefaults();
        }

        private void SaveMenuContext(RunContext context)
        {
            // TODO: Persist to storage. For now this is a no-op placeholder.
        }

        private void ApplyComfortSettings(ComfortOptions comfort)
        {
            if (comfort == null) return;
            // TODO: Pipe through to locomotion, vignette, handedness, and other comfort systems.
        }

        /// <summary>
        /// Recenters the player rig to the center of the plane bounds.
        /// Useful for restart to ensure player starts from a consistent position.
        /// </summary>
        private void RecenterRig()
        {
            if (thrusterController == null || playerPlaneDefinition == null) return;

            var rb = thrusterController.GetComponent<Rigidbody>();
            if (rb == null) return;

            // Calculate center of the plane in world space
            var planePoint = playerPlaneDefinition.PlanePoint;
            var normal = playerPlaneDefinition.Normal;
            var centerOffset = playerPlaneDefinition.CenterOffset;

            // Get plane axes
            var refRight = playerPlaneDefinition.transform.right;
            var xAxis = Vector3.ProjectOnPlane(refRight, normal).normalized;
            var yAxis = Vector3.Cross(normal, xAxis).normalized;

            // Calculate world position at the center of the bounds
            var centerPosition = planePoint + xAxis * centerOffset.x + yAxis * centerOffset.y;

            // Temporarily make kinematic for clean teleport, then restore
            var wasKinematic = rb.isKinematic;
            
            // IMPORTANT: Clear velocities BEFORE setting kinematic
            // Unity doesn't allow setting velocity on kinematic bodies
            if (!wasKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            rb.isKinematic = true;
            rb.position = centerPosition;
            
            // Restore kinematic state and clear velocities if now dynamic
            rb.isKinematic = wasKinematic;
            if (!wasKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void FreezePlayerMovement()
        {
            if (thrusterController == null) return;
            
            var rb = thrusterController.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Stop all movement and freeze the rigidbody
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        private void UnfreezePlayerMovement()
        {
            if (thrusterController == null) return;
            
            var rb = thrusterController.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Restore rigidbody to dynamic state
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheGatedSystems();
        }
#endif
    }
}
