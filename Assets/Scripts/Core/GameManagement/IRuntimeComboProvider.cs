using System;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Optional extension interface for combo trackers that surfaces runtime combo updates.
    /// </summary>
    public interface IRuntimeComboProvider
    {
        /// <summary>Raised whenever the combo count changes.</summary>
        event Action<int> ComboChanged;

        /// <summary>Current combo count for the active run.</summary>
        int CurrentCombo { get; }

        /// <summary>The highest combo achieved across all runs.</summary>
        int HighestCombo { get; }

        /// <summary>Increments the combo counter by one.</summary>
        void IncrementCombo();
    }
}
