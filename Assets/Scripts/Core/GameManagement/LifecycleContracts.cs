using System;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Contract for the scoring system so the <see cref="GameManager"/> can orchestrate run lifecycle.
    /// </summary>
    public interface IRunScoreService
    {
        void ResetScore();
        void BeginRun(RunContext context);
        void RegisterKill(in RunKillData killData);
        RunSummary BuildSummary();
        void FinalizeRun(RunSummary summary);
        void SubmitResults(RunSummary summary);
    }

    /// <summary>
    /// Contract for combo tracking so combos can be reset/broken by lifecycle events.
    /// </summary>
    public interface IComboTracker
    {
        void ResetCombo();
        void BreakCombo();
    }

    /// <summary>
    /// Interface for accessing player health in a run. Provides events for damage/death as well
    /// as methods to reset/apply damage.
    /// </summary>
    public interface IPlayerHealth
    {
        event Action<float, float> OnHealthChanged; // (current, max)
        event Action OnHealthDepleted;

        float CurrentHealth { get; }
        float MaxHealth { get; }

        void ResetHealth();
        void ApplyDamage(float amount);
    }

    /// <summary>
    /// Optional service that routes haptics or other feedback depending on gameplay state.
    /// </summary>
    public interface IRunHapticsRouter
    {
        void SetGameplayEnabled(bool enabled);
    }
}
