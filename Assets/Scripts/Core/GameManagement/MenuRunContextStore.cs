using System;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Static helper that bridges data between the VR main menu scene and the gameplay scene.
    /// Stores the latest menu selections, pending run requests, and the last completed run summary.
    /// </summary>
    public static class MenuRunContextStore
    {
        private static RunContext _menuContext;
        private static RunContext _pendingStart;
        private static RunSummary _lastSummary;

        /// <summary>
        /// Returns a cloned copy of the most recently stored menu context (or a default one if none exists).
        /// </summary>
        public static RunContext GetMenuContext()
        {
            return CloneOrDefault(_menuContext);
        }

        /// <summary>
        /// Persists the latest menu selections so they survive scene loads.
        /// </summary>
        public static void SetMenuContext(RunContext context)
        {
            _menuContext = CloneOrDefault(context);
        }

        /// <summary>
        /// Stores a run request that should be consumed by the gameplay scene when it loads.
        /// </summary>
        public static void SetPendingStart(RunContext context)
        {
            _pendingStart = CloneOrDefault(context);
        }

        /// <summary>
        /// Returns true if a pending run request exists and outputs a clone of it. The stored request is cleared.
        /// </summary>
        public static bool TryConsumePendingStart(out RunContext context)
        {
            if (_pendingStart == null)
            {
                context = null;
                return false;
            }

            context = CloneOrDefault(_pendingStart);
            _pendingStart = null;
            return true;
        }

        /// <summary>
        /// Clears any queued run request without consuming it.
        /// </summary>
        public static void ClearPendingStart()
        {
            _pendingStart = null;
        }

        /// <summary>
        /// Stores a copy of the most recent run summary so the menu can display it.
        /// </summary>
        public static void CacheSummary(RunSummary summary)
        {
            _lastSummary = summary?.Clone();
        }

        /// <summary>
        /// Returns the cached run summary (clone) or null if none exists.
        /// </summary>
        public static RunSummary GetLastSummary()
        {
            return _lastSummary?.Clone();
        }

        private static RunContext CloneOrDefault(RunContext source)
        {
            var clone = (source?.Clone() ?? new RunContext()).EnsureDefaults();
            // Ensure nested objects exist after cloning
            clone.comfort ??= new ComfortOptions();
            clone.difficulty ??= new DifficultyOptions();
            return clone;
        }
    }
}
