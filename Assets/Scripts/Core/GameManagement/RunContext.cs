using System;
using UnityEngine;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Captures player-selected parameters for a single arena run. Built from the main menu and
    /// passed into the <see cref="GameManager"/> when starting the game loop.
    /// </summary>
    [Serializable]
    public class RunContext
    {
        [Tooltip("Identifier for the selected game mode or ruleset.")]
        public string modeId = "default";

        [Tooltip("Optional human-readable label identified by the UI.")]
        public string modeLabel = "Default";

        [Tooltip("Seed used by procedural systems (spawner, target layout, etc.). 0 = auto-generate.")]
        public int seed;

        [Tooltip("Comfort-related preferences applied before a run starts.")]
        public ComfortOptions comfort = new();

        [Tooltip("Difficulty/scoring configuration for the run.")]
        public DifficultyOptions difficulty = new();

        /// <summary>
        /// Ensures the context contains valid defaults and returns itself for fluent usage.
        /// </summary>
        public RunContext EnsureDefaults()
        {
            if (seed == 0)
            {
                seed = Environment.TickCount;
            }

            comfort ??= new ComfortOptions();
            difficulty ??= new DifficultyOptions();
            return this;
        }

        /// <summary>
        /// Creates a deep copy suitable for reuse when restarting a run.
        /// </summary>
        public RunContext Clone()
        {
            return new RunContext
            {
                modeId = modeId,
                modeLabel = modeLabel,
                seed = seed,
                comfort = comfort?.Clone() ?? new ComfortOptions(),
                difficulty = difficulty?.Clone() ?? new DifficultyOptions()
            };
        }

        public override string ToString()
        {
            return $"RunContext(mode={modeId}, seed={seed}, comfort={comfort}, difficulty={difficulty})";
        }
    }

    [Serializable]
    public class ComfortOptions
    {
        [Tooltip("If true the experience should favour a seated posture.")]
        public bool seatedMode = true;

        [Tooltip("If true use snap turning; otherwise smooth turn.")]
        public bool useSnapTurn = true;

        [Tooltip("Dominant hand selection (true = left-dominant).")]
        public bool leftHandDominant;

        [Tooltip("Strength of the comfort vignette while moving (0..1).")]
        [Range(0f, 1f)]
        public float vignetteStrength = 1f;

        public ComfortOptions Clone()
        {
            return new ComfortOptions
            {
                seatedMode = seatedMode,
                useSnapTurn = useSnapTurn,
                leftHandDominant = leftHandDominant,
                vignetteStrength = vignetteStrength
            };
        }

        public override string ToString()
        {
            return $"Comfort(seated={seatedMode}, snap={useSnapTurn}, leftDominant={leftHandDominant}, vignette={vignetteStrength:0.##})";
        }
    }

    [Serializable]
    public class DifficultyOptions
    {
        [Tooltip("Identifier for the difficulty preset applied to scoring/spawning.")]
        public string presetId = "default";

        [Tooltip("Additional scalar applied to difficulty ramps (1 = baseline).")]
        [Range(0.1f, 5f)]
        public float rampScalar = 1f;

        [Tooltip("Seconds of grace period before difficulty ramps start advancing.")]
        [Min(0f)]
        public float warmupSeconds = 5f;

        public DifficultyOptions Clone()
        {
            return new DifficultyOptions
            {
                presetId = presetId,
                rampScalar = rampScalar,
                warmupSeconds = warmupSeconds
            };
        }

        public override string ToString()
        {
            return $"Difficulty(preset={presetId}, ramp={rampScalar:0.##}, warmup={warmupSeconds:0.#})";
        }
    }
}
