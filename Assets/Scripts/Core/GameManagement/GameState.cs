using System;

namespace Thrustslinger.Core
{
    /// <summary>
    /// High-level lifecycle states driven by the <see cref="GameManager"/>. Use this to self-enable subsystems.
    /// </summary>
    [Serializable]
    public enum GameState
    {
        Boot = 0,
        MainMenu = 1,
        Playing = 2,
        Paused = 3,
        GameOver = 4,
        Results = 5
    }
}
