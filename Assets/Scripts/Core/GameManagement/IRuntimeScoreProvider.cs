using System;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Optional extension interface for score services that surfaces runtime score updates.
    /// </summary>
    public interface IRuntimeScoreProvider
    {
        /// <summary>Raised whenever the aggregate score changes.</summary>
        event Action<float> ScoreChanged;

        /// <summary>Current accumulated score for the active run.</summary>
        float CurrentScore { get; }

        /// <summary>Total confirmed kills recorded for the active run.</summary>
        int KillCount { get; }
    }
}
